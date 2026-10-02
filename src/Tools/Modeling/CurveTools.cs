using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Snail.MCP.Blender.Application.Skills;
using Snail.MCP.Blender.Tools.Prompts;

namespace Snail.MCP.Blender.Tools.Modeling;

/// <summary>Curves: drawing them from points, giving them thickness, turning them into meshes.</summary>
[McpServerToolType]
[Skill(Skills.Modeling, SkillDescriptions.Modeling)]
public sealed class CurveTools(IBlenderBridge bridge) : BridgeToolBase(bridge)
{
    [McpServerTool(Name = "blender_add_curve", Destructive = false, OpenWorld = false)]
    [Description(ToolDescriptions.Curves.Add)]
    public Task<CallToolResult> AddAsync(
        [Description("bezier, nurbs or poly (drawn through points), circle or path (Blender's primitives).")] string kind = "bezier",
        [Description("Name for the curve object.")] string? name = null,
        [Description("Control points as [[x, y, z], ...] for bezier, nurbs and poly; at least two.")] double[][]? points = null,
        [Description("Close the curve into a loop.")] bool closed = false,
        [Description(ToolDescriptions.Parameters.Location)] double[]? location = null,
        [Description("Radius of a circle or length of a path.")] double? size = null,
        [Description(ToolDescriptions.Parameters.BevelDepth)] double? bevelDepth = null,
        [Description(ToolDescriptions.Parameters.CurveExtrude)] double? extrude = null,
        [Description(ToolDescriptions.Parameters.ResolutionU)] int? resolutionU = null,
        CancellationToken cancellationToken = default) =>
        SendAsync(BridgeCommands.AddCurve,
            new JsonObject().With("kind", kind).With("name", name).With("points", Points(points)).With("closed", closed).With("location", location)
                .With("size", size).With("bevel_depth", bevelDepth).With("extrude", extrude).With("resolution_u", resolutionU),
            cancellationToken);

    [McpServerTool(Name = "blender_set_curve", Destructive = false, OpenWorld = false)]
    [Description(ToolDescriptions.Curves.Set)]
    public Task<CallToolResult> SetAsync(
        [Description("Name of the curve object.")] string name,
        [Description(ToolDescriptions.Parameters.BevelDepth)] double? bevelDepth = null,
        [Description("Segments around a round bevel; more is smoother.")] int? bevelResolution = null,
        [Description(ToolDescriptions.Parameters.CurveExtrude)] double? extrude = null,
        [Description(ToolDescriptions.Parameters.ResolutionU)] int? resolutionU = null,
        [Description("How closed 2D curves fill: NONE, BACK, FRONT, BOTH (FULL, HALF, FRONT, BACK for 3D).")] string? fillMode = null,
        [Description("Curve object swept along this one as its cross-section; empty string removes it.")] string? bevelObject = null,
        [Description("Curve object scaling the thickness along the length; empty string removes it.")] string? taperObject = null,
        CancellationToken cancellationToken = default) =>
        SendAsync(BridgeCommands.SetCurve,
            new JsonObject().With("name", name).With("bevel_depth", bevelDepth).With("bevel_resolution", bevelResolution).With("extrude", extrude)
                .With("resolution_u", resolutionU).With("fill_mode", fillMode).With("bevel_object", bevelObject).With("taper_object", taperObject),
            cancellationToken);

    [McpServerTool(Name = "blender_convert_to_mesh", Destructive = true, OpenWorld = false)]
    [Description(ToolDescriptions.Curves.ConvertToMesh)]
    public Task<CallToolResult> ConvertToMeshAsync(
        [Description("Name of the curve, text or metaball object.")] string name,
        [Description("Keep the original and convert a copy.")] bool keepOriginal = false,
        CancellationToken cancellationToken = default) =>
        SendAsync(BridgeCommands.ConvertToMesh, new JsonObject().With("name", name).With("keep_original", keepOriginal), cancellationToken);

    private static JsonNode? Points(double[][]? points) =>
        points is null ? null : new JsonArray([.. points.Select(point => (JsonNode)new JsonArray([.. point.Select(value => (JsonNode)value)]))]);
}
