using OsduCsharpClient.Entitlements;
using Xunit;


namespace OsduCsharpClient.IntegrationTests;

/// <summary>
/// Integration tests for the Entitlements service.
/// Mirrors entitlements_test.py.
/// </summary>
[Collection("Osdu")]
public class EntitlementsTests(OsduFixture fixture, ITestOutputHelper output)
{
    private EntitlementsClient CreateClient() =>
        new(fixture.CreateAdapter(fixture.Config.EntitlementsUrl));

    private string DataPartitionId => fixture.Config.DataPartitionId;

    [Fact]
    public async Task ListAllEntitlementGroups_ReturnsGroups()
    {
        var groupType = Environment.GetEnvironmentVariable("GROUP_TYPE") ?? "NONE";

        var client = CreateClient();
        var result = await client.Groups.All.GetAsync(
            config =>
            {
                config.QueryParameters.Type = groupType;
                config.Headers.Add("data-partition-id", DataPartitionId);
            });

        Assert.NotNull(result);
        Assert.NotNull(result.Groups);
        foreach (var group in result.Groups)
            output.WriteLine($"{group.Name} - {group.Email}");
    }
}
