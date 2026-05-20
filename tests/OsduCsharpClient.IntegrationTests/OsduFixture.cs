using DotNetEnv;
using Equinor.OsduCsharpClient.Facade;
using Equinor.OsduCsharpClient.Facade.Auth;
using Microsoft.Kiota.Abstractions.Authentication;
using Microsoft.Kiota.Http.HttpClientLibrary;
using Xunit;

namespace OsduCsharpClient.IntegrationTests;

/// <summary>
/// xUnit collection fixture: acquires MSAL auth once and exposes an <see cref="OsduClient"/>
/// pre-configured for all OSDU services.
/// </summary>
public class OsduFixture : IAsyncLifetime
{
    private string _accessToken = string.Empty;

    public TestConfig Config { get; } = new();
    public OsduClient Client { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        Env.NoClobber().TraversePath().Load();
        var config = OsduConfig.FromEnvironment();
        var tokenProvider = new MsalInteractiveTokenProvider(config);

        // Acquire token once for the whole test collection.
        _accessToken = await tokenProvider.GetTokenAsync();
        Client = new OsduClient(config, new StaticTokenProvider(_accessToken));
    }

    /// <summary>
    /// Creates a Kiota <see cref="HttpClientRequestAdapter"/> for the given base URL using
    /// the already-acquired token. Useful for tests that still instantiate service clients
    /// directly rather than using <see cref="Client"/>.
    /// </summary>
    public HttpClientRequestAdapter CreateAdapter(string baseUrl)
    {
        var authProvider = new BaseBearerTokenAuthenticationProvider(
            new StaticKiotaTokenProvider(_accessToken));
        return new HttpClientRequestAdapter(authProvider) { BaseUrl = baseUrl };
    }

    public ValueTask DisposeAsync()
    {
        Client?.Dispose();
        return ValueTask.CompletedTask;
    }

    private sealed class StaticKiotaTokenProvider(string token) : IAccessTokenProvider
    {
        public Task<string> GetAuthorizationTokenAsync(
            Uri uri,
            Dictionary<string, object>? additionalAuthenticationContext = null,
            CancellationToken cancellationToken = default) => Task.FromResult(token);

        public AllowedHostsValidator AllowedHostsValidator { get; } = new();
    }
}

[CollectionDefinition("Osdu")]
public class OsduCollection : ICollectionFixture<OsduFixture>;


