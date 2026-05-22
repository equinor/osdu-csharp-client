using Xunit;

namespace OsduCsharpClient.IntegrationTests;

/// <summary>
/// Base class for integration tests. Exposes the shared <see cref="OsduFixture"/>
/// and routes SDK request/response logging to the current test's
/// <see cref="ITestOutputHelper"/> so those logs appear per test.
/// </summary>
public abstract class OsduTestBase
{
    protected OsduFixture Fixture { get; }

    protected ITestOutputHelper Output { get; }

    protected OsduTestBase(OsduFixture fixture, ITestOutputHelper output)
    {
        Fixture = fixture;
        Output = output;
        fixture.RouteLogsTo(output);
    }
}
