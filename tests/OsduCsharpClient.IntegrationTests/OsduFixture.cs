using Equinor.OsduCsharpClient.Facade;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace OsduCsharpClient.IntegrationTests;

/// <summary>
/// xUnit collection fixture: exposes a shared <see cref="OsduClient"/> facade
/// pre-configured for every OSDU service, with SDK logging routed to the
/// running test's output. All integration tests use this facade.
/// </summary>
public class OsduFixture : IAsyncLifetime
{
    private readonly TestOutputLoggerFactory _loggerFactory = new();

    /// <summary>The shared facade client used by every integration test.</summary>
    public OsduClient Client { get; private set; } = null!;

    /// <summary>
    /// Routes SDK request/response logs to the given test's output.
    /// Called by <see cref="OsduTestBase"/> for each test.
    /// </summary>
    public void RouteLogsTo(ITestOutputHelper output) => _loggerFactory.SetOutput(output);

    public ValueTask InitializeAsync()
    {
        // Standard .NET configuration: appsettings.json (committed template),
        // appsettings.local.json (gitignored, real values), user secrets, and
        // environment variables (e.g. Osdu__Server). See docs/environment-and-tests.md.
        var configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.local.json", optional: true)
            .AddUserSecrets<OsduFixture>(optional: true)
            .AddEnvironmentVariables()
            .Build();

        // The facade owns authentication (interactive MSAL, silent renewal).
        // Passing a logger factory enables HTTP request/response logging — see
        // the log categories documented in docs/usage.md.
        Client = new OsduClient(OsduConfig.FromConfiguration(configuration), loggerFactory: _loggerFactory);
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        Client?.Dispose();
        _loggerFactory.Dispose();
        return ValueTask.CompletedTask;
    }
}

[CollectionDefinition("Osdu")]
public class OsduCollection : ICollectionFixture<OsduFixture>;
