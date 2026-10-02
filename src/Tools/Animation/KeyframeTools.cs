using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Snail.MCP.Blender.Application.Skills;
using Snail.MCP.Blender.Tools.Prompts;

namespace Snail.MCP.Blender.Tools.Animation;

/// <summary>Keyframes and the curves between them.</summary>
[McpServerToolType]
[Skill(Skills.Animation, SkillDescriptions.Animation)]
public sealed class KeyframeTools(IBlenderBridge bridge) : BridgeToolBase(bridge)
{
    [McpServerTool(Name = "blender_set_frame", Destructive = false, OpenWorld = false)]
    [Description(ToolDescriptions.Keyframes.SetFrame)]
    public Task<CallToolResult> SetFrameAsync(
        [Description("Move the playhead to this frame.")] int? frame = null,
        [Description("First frame of the scene.")] int? start = null,
        [Description("Last frame of the scene.")] int? end = null,
        [Description("Frames per second.")] int? fps = null,
        CancellationToken cancellationToken = default) =>
        SendAsync(BridgeCommands.SetFrame, new JsonObject().With("frame", frame).With("start", start).With("end", end).With("fps", fps), cancellationToken);

    [McpServerTool(Name = "blender_insert_keyframe", Destructive = false, OpenWorld = false)]
    [Description(ToolDescriptions.Keyframes.Insert)]
    public Task<CallToolResult> InsertAsync(
        [Description(ToolDescriptions.Parameters.ObjectName)] string name,
        [Description("Frame to key; the current frame otherwise.")] int? frame = null,
        [Description(ToolDescriptions.Parameters.Channels)] string[]? channels = null,
        [Description("Values to set before keying, by channel: {\"location\": [0, 0, 2], \"rotation_euler\": [0, 0, 90]} (degrees), {\"data.energy\": 500}.")]
        JsonObject? values = null,
        [Description(ToolDescriptions.Parameters.Interpolation)] string? interpolation = null,
        CancellationToken cancellationToken = default) =>
        SendAsync(BridgeCommands.InsertKeyframe,
            new JsonObject().With("name", name).With("frame", frame).With("channels", channels).With("values", values).With("interpolation", interpolation),
            cancellationToken);

    [McpServerTool(Name = "blender_delete_keyframes", Destructive = true, OpenWorld = false)]
    [Description(ToolDescriptions.Keyframes.Delete)]
    public Task<CallToolResult> DeleteAsync(
        [Description(ToolDescriptions.Parameters.ObjectName)] string name,
        [Description("Only keys on this frame.")] int? frame = null,
        [Description(ToolDescriptions.Parameters.Channels)] string[]? channels = null,
        CancellationToken cancellationToken = default) =>
        SendAsync(BridgeCommands.DeleteKeyframes, new JsonObject().With("name", name).With("frame", frame).With("channels", channels), cancellationToken);

    [McpServerTool(Name = "blender_list_keyframes", ReadOnly = true, OpenWorld = false)]
    [Description(ToolDescriptions.Keyframes.List)]
    public Task<CallToolResult> ListAsync(
        [Description(ToolDescriptions.Parameters.ObjectName)] string name,
        [Description(ToolDescriptions.Parameters.Channels)] string[]? channels = null,
        CancellationToken cancellationToken = default) =>
        SendAsync(BridgeCommands.ListKeyframes, new JsonObject().With("name", name).With("channels", channels), cancellationToken);

    [McpServerTool(Name = "blender_set_interpolation", Destructive = false, OpenWorld = false)]
    [Description(ToolDescriptions.Keyframes.Interpolation)]
    public Task<CallToolResult> InterpolationAsync(
        [Description(ToolDescriptions.Parameters.ObjectName)] string name,
        [Description(ToolDescriptions.Parameters.Interpolation)] string interpolation = "BEZIER",
        [Description("For the eased kinds: EASE_IN, EASE_OUT, EASE_IN_OUT or AUTO.")] string? easing = null,
        [Description(ToolDescriptions.Parameters.Channels)] string[]? channels = null,
        [Description("Only keys from this frame on.")] int? fromFrame = null,
        [Description("Only keys up to this frame.")] int? toFrame = null,
        CancellationToken cancellationToken = default) =>
        SendAsync(BridgeCommands.SetInterpolation,
            new JsonObject().With("name", name).With("interpolation", interpolation).With("easing", easing).With("channels", channels).With("from_frame", fromFrame).With("to_frame", toFrame),
            cancellationToken);

    [McpServerTool(Name = "blender_move_keyframes", Destructive = false, OpenWorld = false)]
    [Description(ToolDescriptions.Keyframes.Move)]
    public Task<CallToolResult> MoveAsync(
        [Description(ToolDescriptions.Parameters.ObjectName)] string name,
        [Description("Frames to shift the keys by; negative moves earlier.")] double offset = 0,
        [Description("Factor to stretch the timing by around the pivot; 2 makes the motion twice as slow.")] double scale = 1,
        [Description("Frame the stretch is anchored at; the scene start otherwise.")] int? pivot = null,
        [Description(ToolDescriptions.Parameters.Channels)] string[]? channels = null,
        [Description("Only keys from this frame on.")] int? fromFrame = null,
        [Description("Only keys up to this frame.")] int? toFrame = null,
        CancellationToken cancellationToken = default) =>
        SendAsync(BridgeCommands.MoveKeyframes,
            new JsonObject().With("name", name).With("offset", offset).With("scale", scale).With("pivot", pivot).With("channels", channels).With("from_frame", fromFrame).With("to_frame", toFrame),
            cancellationToken);

    [McpServerTool(Name = "blender_add_fcurve_modifier", Destructive = false, OpenWorld = false)]
    [Description(ToolDescriptions.Keyframes.FcurveModifier)]
    public Task<CallToolResult> FcurveModifierAsync(
        [Description(ToolDescriptions.Parameters.ObjectName)] string name,
        [Description("CYCLES (repeat), NOISE, ENVELOPE, LIMITS, STEPPED, GENERATOR or FNGENERATOR.")] string type,
        [Description(ToolDescriptions.Parameters.Channels)] string[]? channels = null,
        [Description("Modifier settings by Python name, e.g. NOISE {\"strength\": 0.5, \"scale\": 10}, CYCLES {\"mode_after\": \"REPEAT_OFFSET\"}.")] JsonObject? settings = null,
        CancellationToken cancellationToken = default) =>
        SendAsync(BridgeCommands.AddFcurveModifier,
            new JsonObject().With("name", name).With("type", type).With("channels", channels).With("settings", settings), cancellationToken);

    [McpServerTool(Name = "blender_add_marker", Destructive = false, OpenWorld = false)]
    [Description(ToolDescriptions.Keyframes.Marker)]
    public Task<CallToolResult> MarkerAsync(
        [Description("Frame of the marker; the current frame otherwise.")] int? frame = null,
        [Description("Marker name.")] string? markerName = null,
        [Description("Camera that becomes active from this marker on (camera switching).")] string? camera = null,
        CancellationToken cancellationToken = default) =>
        SendAsync(BridgeCommands.AddMarker, new JsonObject().With("frame", frame).With("marker_name", markerName).With("camera", camera), cancellationToken);
}
