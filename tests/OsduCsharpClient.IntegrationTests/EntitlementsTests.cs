using Xunit;

namespace OsduCsharpClient.IntegrationTests;

/// <summary>
/// Integration tests for the Entitlements service.
/// </summary>
[Collection("Osdu")]
public class EntitlementsTests(OsduFixture fixture, ITestOutputHelper output)
{
    [Fact]
    public async Task ListAllEntitlementGroups_ReturnsGroups()
    {
        var groupType = Environment.GetEnvironmentVariable("GROUP_TYPE") ?? "NONE";

        // The facade OsduClient injects auth and the data-partition-id header.
        var result = await fixture.Client.Entitlements.Groups.All.GetAsync(
            config => config.QueryParameters.Type = groupType,
            TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.NotNull(result.Groups);
        foreach (var group in result.Groups)
            output.WriteLine($"{group.Name} - {group.Email}");
    }
}
