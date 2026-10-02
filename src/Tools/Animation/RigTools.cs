using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Snail.MCP.Blender.Application.Skills;
using Snail.MCP.Blender.Tools.Prompts;

namespace Snail.MCP.Blender.Tools.Animation;

/// <summary>Armatures, skinning and posing.</summary>
[McpServerToolType]
[Skill(Skills.Animation, SkillDescriptions.Animation)]
public sealed class RigTools(IBlenderBridge bridge) : BridgeToolBase(bridge)
{
    [McpServerTool(Name = "blender_add_armature", Destructive = false, OpenWorld = false)]
    [Description(ToolDescriptions.Rig.AddArmature)]
    public Task<CallToolResult> AddArmatureAsync(
        [Description("Name for the armature object.")] string? name = null,
        [Description("Bones as [{\"name\": \"Spine\", \"head\": [0, 0, 0], \"tail\": [0, 0, 1], \"parent\": \"Root\", \"connected\": true}, ...]; one default bone otherwise.")]
        JsonArray? bones = null,
        [Description(ToolDescriptions.Parameters.Location)] double[]? location = null,
        CancellationToken cancellationToken = default) =>
        SendAsync(BridgeCommands.AddArmature, new JsonObject().With("name", name).With("bones", bones).With("location", location), cancellationToken);

    [McpServerTool(Name = "blender_parent_to_armature", Destructive = false, OpenWorld = false)]
    [Description(ToolDescriptions.Rig.ParentToArmature)]
    public Task<CallToolResult> ParentToArmatureAsync(
        [Description(ToolDescriptions.Parameters.MeshName)] string name,
        [Description("Name of the armature object.")] string armature,
        [Description("automatic (weights from bone distance, the default), envelope, or empty (groups to fill by blender_set_vertex_weights).")]
        string method = "automatic",
        CancellationToken cancellationToken = default) =>
        SendAsync(BridgeCommands.ParentToArmature, new JsonObject().With("name", name).With("armature", armature).With("method", method), cancellationToken);

    [McpServerTool(Name = "blender_pose_bone", Destructive = false, OpenWorld = false)]
    [Description(ToolDescriptions.Rig.PoseBone)]
    public Task<CallToolResult> PoseBoneAsync(
        [Description("Name of the armature object.")] string armature,
        [Description("Bone name.")] string bone,
        [Description("[x, y, z] offset in the bone's local space.")] double[]? location = null,
        [Description(ToolDescriptions.Parameters.Rotation)] double[]? rotation = null,
        [Description(ToolDescriptions.Parameters.Scale)] double[]? scale = null,
        [Description("Key the changed channels on this frame.")] int? frame = null,
        CancellationToken cancellationToken = default) =>
        SendAsync(BridgeCommands.PoseBone,
            new JsonObject().With("armature", armature).With("bone", bone).With("location", location).With("rotation", rotation).With("scale", scale).With("frame", frame),
            cancellationToken);

    [McpServerTool(Name = "blender_set_vertex_weights", Destructive = false, OpenWorld = false)]
    [Description(ToolDescriptions.Rig.VertexWeights)]
    public Task<CallToolResult> VertexWeightsAsync(
        [Description(ToolDescriptions.Parameters.MeshName)] string name,
        [Description("Vertex group, created when missing; for skinning it is the bone's name.")] string group,
        [Description("[[vertexIndex, weight], ...] with weights 0 to 1.")] double[][]? weights = null,
        [Description("Give every vertex this weight.")] double? all = null,
        [Description("Give the mesh's selected vertices (blender_mesh_select) this weight.")] double? selected = null,
        [Description("REPLACE, ADD or SUBTRACT.")] string mode = "REPLACE",
        CancellationToken cancellationToken = default) =>
        SendAsync(BridgeCommands.SetVertexWeights,
            new JsonObject().With("name", name).With("group", group).With("weights", Pairs(weights)).With("all", all).With("selected", selected).With("mode", mode),
            cancellationToken);

    private static JsonNode? Pairs(double[][]? weights) =>
        weights is null ? null : new JsonArray([.. weights.Select(pair => (JsonNode)new JsonArray([.. pair.Select(value => (JsonNode)value)]))]);
}
