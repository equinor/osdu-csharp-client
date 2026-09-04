using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Identity.Client;

namespace Equinor.OsduCsharpClient.Facade.Auth;

/// <summary>
/// Acquires a token via MSAL interactive browser login with a persistent file cache.
/// Falls back to interactive if silent acquisition fails.
/// </summary>
/// <remarks>
/// One cache can hold several accounts. Where a person has more than one — a normal account
/// and a separate privileged one is the common case — pass <c>username</c> to say which is
/// meant. Without it the first cached account wins, which is arbitrary from the caller's
/// point of view and silently so. Use <see cref="GetCachedUsernamesAsync"/> to find out what
/// the cache holds and let the user choose.
/// </remarks>
public sealed class MsalInteractiveTokenProvider : ITokenProvider
{
    private static readonly string DefaultCachePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".osdu",
        "msal_cache.bin");

    private readonly IPublicClientApplication _app;
    private readonly string[] _scopes;
    private readonly ILogger _log;
    private readonly string _cachePath;
    private readonly string? _username;
    private readonly SemaphoreSlim _cacheGate = new(1, 1);
    private bool _cacheRegistered;

    /// <param name="username">
    /// Which account to use, when the cache holds more than one. Matched case-insensitively
    /// against the cached usernames, and used as the sign-in hint when no cached account
    /// matches. Omit to keep the previous behaviour of taking the first cached account.
    /// </param>
    public MsalInteractiveTokenProvider(
        OsduConfig config,
        string? tokenCachePath = null,
        ILoggerFactory? loggerFactory = null,
        string? username = null)
    {
        _scopes = config.ScopesArray;
        _username = string.IsNullOrWhiteSpace(username) ? null : username.Trim();
        _log = (loggerFactory ?? NullLoggerFactory.Instance)
            .CreateLogger<MsalInteractiveTokenProvider>();

        _cachePath = tokenCachePath
            ?? Environment.GetEnvironmentVariable("OSDU_MSAL_CACHE_PATH")
            ?? DefaultCachePath;

        _app = PublicClientApplicationBuilder
            .Create(config.ClientId)
            .WithAuthority(config.Authority)
            .WithRedirectUri("http://localhost")
            .Build();

    }

    public async Task<string> GetTokenAsync(CancellationToken cancellationToken = default)
    {
        await EnsureCacheRegisteredAsync().ConfigureAwait(false);

        var accounts = (await _app.GetAccountsAsync()).ToList();
        AuthenticationResult? result = null;

        var account = SelectAccount(accounts, _username);

        if (account is not null)
        {
            try
            {
                result = await _app.AcquireTokenSilent(_scopes, account)
                    .ExecuteAsync(cancellationToken);
                _log.LogDebug("Token acquired silently for {Username}.", account.Username);
            }
            catch (MsalUiRequiredException) { }
        }

        if (result is null)
        {
            _log.LogInformation("Interactive auth flow required — opening browser.");
            var request = _app.AcquireTokenInteractive(_scopes);
            if (_username is not null)
            {
                // Lands the browser on the intended account instead of whichever one the
                // existing browser session happens to be signed in as.
                request = request.WithLoginHint(_username);
            }

            result = await request.ExecuteAsync(cancellationToken);
            _log.LogDebug("Token acquired via interactive flow.");
        }

        EnsureExpectedAccount(_username, result.Account?.Username);

        return result.AccessToken
            ?? throw new OsduException("Authentication failed: no access token returned.");
    }

    /// <summary>
    /// The usernames this provider's token cache currently holds, in MSAL's order.
    /// </summary>
    /// <remarks>
    /// Lets a caller show the user what they are signed into, and decide for itself what to
    /// do when there is more than one — a CLI can refuse to guess and name the alternatives,
    /// which is the only honest answer when the choice is the user's to make.
    /// </remarks>
    public async Task<IReadOnlyList<string>> GetCachedUsernamesAsync()
    {
        await EnsureCacheRegisteredAsync().ConfigureAwait(false);
        return (await _app.GetAccountsAsync()).Select(a => a.Username).ToList();
    }


    /// <summary>
    /// Picks the cached account to try silently: the one whose username matches, or the
    /// first when no username was asked for.
    /// </summary>
    internal static IAccount? SelectAccount(IReadOnlyList<IAccount> accounts, string? username) =>
        username is null
            ? accounts.FirstOrDefault()
            : accounts.FirstOrDefault(a => string.Equals(
                a.Username, username, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Fails when the account that signed in is not the one that was asked for.
    /// </summary>
    /// <remarks>
    /// A login hint is a suggestion, not a constraint. The user can pick a different account
    /// in the browser and MSAL returns that token quite happily — handing the caller a token
    /// for an identity it did not ask for, which is the failure this whole mechanism exists
    /// to prevent. Loud is the only safe option: a warning would be missed, and the caller
    /// would act on the wrong identity's permissions.
    /// </remarks>
    internal static void EnsureExpectedAccount(string? requested, string? signedIn)
    {
        if (requested is null || signedIn is null) return;
        if (string.Equals(requested, signedIn, StringComparison.OrdinalIgnoreCase)) return;

        throw new OsduException(
            $"Signed in as {signedIn}, but {requested} was requested. "
            + $"Sign in again and choose {requested}.");
    }

    /// <summary>Registers the OS-encrypted token cache on first use.</summary>
    /// <remarks>
    /// <c>MsalCacheHelper</c> is created asynchronously and a constructor cannot await, so
    /// this happens on the first token request rather than sync-over-async in the
    /// constructor. Costs one guarded flag check per call thereafter.
    /// </remarks>
    private async Task EnsureCacheRegisteredAsync()
    {
        if (_cacheRegistered) return;

        await _cacheGate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_cacheRegistered) return;
            await TokenCacheStorage.TryRegisterAsync(_app.UserTokenCache, _cachePath, _log)
                .ConfigureAwait(false);
            // Set even when persistence was unavailable: the warning is already logged, and
            // retrying on every request would only repeat it.
            _cacheRegistered = true;
        }
        finally
        {
            _cacheGate.Release();
        }
    }
}
