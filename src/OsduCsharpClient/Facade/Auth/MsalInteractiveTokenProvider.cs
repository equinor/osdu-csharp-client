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

    public MsalInteractiveTokenProvider(
        OsduConfig config,
        string? tokenCachePath = null,
        ILoggerFactory? loggerFactory = null)
    {
        _scopes = config.ScopesArray;
        _log = (loggerFactory ?? NullLoggerFactory.Instance)
            .CreateLogger<MsalInteractiveTokenProvider>();

        var cachePath = tokenCachePath
            ?? Environment.GetEnvironmentVariable("OSDU_MSAL_CACHE_PATH")
            ?? DefaultCachePath;

        Directory.CreateDirectory(Path.GetDirectoryName(cachePath)!);

        _app = PublicClientApplicationBuilder
            .Create(config.ClientId)
            .WithAuthority(config.Authority)
            .WithRedirectUri("http://localhost")
            .Build();

        _app.UserTokenCache.SetBeforeAccess(args =>
        {
            if (File.Exists(cachePath))
                args.TokenCache.DeserializeMsalV3(File.ReadAllBytes(cachePath));
        });
        _app.UserTokenCache.SetAfterAccess(args =>
        {
            if (args.HasStateChanged)
                File.WriteAllBytes(cachePath, args.TokenCache.SerializeMsalV3());
        });
    }

    public async Task<string> GetTokenAsync(CancellationToken cancellationToken = default)
    {
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
}
