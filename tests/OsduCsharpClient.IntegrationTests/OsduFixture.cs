using DotNetEnv;
using Equinor.OsduCsharpClient.Facade;
using Xunit;

namespace OsduCsharpClient.IntegrationTests;

/// <summary>
/// xUnit collection fixture: exposes a shared <see cref="OsduClient"/> facade
/// pre-configured for every OSDU service. All integration tests use this facade.
/// </summary>
public class OsduFixture : IAsyncLifetime
{
    /// <summary>The shared facade client used by every integration test.</summary>
    public OsduClient Client { get; private set; } = null!;

    public ValueTask InitializeAsync()
    {
        Env.NoClobber().TraversePath().Load();

        // The facade owns authentication: OsduClient defaults to interactive
        // MSAL and renews tokens silently from the MSAL cache.
        Client = new OsduClient(OsduConfig.FromEnvironment());
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        Client?.Dispose();
        return ValueTask.CompletedTask;
    }
}

[CollectionDefinition("Osdu")]
public class OsduCollection : ICollectionFixture<OsduFixture>;
