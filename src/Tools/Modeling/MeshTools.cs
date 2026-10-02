using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Snail.MCP.Blender.Application.Skills;
using Snail.MCP.Blender.Tools.Prompts;

namespace Snail.MCP.Blender.Tools.Modeling;

/// <summary>Mesh editing through bmesh: every operation works on the mesh's stored selection, so select first, then edit.</summary>
[McpServerToolType]
[Skill(Skills.Modeling, SkillDescriptions.Modeling)]
public sealed class MeshTools(IBlenderBridge bridge) : BridgeToolBase(bridge)
{
    [McpServerTool(Name = "blender_mesh_select", Destructive = false, OpenWorld = false)]
    [Description(ToolDescriptions.Mesh.Select)]
    public Task<CallToolResult> SelectAsync(
        [Description(ToolDescriptions.Parameters.MeshName)] string name,
        [Description("all, none, invert, by_index, by_normal, by_position or by_material.")] string mode = "all",
        [Description("by_index: vertex indices.")] int[]? vertices = null,
        [Description("by_index: edge indices.")] int[]? edges = null,
        [Description("by_index: face indices.")] int[]? faces = null,
        [Description("by_normal: facing direction, one of +x -x +y -y +z -z. by_position: x, y or z.")] string? axis = null,
        [Description("by_normal: minimum dot product with the axis, 1 is exactly facing; default 0.5.")] double? threshold = null,
        [Description("by_position: lowest coordinate along the axis, in the object's local space.")] double? min = null,
        [Description("by_position: highest coordinate along the axis.")] double? max = null,
        [Description("by_material: material slot index.")] int? materialIndex = null,
        [Description("Add to the current selection instead of replacing it.")] bool extend = false,
        CancellationToken cancellationToken = default) =>
        SendAsync(BridgeCommands.MeshSelect,
            new JsonObject().With("name", name).With("mode", mode).With("vertices", Indices(vertices)).With("edges", Indices(edges))
                .With("faces", Indices(faces)).With("axis", axis).With("threshold", threshold).With("min", min).With("max", max)
                .With("material_index", materialIndex).With("extend", extend),
            cancellationToken);

    [McpServerTool(Name = "blender_mesh_extrude", Destructive = false, OpenWorld = false)]
    [Description(ToolDescriptions.Mesh.Extrude)]
    public Task<CallToolResult> ExtrudeAsync(
        [Description(ToolDescriptions.Parameters.MeshName)] string name,
        [Description("How far to push the selected faces along their normal, in metres; negative pushes inward.")] double distance = 1,
        [Description("Extrude each face along its own normal instead of the region as a whole.")] bool individual = false,
        CancellationToken cancellationToken = default) =>
        SendAsync(BridgeCommands.MeshExtrude,
            new JsonObject().With("name", name).With("distance", distance).With("individual", individual), cancellationToken);

    [McpServerTool(Name = "blender_mesh_inset", Destructive = false, OpenWorld = false)]
    [Description(ToolDescriptions.Mesh.Inset)]
    public Task<CallToolResult> InsetAsync(
        [Description(ToolDescriptions.Parameters.MeshName)] string name,
        [Description("Inset width in metres.")] double thickness = 0.1,
        [Description("Push the inset face along the normal, in metres.")] double depth = 0,
        [Description("Inset each face separately instead of the region.")] bool individual = false,
        CancellationToken cancellationToken = default) =>
        SendAsync(BridgeCommands.MeshInset,
            new JsonObject().With("name", name).With("thickness", thickness).With("depth", depth).With("individual", individual), cancellationToken);

    [McpServerTool(Name = "blender_mesh_bevel", Destructive = false, OpenWorld = false)]
    [Description(ToolDescriptions.Mesh.Bevel)]
    public Task<CallToolResult> BevelAsync(
        [Description(ToolDescriptions.Parameters.MeshName)] string name,
        [Description("Bevel width in metres.")] double width = 0.1,
        [Description("Segments per bevelled edge; more is rounder.")] int segments = 1,
        [Description("Profile shape 0 to 1; 0.5 is a circular arc.")] double profile = 0.5,
        [Description("Bevel the selected vertices instead of the selected edges.")] bool verticesOnly = false,
        CancellationToken cancellationToken = default) =>
        SendAsync(BridgeCommands.MeshBevel,
            new JsonObject().With("name", name).With("width", width).With("segments", segments).With("profile", profile).With("vertices_only", verticesOnly),
            cancellationToken);

