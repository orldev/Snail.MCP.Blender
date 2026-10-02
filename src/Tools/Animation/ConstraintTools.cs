using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Snail.MCP.Blender.Application.Skills;
using Snail.MCP.Blender.Tools.Prompts;

namespace Snail.MCP.Blender.Tools.Animation;

/// <summary>Constraints on objects and bones, driven by their Python property names.</summary>
[McpServerToolType]
[Skill(Skills.Animation, SkillDescriptions.Animation)]
public sealed class ConstraintTools(IBlenderBridge bridge) : BridgeToolBase(bridge)
{
    [McpServerTool(Name = "blender_add_constraint", Destructive = false, OpenWorld = false)]
    [Description(ToolDescriptions.Constraints.Add)]
    public Task<CallToolResult> AddAsync(
        [Description(ToolDescriptions.Parameters.ObjectName)] string name,
        [Description("TRACK_TO, DAMPED_TRACK, COPY_LOCATION, COPY_ROTATION, COPY_TRANSFORMS, LIMIT_LOCATION, LIMIT_ROTATION, LIMIT_DISTANCE, CHILD_OF, FOLLOW_PATH, IK, STRETCH_TO, SHRINKWRAP and the rest; blender_describe_constraint lists them.")]
        string type,
        [Description(ToolDescriptions.Parameters.ConstraintSettings)] JsonObject? settings = null,
        [Description("Bone of an armature to constrain instead of the object (IK, copy rotation between bones).")] string? bone = null,
        [Description("Name for the constraint.")] string? constraintName = null,
        [Description("Strength 0 to 1.")] double? influence = null,
        CancellationToken cancellationToken = default) =>
        SendAsync(BridgeCommands.AddConstraint,
            new JsonObject().With("name", name).With("type", type).With("settings", settings).With("bone", bone).With("constraint_name", constraintName).With("influence", influence),
            cancellationToken);

    [McpServerTool(Name = "blender_update_constraint", Destructive = false, OpenWorld = false)]
    [Description(ToolDescriptions.Constraints.Update)]
    public Task<CallToolResult> UpdateAsync(
        [Description(ToolDescriptions.Parameters.ObjectName)] string name,
        [Description("Constraint name as blender_object_info lists it.")] string constraint,
        [Description(ToolDescriptions.Parameters.ConstraintSettings)] JsonObject? settings = null,
        [Description("Bone that owns the constraint, for bone constraints.")] string? bone = null,
        [Description("Strength 0 to 1.")] double? influence = null,
        [Description("Switch it on or off without removing it.")] bool? enabled = null,
        [Description("New name.")] string? newName = null,
        CancellationToken cancellationToken = default) =>
        SendAsync(BridgeCommands.UpdateConstraint,
            new JsonObject().With("name", name).With("constraint", constraint).With("settings", settings).With("bone", bone).With("influence", influence).With("enabled", enabled).With("new_name", newName),
            cancellationToken);

    [McpServerTool(Name = "blender_remove_constraint", Destructive = true, OpenWorld = false)]
    [Description(ToolDescriptions.Constraints.Remove)]
    public Task<CallToolResult> RemoveAsync(
        [Description(ToolDescriptions.Parameters.ObjectName)] string name,
        [Description("Constraint name.")] string constraint,
        [Description("Bone that owns the constraint, for bone constraints.")] string? bone = null,
        CancellationToken cancellationToken = default) =>
        SendAsync(BridgeCommands.RemoveConstraint, new JsonObject().With("name", name).With("constraint", constraint).With("bone", bone), cancellationToken);

    [McpServerTool(Name = "blender_describe_constraint", ReadOnly = true, OpenWorld = false)]
    [Description(ToolDescriptions.Constraints.Describe)]
    public Task<CallToolResult> DescribeAsync(
        [Description("Constraint type to document; empty lists every type.")] string? type = null,
        CancellationToken cancellationToken = default) =>
        SendAsync(BridgeCommands.DescribeConstraint, new JsonObject().With("type", type), cancellationToken);
}
