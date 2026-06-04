using System.Text.Json;
using System.Text.Json.Nodes;
using Equinor.OsduCsharpClient.Facade;
using Microsoft.Kiota.Abstractions.Serialization;
using Xunit;

namespace OsduCsharpClient.Tests;

public class UntypedNodeJsonExtensionsTests
{
    private const string WellLogDataJson = """
        {
          "BottomMeasuredDepth": 13856.2,
          "IsRegular": true,
          "Name": "GR Log",
          "Curves": [
            { "Mnemonic": "GR", "NumberOfColumns": 1 },
            { "Mnemonic": "RHOB" }
          ],
          "VerticalMeasurement": { "VerticalMeasurement": 2680.5 },
          "Optional": null
        }
        """;

    [Fact]
    public void ToUntypedNode_NullJsonNode_ReturnsUntypedNull()
    {
        JsonNode? node = null;
        Assert.IsType<UntypedNull>(node.ToUntypedNode());
    }

    [Fact]
    public void ToJsonNode_NullOrUntypedNull_ReturnsNull()
    {
        Assert.Null(((UntypedNode?)null).ToJsonNode());
        Assert.Null(new UntypedNull().ToJsonNode());
    }

    [Fact]
    public void RoundTrip_JsonNode_PreservesStructureAndValues()
    {
        var original = JsonNode.Parse(WellLogDataJson)!;

        var roundTripped = original.ToUntypedNode().ToJsonNode();

        Assert.NotNull(roundTripped);
        Assert.True(
            JsonNode.DeepEquals(original, roundTripped),
            $"Expected:\n{original.ToJsonString()}\n\nActual:\n{roundTripped!.ToJsonString()}");
    }

    [Fact]
    public void ToUntypedNode_MapsJsonPrimitivesToTypedNodes()
    {
        var obj = (UntypedObject)JsonNode.Parse(WellLogDataJson)!.ToUntypedNode();
        var members = obj.GetValue();

        Assert.IsType<UntypedDecimal>(members["BottomMeasuredDepth"]);
        Assert.IsType<UntypedBoolean>(members["IsRegular"]);
        Assert.IsType<UntypedString>(members["Name"]);
        Assert.IsType<UntypedArray>(members["Curves"]);
        Assert.IsType<UntypedObject>(members["VerticalMeasurement"]);
        Assert.IsType<UntypedNull>(members["Optional"]);

        var firstCurve = (UntypedObject)((UntypedArray)members["Curves"]).GetValue().First();
        Assert.IsType<UntypedLong>(firstCurve.GetValue()["NumberOfColumns"]);
    }

    [Fact]
    public void RoundTrip_Poco_PreservesValues()
    {
        var options = new JsonSerializerOptions { PropertyNamingPolicy = null };
        var curve = new Curve { Mnemonic = "GR", NumberOfColumns = 1 };

        var node = curve.ToUntypedNode(options);
        var result = node.Deserialize<Curve>(options);

        Assert.NotNull(result);
        Assert.Equal("GR", result!.Mnemonic);
        Assert.Equal(1, result.NumberOfColumns);
    }

    [Fact]
    public void Deserialize_UntypedNull_ReturnsDefault()
    {
        Assert.Null(new UntypedNull().Deserialize<Curve>());
    }

    private sealed class Curve
    {
        public string? Mnemonic { get; set; }
        public int NumberOfColumns { get; set; }
    }
}