    [McpServerTool(Name = "blender_mesh_subdivide", Destructive = false, OpenWorld = false)]
    [Description(ToolDescriptions.Mesh.Subdivide)]
    public Task<CallToolResult> SubdivideAsync(
        [Description(ToolDescriptions.Parameters.MeshName)] string name,
        [Description("Cuts per edge.")] int cuts = 1,
        [Description("Smoothness of the new geometry, 0 keeps it flat.")] double smooth = 0,
        CancellationToken cancellationToken = default) =>
        SendAsync(BridgeCommands.MeshSubdivide, new JsonObject().With("name", name).With("cuts", cuts).With("smooth", smooth), cancellationToken);

    [McpServerTool(Name = "blender_mesh_merge", Destructive = true, OpenWorld = false)]
    [Description(ToolDescriptions.Mesh.Merge)]
    public Task<CallToolResult> MergeAsync(
        [Description(ToolDescriptions.Parameters.MeshName)] string name,
        [Description("Vertices closer than this, in metres, become one.")] double distance = 0.0001,
        CancellationToken cancellationToken = default) =>
        SendAsync(BridgeCommands.MeshMerge, new JsonObject().With("name", name).With("distance", distance), cancellationToken);

    [McpServerTool(Name = "blender_mesh_normals", Destructive = false, OpenWorld = false)]
    [Description(ToolDescriptions.Mesh.Normals)]
    public Task<CallToolResult> NormalsAsync(
        [Description(ToolDescriptions.Parameters.MeshName)] string name,
        [Description("Point the normals inward instead of outward.")] bool inside = false,
        CancellationToken cancellationToken = default) =>
        SendAsync(BridgeCommands.MeshNormals, new JsonObject().With("name", name).With("inside", inside), cancellationToken);

    [McpServerTool(Name = "blender_mesh_shade", Destructive = false, OpenWorld = false)]
    [Description(ToolDescriptions.Mesh.Shade)]
    public Task<CallToolResult> ShadeAsync(
        [Description(ToolDescriptions.Parameters.MeshName)] string name,
        [Description("Smooth shading; false restores flat shading.")] bool smooth = true,
        [Description("Keep edges sharper than this angle in degrees flat (Blender's Shade Auto Smooth).")] double? angle = null,
        CancellationToken cancellationToken = default) =>
        SendAsync(BridgeCommands.MeshShade, new JsonObject().With("name", name).With("smooth", smooth).With("angle", angle), cancellationToken);

    [McpServerTool(Name = "blender_mesh_geometry", ReadOnly = true, OpenWorld = false)]
    [Description(ToolDescriptions.Mesh.Geometry)]
    public Task<CallToolResult> GeometryAsync(
        [Description(ToolDescriptions.Parameters.MeshName)] string name,
        [Description("At most this many vertices and faces; the counts report the whole mesh.")] int limit = 500,
        [Description("Only the selected vertices and faces.")] bool selectedOnly = false,
        [Description("Coordinates in world space instead of the object's local space.")] bool world = false,
        CancellationToken cancellationToken = default) =>
        SendAsync(BridgeCommands.MeshGeometry,
            new JsonObject().With("name", name).With("limit", limit).With("selected_only", selectedOnly).With("world", world), cancellationToken);

    [McpServerTool(Name = "blender_mesh_set_vertices", Destructive = false, OpenWorld = false)]
    [Description(ToolDescriptions.Mesh.SetVertices)]
    public Task<CallToolResult> SetVerticesAsync(
        [Description(ToolDescriptions.Parameters.MeshName)] string name,
        [Description("[[index, x, y, z], ...] in the object's local space.")] double[][] vertices,
        [Description("Treat x, y, z as offsets from the current position instead of new positions.")] bool relative = false,
        CancellationToken cancellationToken = default) =>
        SendAsync(BridgeCommands.MeshSetVertices,
            new JsonObject().With("name", name).With("vertices", Nested(vertices)).With("relative", relative), cancellationToken);

    private static JsonNode Nested(double[][] rows) =>
        new JsonArray([.. rows.Select(row => (JsonNode)new JsonArray([.. row.Select(value => (JsonNode)value)]))]);

    private static JsonNode? Indices(int[]? indices) =>
        indices is null ? null : new JsonArray([.. indices.Select(index => (JsonNode)index)]);
}
