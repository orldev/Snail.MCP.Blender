using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Snail.MCP.Blender.Application.Skills;
using Snail.MCP.Blender.Tools.Prompts;

namespace Snail.MCP.Blender.Tools.Animation;

/// <summary>Motion that is computed rather than keyed: paths and drivers.</summary>
[McpServerToolType]
[Skill(Skills.Animation, SkillDescriptions.Animation)]
public sealed class MotionTools(IBlenderBridge bridge) : BridgeToolBase(bridge)
{
    [McpServerTool(Name = "blender_follow_path", Destructive = false, OpenWorld = false)]
    [Description(ToolDescriptions.Motion.FollowPath)]
    public Task<CallToolResult> FollowPathAsync(
        [Description(ToolDescriptions.Parameters.ObjectName)] string name,
        [Description("Name of the curve object to travel along.")] string curve,
        [Description("Frame the travel starts on; the scene start otherwise.")] int? start = null,
        [Description("Frames the travel takes; up to the scene end otherwise.")] int? duration = null,
        [Description("Turn the object to face along the curve.")] bool follow = true,
        [Description("Axis that points along the curve: FORWARD_X, FORWARD_Y, FORWARD_Z, TRACK_NEGATIVE_X, TRACK_NEGATIVE_Y, TRACK_NEGATIVE_Z.")] string? forwardAxis = null,
        CancellationToken cancellationToken = default) =>
        SendAsync(BridgeCommands.FollowPath,
            new JsonObject().With("name", name).With("curve", curve).With("start", start).With("duration", duration).With("follow", follow).With("forward_axis", forwardAxis),
            cancellationToken);

    [McpServerTool(Name = "blender_add_driver", Destructive = false, OpenWorld = false)]
    [Description(ToolDescriptions.Motion.AddDriver)]
    public Task<CallToolResult> AddDriverAsync(
        [Description(ToolDescriptions.Parameters.ObjectName)] string name,
        [Description("Property to drive, e.g. location, rotation_euler, scale, data.energy, modifiers[\"Array\"].count.")] string dataPath,
        [Description("Component of a vector property: 0 for x, 1 for y, 2 for z; empty for scalars.")] int? index = null,
        [Description("Python expression over the variables and frame, e.g. sin(frame / 10) or dist * 2.")] string expression = "frame",
        [Description("Variables as [{\"name\": \"dist\", \"type\": \"SINGLE_PROP\", \"object\": \"Cube\", \"data_path\": \"location[0]\"}] or type TRANSFORMS with transform_type LOC_X, ROT_Z, SCALE_Y.")]
        JsonArray? variables = null,
        CancellationToken cancellationToken = default) =>
        SendAsync(BridgeCommands.AddDriver,
            new JsonObject().With("name", name).With("data_path", dataPath).With("index", index).With("expression", expression).With("variables", variables),
            cancellationToken);
}
