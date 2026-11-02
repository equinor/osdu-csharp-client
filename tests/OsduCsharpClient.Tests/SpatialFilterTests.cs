using System.Text.Json;
using System.Text.Json.Nodes;
using Equinor.OsduCsharpClient.Search.Models;
using Microsoft.Kiota.Abstractions.Serialization;
using Microsoft.Kiota.Serialization.Json;
using Xunit;

namespace OsduCsharpClient.Tests;

/// <summary>
/// Search's <c>spatialFilter</c> is a <c>oneOf</c> of objects with no discriminator upstream.
/// Generated as is, Kiota's wrapper read no member and wrote the property with no value, so
/// no request carrying a spatial filter could be serialised. The generation step merges the
/// members into one <see cref="SpatialFilter"/>; these hold it to round-tripping.
/// </summary>
public class SpatialFilterTests
{
    [Theory]
    [InlineData("""{ "field": "data.SpatialLocation", "byBoundingBox": { "topLeft": { "latitude": 60.5, "longitude": 2.1 }, "bottomRight": { "latitude": 58.2, "longitude": 4.3 } } }""")]
    [InlineData("""{ "field": "data.SpatialLocation", "byDistance": { "point": { "latitude": 60.5, "longitude": 2.1 }, "distance": 1500 } }""")]
    [InlineData("""{ "field": "data.SpatialLocation", "byGeoPolygon": { "points": [ { "latitude": 26.1, "longitude": -112.2 }, { "latitude": 26.5, "longitude": -68.4 }, { "latitude": 52.2, "longitude": -93.5 } ] } }""")]
    public void QueryRequest_SpatialFilter_RoundTrips(string filter)
    {
        var json = $$"""{ "kind": "*:*:*:*", "spatialFilter": {{filter}} }""";

        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(filter), RoundTrip(json, QueryRequest.CreateFromDiscriminatorValue)["spatialFilter"]));
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(filter), RoundTrip(json, CursorQueryRequest.CreateFromDiscriminatorValue)["spatialFilter"]));
    }

    private static JsonNode RoundTrip<T>(string json, ParsableFactory<T> factory) where T : IParsable
    {
        using var document = JsonDocument.Parse(json);
        var model = new JsonParseNode(document.RootElement).GetObjectValue(factory)!;

        using var writer = new JsonSerializationWriter();
        writer.WriteObjectValue(string.Empty, model);
        using var stream = writer.GetSerializedContent();
        return JsonNode.Parse(stream)!;
    }
}
