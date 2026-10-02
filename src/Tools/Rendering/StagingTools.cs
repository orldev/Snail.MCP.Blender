using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Snail.MCP.Blender.Application.Skills;
using Snail.MCP.Blender.Tools.Prompts;

namespace Snail.MCP.Blender.Tools.Rendering;

/// <summary>Staging a subject for the camera: moves that circle or approach it, and the lights around it.</summary>
[McpServerToolType]
[Skill(Skills.Rendering, SkillDescriptions.Rendering)]
public sealed class StagingTools(IBlenderBridge bridge) : BridgeToolBase(bridge)
{
    [McpServerTool(Name = "blender_camera_move", Destructive = false, OpenWorld = false)]
    [Description(ToolDescriptions.Staging.CameraMove)]
    public Task<CallToolResult> CameraMoveAsync(
        [Description(ToolDescriptions.Parameters.CameraMove)] string type,
        [Description("Subject object.")] string? target = null,
        [Description("Several subjects framed together.")] string[]? targets = null,
        [Description("Camera to move; the scene camera otherwise. Not used by turntable.")] string? camera = null,
        [Description("First frame; the scene start otherwise.")] int? start = null,
        [Description("Last frame; the scene end otherwise.")] int? end = null,
        [Description("turntable, orbit: full turns over the range; 1 by default.")] double? revolutions = null,
        [Description("Start angle around the subject in degrees.")] double? startAngle = null,
        [Description("orbit, dolly, crane: camera distance from the subject in metres; from its size otherwise.")] double? distance = null,
        [Description("dolly, push_in: distance at the end; half the start otherwise.")] double? distanceEnd = null,
        [Description("orbit, dolly: camera height above the subject centre; crane: start height.")] double? height = null,
        [Description("crane: height at the end.")] double? heightEnd = null,
        [Description("LINEAR (the default, for loops) or BEZIER (eases in and out).")] string? interpolation = null,
        [Description("Name of the pivot empty for turntable and orbit.")] string? pivotName = null,
        [Description(ToolDescriptions.Parameters.Scene)] string? scene = null,
        CancellationToken cancellationToken = default) =>
        SendAsync(BridgeCommands.CameraMove,
            new JsonObject().With("type", type).With("target", target).With("targets", targets).With("camera", camera).With("start", start).With("end", end)
                .With("revolutions", revolutions).With("start_angle", startAngle).With("distance", distance).With("distance_end", distanceEnd).With("height", height)
                .With("height_end", heightEnd).With("interpolation", interpolation).With("pivot_name", pivotName).With("scene", scene),
            cancellationToken);

    [McpServerTool(Name = "blender_light_rig", Destructive = false, OpenWorld = false)]
    [Description(ToolDescriptions.Staging.LightRig)]
    public Task<CallToolResult> LightRigAsync(
        [Description("Subject object.")] string? target = null,
        [Description("Several subjects lit together.")] string[]? targets = null,
        [Description("Name prefix for the lights; Rig otherwise.")] string? name = null,
        [Description("Lights to build: key, fill, rim; all three otherwise.")] string[]? roles = null,
        [Description("Key light power in watts; 1000 by default.")] double? keyEnergy = null,
        [Description("Fill power as a fraction of the key; 0.35 by default (a 3:1 ratio).")] double? fillRatio = null,
        [Description("Rim power as a fraction of the key; 1.5 by default.")] double? rimRatio = null,
        [Description("Key light direction around the subject in degrees; 45 by default.")] double? keyAngle = null,
        [Description("Distance of the lights from the subject in metres; from its size otherwise.")] double? distance = null,
        [Description("Key colour temperature in kelvin; 5600 by default.")] double? keyTemperature = null,
        [Description("Fill colour temperature in kelvin; 6500 by default.")] double? fillTemperature = null,
        [Description("Rim colour temperature in kelvin; 6500 by default.")] double? rimTemperature = null,
        [Description(ToolDescriptions.Parameters.Scene)] string? scene = null,
        CancellationToken cancellationToken = default) =>
        SendAsync(BridgeCommands.LightRig,
            new JsonObject().With("target", target).With("targets", targets).With("name", name).With("roles", roles).With("key_energy", keyEnergy).With("fill_ratio", fillRatio)
                .With("rim_ratio", rimRatio).With("key_angle", keyAngle).With("distance", distance).With("key_temperature", keyTemperature).With("fill_temperature", fillTemperature)
                .With("rim_temperature", rimTemperature).With("scene", scene),
            cancellationToken);
}
