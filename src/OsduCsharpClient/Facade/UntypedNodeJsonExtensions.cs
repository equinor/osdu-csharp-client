using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Kiota.Abstractions.Serialization;

namespace Equinor.OsduCsharpClient.Facade;

/// <summary>
/// Bridges Kiota's <see cref="UntypedNode"/> tree and <c>System.Text.Json</c>.
/// </summary>
/// <remarks>
/// OSDU records carry a free-form <c>data</c> payload that is polymorphic by
/// <c>kind</c> (a WellLog, a Wellbore, a Trajectory, ... all share the same
/// <c>Record</c> envelope). The generated clients expose that payload as an
/// <see cref="UntypedNode"/>, which round-trips arbitrary JSON but is awkward
/// to author by hand. These extensions let callers work with ordinary
/// <see cref="JsonNode"/> values — or their own POCOs — instead.
/// <code>
/// record.Data = myJsonNode.ToUntypedNode();      // authoring
/// JsonNode? data = result.Data.ToJsonNode();      // reading
/// </code>
/// </remarks>
public static class UntypedNodeJsonExtensions
{
    /// <summary>
    /// Converts a <see cref="JsonNode"/> (or <see langword="null"/>) into the
    /// equivalent Kiota <see cref="UntypedNode"/> tree.
    /// </summary>
    /// <param name="node">The JSON value to convert.</param>
    /// <returns>An <see cref="UntypedNode"/>; <see cref="UntypedNull"/> for <see langword="null"/>.</returns>
    public static UntypedNode ToUntypedNode(this JsonNode? node)
    {
        switch (node)
        {
            case null:
                return new UntypedNull();
            case JsonObject obj:
                var members = new Dictionary<string, UntypedNode>(obj.Count);
                foreach (var pair in obj)
                {
                    members[pair.Key] = pair.Value.ToUntypedNode();
                }
                return new UntypedObject(members);
            case JsonArray array:
                var items = new List<UntypedNode>(array.Count);
                foreach (var item in array)
                {
                    items.Add(item.ToUntypedNode());
                }
                return new UntypedArray(items);
            case JsonValue value:
                return ConvertJsonValue(value);
            default:
                return new UntypedNull();
        }
    }

    /// <summary>
    /// Converts a Kiota <see cref="UntypedNode"/> (or <see langword="null"/>)
    /// into the equivalent <see cref="JsonNode"/>.
    /// </summary>
    /// <param name="node">The untyped node to convert.</param>
    /// <returns>A <see cref="JsonNode"/>, or <see langword="null"/> for a JSON null.</returns>
    public static JsonNode? ToJsonNode(this UntypedNode? node)
    {
        switch (node)
        {
            case null:
            case UntypedNull:
                return null;
            case UntypedObject obj:
                var jsonObject = new JsonObject();
                foreach (var pair in obj.GetValue())
                {
                    jsonObject[pair.Key] = pair.Value.ToJsonNode();
                }
                return jsonObject;
            case UntypedArray array:
                var jsonArray = new JsonArray();
                foreach (var item in array.GetValue())
                {
                    jsonArray.Add(item.ToJsonNode());
                }
                return jsonArray;
            case UntypedString s:
                return s.GetValue() is { } str ? JsonValue.Create(str) : null;
            case UntypedBoolean b:
                return JsonValue.Create(b.GetValue());
            case UntypedInteger i:
                return JsonValue.Create(i.GetValue());
            case UntypedLong l:
                return JsonValue.Create(l.GetValue());
            case UntypedDecimal m:
                return JsonValue.Create(m.GetValue());
            case UntypedDouble d:
                return JsonValue.Create(d.GetValue());
            case UntypedFloat f:
                return JsonValue.Create(f.GetValue());
            default:
                return JsonSerializer.SerializeToNode(node.GetValue());
        }
    }

    /// <summary>
    /// Serializes an arbitrary value to JSON and returns it as an
    /// <see cref="UntypedNode"/>. Convenient for assigning a typed POCO to a
    /// free-form OSDU <c>data</c> property.
    /// </summary>
    /// <typeparam name="T">The type of the value to convert.</typeparam>
    /// <param name="value">The value to serialize.</param>
    /// <param name="options">Optional serializer options.</param>
    /// <returns>The value as an <see cref="UntypedNode"/> tree.</returns>
    public static UntypedNode ToUntypedNode<T>(this T value, JsonSerializerOptions? options = null) =>
        JsonSerializer.SerializeToNode(value, options).ToUntypedNode();

    /// <summary>
    /// Deserializes an <see cref="UntypedNode"/> into a typed value via
    /// <c>System.Text.Json</c>.
    /// </summary>
    /// <typeparam name="T">The target type.</typeparam>
    /// <param name="node">The untyped node to deserialize.</param>
    /// <param name="options">Optional serializer options.</param>
    /// <returns>The deserialized value, or <see langword="default"/> for a JSON null.</returns>
    public static T? Deserialize<T>(this UntypedNode? node, JsonSerializerOptions? options = null)
    {
        var json = node.ToJsonNode();
        return json is null ? default : json.Deserialize<T>(options);
    }

    private static UntypedNode ConvertJsonValue(JsonValue value)
    {
        switch (value.GetValueKind())
        {
            case JsonValueKind.String:
                return new UntypedString(value.GetValue<string>());
            case JsonValueKind.True:
                return new UntypedBoolean(true);
            case JsonValueKind.False:
                return new UntypedBoolean(false);
            case JsonValueKind.Number:
                if (value.TryGetValue<long>(out var longValue))
                {
                    return new UntypedLong(longValue);
                }
                if (value.TryGetValue<decimal>(out var decimalValue))
                {
                    return new UntypedDecimal(decimalValue);
                }
                return new UntypedDouble(value.GetValue<double>());
            default:
                return new UntypedNull();
        }
    }
}
