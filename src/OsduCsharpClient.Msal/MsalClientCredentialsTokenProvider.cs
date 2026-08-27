using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Identity.Client;

namespace Equinor.OsduCsharpClient.Facade.Auth;

/// <summary>
/// Acquires a token via MSAL client-credentials flow (app identity, no user interaction).
/// Suitable for CI/headless environments.
/// </summary>
public sealed class MsalClientCredentialsTokenProvider : ITokenProvider
{
    private readonly IConfidentialClientApplication _app;
    private readonly string[] _scopes;
    private readonly ILogger _log;

    public MsalClientCredentialsTokenProvider(
        OsduConfig config,
        string clientSecret,
        ILoggerFactory? loggerFactory = null)
    {
        _scopes = config.ScopesArray;
        _log = (loggerFactory ?? NullLoggerFactory.Instance)
            .CreateLogger<MsalClientCredentialsTokenProvider>();

        _app = ConfidentialClientApplicationBuilder
            .Create(config.ClientId)
            .WithClientSecret(clientSecret)
            .WithAuthority(config.Authority)
            .Build();
    }

    public async Task<string> GetTokenAsync(CancellationToken cancellationToken = default)
    {
        _log.LogDebug("Acquiring token via client credentials flow.");
        var result = await _app.AcquireTokenForClient(_scopes)
            .ExecuteAsync(cancellationToken);

        _log.LogDebug("Token acquired (from cache: {FromCache}).", result.AuthenticationResultMetadata.TokenSource);

        return result.AccessToken
            ?? throw new OsduException("Authentication failed: no access token returned.");
    }
}
