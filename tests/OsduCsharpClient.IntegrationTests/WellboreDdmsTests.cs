using System.Text.Json.Nodes;
using Equinor.OsduCsharpClient.Facade;
using Xunit;
using Models = Equinor.OsduCsharpClient.WellboreDdms.Models;

namespace OsduCsharpClient.IntegrationTests;

/// <summary>
/// Integration tests for the Wellbore DDMS service. All tests use the shared
/// <see cref="OsduFixture.Client"/> facade, which injects auth and the
/// data-partition-id header.
/// </summary>
[Collection("Osdu")]
public class WellboreDdmsTests(OsduFixture fixture, ITestOutputHelper output)
    : OsduTestBase(fixture, output)
{
    [Fact]
    public async Task GetAbout_ReturnsServiceInfo()
    {
        var result = await Fixture.Client.WellboreDdms.About.GetAsync(
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Output.WriteLine($"Service:  {result.Service?.String}");
        Output.WriteLine($"Version:  {result.Version?.String}");
        Output.WriteLine($"Release:  {result.Release?.String}");
    }

    [Fact]
    public async Task GetWellbore_ById_ReturnsRecord()
    {
        var wellboreId = Environment.GetEnvironmentVariable("WELLBORE_DDMS_WELLBORE_ID");
        Assert.SkipWhen(string.IsNullOrEmpty(wellboreId),
            "WELLBORE_DDMS_WELLBORE_ID is not set.");

        var result = await Fixture.Client.WellboreDdms.Ddms.V3.Wellbores[wellboreId!].GetAsync(
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Output.WriteLine($"Kind: {result.Kind}");
    }

    [Fact]
    public async Task GetWell_ById_ReturnsRecord()
    {
        var wellId = Environment.GetEnvironmentVariable("WELLBORE_DDMS_WELL_ID");
        Assert.SkipWhen(string.IsNullOrEmpty(wellId), "WELLBORE_DDMS_WELL_ID is not set.");

        var result = await Fixture.Client.WellboreDdms.Ddms.V3.Wells[wellId!].GetAsync(
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Output.WriteLine($"Kind: {result.Kind}");
    }

    [Fact]
    public async Task GetWellLog_ById_ExposesDataAsJson()
    {
        var wellLogId = Environment.GetEnvironmentVariable("WELLBORE_DDMS_WELLLOG_ID");
        Assert.SkipWhen(string.IsNullOrEmpty(wellLogId),
            "WELLBORE_DDMS_WELLLOG_ID is not set.");

        // The facade OsduClient injects auth and the data-partition-id header.
        var result = await Fixture.Client.WellboreDdms.Ddms.V3.Welllogs[wellLogId!].GetAsync(
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotNull(result);

        // data is a free-form UntypedNode; the JSON bridge turns it into
        // ordinary System.Text.Json for inspection.
        JsonNode? data = result.Data.ToJsonNode();
        Assert.NotNull(data);
        Output.WriteLine($"Kind: {result.Kind}");
        Output.WriteLine($"Name: {(string?)data["Name"]}");
    }

    [Fact]
    public async Task PostWellLog_WithJsonData_CreatesAndRoundTripsRecord()
    {
        var legalTag = Environment.GetEnvironmentVariable("WELLBORE_DDMS_LEGAL_TAG");
        var aclOwner = Environment.GetEnvironmentVariable("WELLBORE_DDMS_ACL_OWNER");
        var aclViewer = Environment.GetEnvironmentVariable("WELLBORE_DDMS_ACL_VIEWER");
        var wellboreId = Environment.GetEnvironmentVariable("WELLBORE_DDMS_WELLBORE_ID");
        Assert.SkipWhen(
            string.IsNullOrEmpty(legalTag) || string.IsNullOrEmpty(aclOwner)
            || string.IsNullOrEmpty(aclViewer) || string.IsNullOrEmpty(wellboreId),
            "Set WELLBORE_DDMS_LEGAL_TAG, WELLBORE_DDMS_ACL_OWNER, "
            + "WELLBORE_DDMS_ACL_VIEWER and WELLBORE_DDMS_WELLBORE_ID to run this test.");

        var ct = TestContext.Current.CancellationToken;
        const string logName = "osdu-csharp-client integration-test WellLog";

        // The facade OsduClient injects auth and the data-partition-id header.
        var wellbore = Fixture.Client.WellboreDdms.Ddms.V3;

        // The WellLog `data` block is authored as plain JSON and bridged into the
        // record's free-form `Data` (an UntypedNode) via ToUntypedNode().
        var data = JsonNode.Parse($$"""
        {
          "Name": "{{logName}}",
          "WellboreID": "{{wellboreId}}",
          "TopMeasuredDepth": 12345.6,
          "BottomMeasuredDepth": 13856.2,
          "IsRegular": true,
          "Curves": [ { "CurveID": "GR_ID", "Mnemonic": "GR", "NumberOfColumns": 1 } ]
        }
        """)!;

        var record = new Models.Record
        {
            Kind = "osdu:wks:work-product-component--WellLog:1.2.0",
            Acl = new Models.StorageAcl { Owners = [aclOwner!], Viewers = [aclViewer!] },
            Legal = new Models.Legal
            {
                Legaltags = new Models.Legal.Legal_legaltags { String = [legalTag!] },
                OtherRelevantDataCountries =
                    new Models.Legal.Legal_otherRelevantDataCountries { String = ["US"] },
            },
            Data = data.ToUntypedNode(),
        };

        var response = await wellbore.Welllogs.PostAsync([record], cancellationToken: ct);

        var ids = response?.RecordIds?.String ?? [];
        Assert.NotEmpty(ids);
        var wellLogId = ids[0];
        Output.WriteLine($"Created WellLog: {wellLogId}");

        try
        {
            // Read it back: the data must round-trip through the JSON bridge.
            var created = await wellbore.Welllogs[wellLogId].GetAsync(cancellationToken: ct);

            Assert.NotNull(created);
            var roundTripped = created.Data.ToJsonNode();
            Assert.Equal(logName, (string?)roundTripped?["Name"]);
            Assert.Equal(wellboreId, (string?)roundTripped?["WellboreID"]);
        }
        finally
        {
            // Best-effort cleanup so the test does not leave records behind.
            try
            {
                await wellbore.Welllogs[wellLogId].DeleteAsync(cancellationToken: ct);
                Output.WriteLine($"Deleted WellLog: {wellLogId}");
            }
            catch (Exception ex)
            {
                Output.WriteLine($"Cleanup failed for {wellLogId}: {ex.Message}");
            }
        }
    }
}
