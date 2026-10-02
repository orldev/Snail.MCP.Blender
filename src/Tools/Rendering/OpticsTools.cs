using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Snail.MCP.Blender.Application.Skills;
using Snail.MCP.Blender.Tools.Prompts;

namespace Snail.MCP.Blender.Tools.Rendering;

/// <summary>The glass: the physical camera in front of the sensor.</summary>
[McpServerToolType]
[Skill(Skills.Rendering, SkillDescriptions.Rendering)]
public sealed class OpticsTools(IBlenderBridge bridge) : BridgeToolBase(bridge)
{
    [McpServerTool(Name = "blender_set_camera_optics", Destructive = false, OpenWorld = false)]
    [Description(ToolDescriptions.Lens.Optics)]
    public Task<CallToolResult> OpticsAsync(
        [Description("Camera object; the scene camera otherwise.")] string? camera = null,
        [Description("Sensor size and fit.")] Sensor? sensor = null,
        [Description("Focal length in mm.")] double? lens = null,
        [Description("Horizontal field of view in degrees instead of the focal length.")] double? fov = null,
        [Description("Lens shift [x, y] as fractions of the frame; keeps verticals parallel without tilting the camera.")] double[]? shift = null,
        [Description("Clip range [start, end] in metres.")] double[]? clip = null,
        [Description("PERSP, ORTHO or PANO.")] string? type = null,
        [Description("Panoramic projection, which also sets the type to PANO: EQUIRECTANGULAR, FISHEYE_EQUIDISTANT, FISHEYE_EQUISOLID, FISHEYE_LENS_POLYNOMIAL, CENTRAL_CYLINDRICAL.")] string? panorama = null,
        [Description("Orthographic scale in metres.")] double? orthoScale = null,
        [Description("Aperture: f-stop, blades, rotation, anamorphic ratio.")] Aperture? aperture = null,
        [Description("Focus by distance, object or bone.")] Focus? focus = null,
        [Description(ToolDescriptions.Parameters.Scene)] string? scene = null,
        CancellationToken cancellationToken = default) =>
        SendAsync(BridgeCommands.SetCameraOptics,
            new JsonObject().With("camera", camera).With("sensor", sensor).With("lens", lens).With("fov", fov).With("shift", shift).With("clip", clip).With("type", type)
                .With("panorama", panorama).With("ortho_scale", orthoScale).With("aperture", aperture).With("focus", focus).With("scene", scene),
            cancellationToken);
}
