using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Snail.MCP.Blender.Application.Skills;
using Snail.MCP.Blender.Tools.Prompts;

namespace Snail.MCP.Blender.Tools.Modeling;

/// <summary>Whole-object geometry: joining, origins and baking transforms into the mesh.</summary>
[McpServerToolType]
[Skill(Skills.Modeling, SkillDescriptions.Modeling)]
public sealed class GeometryTools(IBlenderBridge bridge) : BridgeToolBase(bridge)
{
    [McpServerTool(Name = "blender_join_objects", Destructive = true, OpenWorld = false)]
    [Description(ToolDescriptions.Geometry.Join)]
    public Task<CallToolResult> JoinAsync(
        [Description("Names of the objects to join; at least two.")] string[] names,
        [Description("The object that survives and receives the others; the first name otherwise.")] string? target = null,
        CancellationToken cancellationToken = default) =>
        SendAsync(BridgeCommands.JoinObjects, new JsonObject().With("names", names).With("target", target), cancellationToken);

    [McpServerTool(Name = "blender_set_origin", Destructive = false, OpenWorld = false)]
    [Description(ToolDescriptions.Geometry.SetOrigin)]
    public Task<CallToolResult> SetOriginAsync(
        [Description(ToolDescriptions.Parameters.ObjectName)] string name,
        [Description("ORIGIN_GEOMETRY (origin to geometry, the default), GEOMETRY_ORIGIN (geometry to origin), ORIGIN_CURSOR, ORIGIN_CENTER_OF_MASS, ORIGIN_CENTER_OF_VOLUME.")]
        string type = "ORIGIN_GEOMETRY",
        [Description("MEDIAN or BOUNDS: how the geometry centre is found.")] string center = "MEDIAN",
        CancellationToken cancellationToken = default) =>
        SendAsync(BridgeCommands.SetOrigin, new JsonObject().With("name", name).With("type", type).With("center", center), cancellationToken);

    [McpServerTool(Name = "blender_apply_transforms", Destructive = true, OpenWorld = false)]
    [Description(ToolDescriptions.Geometry.ApplyTransforms)]
    public Task<CallToolResult> ApplyTransformsAsync(
        [Description(ToolDescriptions.Parameters.ObjectName)] string name,
        [Description("Bake the location.")] bool location = true,
        [Description("Bake the rotation.")] bool rotation = true,
        [Description("Bake the scale.")] bool scale = true,
        CancellationToken cancellationToken = default) =>
        SendAsync(BridgeCommands.ApplyTransforms,
            new JsonObject().With("name", name).With("location", location).With("rotation", rotation).With("scale", scale), cancellationToken);
}
