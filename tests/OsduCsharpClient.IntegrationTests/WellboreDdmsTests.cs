using Equinor.OsduCsharpClient.WellboreDdms;
using Xunit;


namespace OsduCsharpClient.IntegrationTests;

/// <summary>
/// Integration tests for the Wellbore DDMS service.
/// </summary>
[Collection("Osdu")]
public class WellboreDdmsTests(OsduFixture fixture, ITestOutputHelper output)
{
    private WellboreDdmsClient CreateClient() =>
        new(fixture.CreateAdapter(fixture.Config.WellboreDdmsUrl));

    private string DataPartitionId => fixture.Config.DataPartitionId;

    [Fact]
    public async Task GetAbout_ReturnsServiceInfo()
    {
        var client = CreateClient();
        var result = await client.About.GetAsync(
            config => config.Headers.Add("data-partition-id", DataPartitionId));

        Assert.NotNull(result);
        output.WriteLine($"Service:  {result.Service?.String}");
        output.WriteLine($"Version:  {result.Version?.String}");
        output.WriteLine($"Release:  {result.Release?.String}");
    }

    [Fact]
    public async Task GetWellbore_ById_ReturnsRecord()
    {
        var wellboreId = Environment.GetEnvironmentVariable("WELLBORE_DDMS_WELLBORE_ID");
        if (string.IsNullOrEmpty(wellboreId))
            return; // skip: set WELLBORE_DDMS_WELLBORE_ID to run this test

        var client = CreateClient();
        var result = await client.Ddms.V3.Wellbores[wellboreId].GetAsync(
            config => config.Headers.Add("data-partition-id", DataPartitionId));

        Assert.NotNull(result);
        output.WriteLine($"Kind: {result.Kind}");
    }

    [Fact]
    public async Task GetWell_ById_ReturnsRecord()
    {
        var wellId = Environment.GetEnvironmentVariable("WELLBORE_DDMS_WELL_ID");
        if (string.IsNullOrEmpty(wellId))
            return; // skip: set WELLBORE_DDMS_WELL_ID to run this test

        var client = CreateClient();
        var result = await client.Ddms.V3.Wells[wellId].GetAsync(
            config => config.Headers.Add("data-partition-id", DataPartitionId));

        Assert.NotNull(result);
        output.WriteLine($"Kind: {result.Kind}");
    }
}
