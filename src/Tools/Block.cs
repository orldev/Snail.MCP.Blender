using System.Text.Json;
using System.Text.Json.Serialization;

namespace Snail.MCP.Blender.Tools;

/// <summary>A nested block of settings a tool takes as one typed argument, so the model sees its fields in the schema instead of a bare object.</summary>
/// <remarks>Blocks travel to the add-on in snake_case with nulls left out, the shape the Python side reads; the schema the
/// client sees keeps the camelCase the SDK derives from the properties.</remarks>
public abstract record Block
{
    private static readonly JsonSerializerOptions Wire = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseUpper) },
    };

    /// <summary>The block as the add-on reads it.</summary>
    public JsonNode ToWire() => JsonSerializer.SerializeToNode(this, GetType(), Wire)!;
}
