using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Extensions.Msal;
using Microsoft.Kiota.Abstractions.Authentication;
using Microsoft.Kiota.Http.HttpClientLibrary;
using Xunit;

namespace OsduCsharpClient.IntegrationTests;

/// <summary>
/// Provides a bearer token via MSAL interactive login with a persistent cache.
/// Mirrors auth_fixture.py.
/// </summary>
file class StaticTokenProvider(string token) : IAccessTokenProvider
{
    public Task<string> GetAuthorizationTokenAsync(
        Uri uri,
        Dictionary<string, object>? additionalAuthenticationContext = null,
        CancellationToken cancellationToken = default) => Task.FromResult(token);

    public AllowedHostsValidator AllowedHostsValidator { get; } = new();
}

/// <summary>
/// xUnit collection fixture: acquires auth once and exposes a factory for
/// creating Kiota request adapters scoped to a service base URL.
/// </summary>
public class OsduFixture : IAsyncLifetime
{
    private static readonly string TokenCachePath =
        Environment.GetEnvironmentVariable("OSDU_MSAL_CACHE_PATH")
        ?? Path.Combine(Directory.GetCurrentDirectory(), ".msal_token_cache.bin");

    public TestConfig Config { get; } = new();
    public string AccessToken { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        var storageProperties = new StorageCreationPropertiesBuilder(
                Path.GetFileName(TokenCachePath),
                Path.GetDirectoryName(TokenCachePath)!)
            .WithMacKeyChain("OsduCsharpClient", "MSALCache")
            .Build();

        var cacheHelper = await MsalCacheHelper.CreateAsync(storageProperties);

        var app = PublicClientApplicationBuilder
            .Create(Config.ClientId)
            .WithAuthority(Config.Authority)
            .WithRedirectUri("http://localhost")
            .Build();

        cacheHelper.RegisterCache(app.UserTokenCache);

        var scopes = Config.Scopes.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var accounts = await app.GetAccountsAsync();

        AuthenticationResult? result = null;
        if (accounts.Any())
        {
            try
            {
                result = await app.AcquireTokenSilent(scopes, accounts.First()).ExecuteAsync();
            }
            catch (MsalUiRequiredException) { }
        }

        result ??= await app.AcquireTokenInteractive(scopes).ExecuteAsync();

        AccessToken = result.AccessToken
            ?? throw new InvalidOperationException("Authentication failed: no access token returned.");
    }

    /// <summary>Creates a Kiota HttpClientRequestAdapter for the given base URL.</summary>
    public HttpClientRequestAdapter CreateAdapter(string baseUrl)
    {
        var authProvider = new BaseBearerTokenAuthenticationProvider(
            new StaticTokenProvider(AccessToken));
        return new HttpClientRequestAdapter(authProvider) { BaseUrl = baseUrl };
    }

    public Task DisposeAsync() => Task.CompletedTask;
}

[CollectionDefinition("Osdu")]
public class OsduCollection : ICollectionFixture<OsduFixture>;
