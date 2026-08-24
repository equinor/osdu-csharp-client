using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Identity.Client;

namespace Equinor.OsduCsharpClient.Facade.Auth;

/// <summary>
/// Acquires a token via MSAL interactive browser login with a persistent file cache.
/// Falls back to interactive if silent acquisition fails.
/// </summary>
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
    private readonly SemaphoreSlim _cacheGate = new(1, 1);
    private bool _cacheRegistered;

    public MsalInteractiveTokenProvider(
        OsduConfig config,
        string? tokenCachePath = null,
        ILoggerFactory? loggerFactory = null)
    {
        _scopes = config.ScopesArray;
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

        var accounts = await _app.GetAccountsAsync();
        AuthenticationResult? result = null;

        if (accounts.Any())
        {
            try
            {
                result = await _app.AcquireTokenSilent(_scopes, accounts.First())
                    .ExecuteAsync(cancellationToken);
                _log.LogDebug("Token acquired silently.");
            }
            catch (MsalUiRequiredException) { }
        }

        if (result is null)
        {
            _log.LogInformation("Interactive auth flow required — opening browser.");
            result = await _app.AcquireTokenInteractive(_scopes)
                .ExecuteAsync(cancellationToken);
            _log.LogDebug("Token acquired via interactive flow.");
        }

        return result.AccessToken
            ?? throw new OsduException("Authentication failed: no access token returned.");
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
