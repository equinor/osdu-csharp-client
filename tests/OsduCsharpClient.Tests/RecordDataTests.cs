using System.Text.Json.Nodes;
using Equinor.OsduCsharpClient.Facade;
using Microsoft.Kiota.Abstractions.Serialization;
using Microsoft.Kiota.Serialization.Json;
using Xunit;
using StorageRecord = Equinor.OsduCsharpClient.Storage.Models.Record;
using StorageMergePatch = Equinor.OsduCsharpClient.Storage.Models.RecordMergePatchRequest;
using DatasetRecord = Equinor.OsduCsharpClient.Dataset.Models.Record;
using WellboreRecord = Equinor.OsduCsharpClient.WellboreDdms.Models.Record;

namespace OsduCsharpClient.Tests;

/// <summary>
/// Regression coverage for https://github.com/equinor/osdu-csharp-client/issues/38.
///
/// OSDU services that store generic records (Storage, Dataset, Wellbore DDMS)
/// declare <c>Record.data</c> as a free-form object. The generation step
/// patches the spec so Kiota emits an <see cref="UntypedNode"/> for that
/// property — not an empty <c>Record_data</c> class — so callers can author
/// and read <c>data</c> as JSON.
/// </summary>
public class RecordDataTests
{
    private const string DataJson = """
        {
          "Name": "GR Log",
          "BottomMeasuredDepth": 13856.2,
          "IsRegular": true,
          "Curves": [ { "Mnemonic": "GR", "NumberOfColumns": 1 } ]
        }
        """;

    [Fact]
    public void StorageRecord_Data_IsUntypedNode()
    {
        var record = new StorageRecord { Data = JsonNode.Parse(DataJson).ToUntypedNode() };
        Assert.IsAssignableFrom<UntypedNode>(record.Data);
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(DataJson), record.Data.ToJsonNode()));
    }

    [Fact]
    public void DatasetRecord_Data_IsUntypedNode()
    {
        var record = new DatasetRecord { Data = JsonNode.Parse(DataJson).ToUntypedNode() };
        Assert.IsAssignableFrom<UntypedNode>(record.Data);
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(DataJson), record.Data.ToJsonNode()));
    }

    [Fact]
    public void WellboreRecord_Data_IsUntypedNode()
    {
        var record = new WellboreRecord { Data = JsonNode.Parse(DataJson).ToUntypedNode() };
        Assert.IsAssignableFrom<UntypedNode>(record.Data);
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(DataJson), record.Data.ToJsonNode()));
    }

    [Fact]
    public void StorageMergePatch_Data_IsUntypedNode()
    {
        // PATCH /records/{id} uses RecordMergePatchRequest; its `data` payload
        // is free-form too and must be authorable through the JSON bridge.
        var patch = new StorageMergePatch
        {
            Data = JsonNode.Parse("""{ "Name": "Updated", "Optional": null }""").ToUntypedNode(),
        };
        Assert.IsAssignableFrom<UntypedNode>(patch.Data);

        // JSON Merge Patch (RFC 7396) uses null to delete a key; UntypedNull
        // must round-trip as JSON null through Kiota's writer.
        using var writer = new JsonSerializationWriter();
        writer.WriteObjectValue(string.Empty, patch);
        using var stream = writer.GetSerializedContent();
        var json = JsonNode.Parse(stream)!;

        Assert.Equal("Updated", (string?)json["data"]!["Name"]);
        Assert.NotNull(json["data"]!.AsObject());
        Assert.True(json["data"]!.AsObject().ContainsKey("Optional"));
        Assert.Null(json["data"]!["Optional"]);
    }

    [Fact]
    public void Record_WithJsonData_SerializesThroughKiota()
    {
        var record = new StorageRecord
        {
            Kind = "osdu:wks:work-product-component--WellLog:1.2.0",
            Data = JsonNode.Parse(DataJson).ToUntypedNode(),
        };

        using var writer = new JsonSerializationWriter();
        writer.WriteObjectValue(string.Empty, record);
        using var stream = writer.GetSerializedContent();
        var roundTripped = JsonNode.Parse(stream)!;

        Assert.Equal(
            "osdu:wks:work-product-component--WellLog:1.2.0",
            (string?)roundTripped["kind"]);
        Assert.Equal("GR Log", (string?)roundTripped["data"]!["Name"]);
        Assert.Equal("GR", (string?)roundTripped["data"]!["Curves"]![0]!["Mnemonic"]);
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(DataJson), roundTripped["data"]));
    }
}
