using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Identity.Client;

namespace Equinor.OsduCsharpClient.Facade.Auth;

/// <summary>
/// Acquires a token via MSAL device code flow. The user is prompted to visit a URL
/// and enter a code — no browser on the machine running the code is required.
/// Suitable for headless scripts and SSH sessions.
/// </summary>
public sealed class MsalDeviceFlowTokenProvider : ITokenProvider
{
    private static readonly string DefaultCachePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".osdu",
        "msal_cache.bin");

    private readonly IPublicClientApplication _app;
    private readonly string[] _scopes;
    private readonly Action<DeviceCodeResult> _prompt;
    private readonly ILogger _log;

    /// <param name="config">OSDU configuration.</param>
    /// <param name="tokenCachePath">Override path for the persistent token cache file.</param>
    /// <param name="prompt">
    /// Callback invoked with the <see cref="DeviceCodeResult"/> so the caller can display
    /// the device code message. Defaults to writing <see cref="DeviceCodeResult.Message"/>
    /// to <see cref="Console.WriteLine(string)"/>.
    /// </param>
    /// <param name="loggerFactory">Optional logger factory.</param>
    public MsalDeviceFlowTokenProvider(
        OsduConfig config,
        string? tokenCachePath = null,
        Action<DeviceCodeResult>? prompt = null,
        ILoggerFactory? loggerFactory = null)
    {
        _scopes = config.ScopesArray;
        _prompt = prompt ?? (result => Console.WriteLine(result.Message));
        _log = (loggerFactory ?? NullLoggerFactory.Instance)
            .CreateLogger<MsalDeviceFlowTokenProvider>();

        var cachePath = tokenCachePath
            ?? Environment.GetEnvironmentVariable("OSDU_MSAL_CACHE_PATH")
            ?? DefaultCachePath;

        Directory.CreateDirectory(Path.GetDirectoryName(cachePath)!);

        _app = PublicClientApplicationBuilder
            .Create(config.ClientId)
            .WithAuthority(config.Authority)
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
            _log.LogInformation("Device code flow required — awaiting user code entry.");
            result = await _app.AcquireTokenWithDeviceCode(_scopes, deviceCodeResult =>
            {
                _prompt(deviceCodeResult);
                return Task.CompletedTask;
            }).ExecuteAsync(cancellationToken);
            _log.LogDebug("Token acquired via device code flow.");
        }

        return result.AccessToken
            ?? throw new OsduException("Authentication failed: no access token returned.");
    }
}
