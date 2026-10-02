using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Snail.MCP.Blender.Application.Skills;
using Snail.MCP.Blender.Tools.Prompts;

namespace Snail.MCP.Blender.Tools.Animation;

/// <summary>Shape keys: blend shapes for morphing and facial animation.</summary>
[McpServerToolType]
[Skill(Skills.Animation, SkillDescriptions.Animation)]
public sealed class ShapeKeyTools(IBlenderBridge bridge) : BridgeToolBase(bridge)
{
    [McpServerTool(Name = "blender_add_shape_key", Destructive = false, OpenWorld = false)]
    [Description(ToolDescriptions.ShapeKeys.Add)]
    public Task<CallToolResult> AddAsync(
        [Description(ToolDescriptions.Parameters.ObjectName)] string name,
        [Description("Name of the new key; a Basis key is created first when there is none.")] string keyName = "Key",
        [Description("[[index, x, y, z], ...] vertex offsets that define the shape; positions when relative=false.")] double[][]? vertices = null,
        [Description("Treat x, y, z as offsets from the basis (the default) or as positions.")] bool relative = true,
        [Description("Start from the current mix of keys instead of the basis.")] bool fromMix = false,
        [Description("Initial value 0 to 1.")] double? value = null,
        CancellationToken cancellationToken = default) =>
        SendAsync(BridgeCommands.AddShapeKey,
            new JsonObject().With("name", name).With("key_name", keyName).With("vertices", Nested(vertices)).With("relative", relative).With("from_mix", fromMix).With("value", value),
            cancellationToken);

    [McpServerTool(Name = "blender_set_shape_key", Destructive = false, OpenWorld = false)]
    [Description(ToolDescriptions.ShapeKeys.Set)]
    public Task<CallToolResult> SetAsync(
        [Description(ToolDescriptions.Parameters.ObjectName)] string name,
        [Description("Shape key name.")] string keyName,
        [Description("Blend value; 0 is off, 1 is the full shape.")] double? value = null,
        [Description("Key the value on this frame.")] int? frame = null,
        [Description("Lowest allowed value.")] double? sliderMin = null,
        [Description("Highest allowed value; above 1 overshoots.")] double? sliderMax = null,
        CancellationToken cancellationToken = default) =>
        SendAsync(BridgeCommands.SetShapeKey,
            new JsonObject().With("name", name).With("key_name", keyName).With("value", value).With("frame", frame).With("slider_min", sliderMin).With("slider_max", sliderMax),
            cancellationToken);

    private static JsonNode? Nested(double[][]? rows) =>
        rows is null ? null : new JsonArray([.. rows.Select(row => (JsonNode)new JsonArray([.. row.Select(value => (JsonNode)value)]))]);
}
