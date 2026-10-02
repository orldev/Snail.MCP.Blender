using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Snail.MCP.Blender.Application.Rendering;
using Snail.MCP.Blender.Application.Skills;
using Snail.MCP.Blender.Tools.Prompts;

namespace Snail.MCP.Blender.Tools.Rendering;

/// <summary>Pictures of a thing rather than of the scene: product shots, viewport captures, baked textures, and a look at any image on disk.</summary>
[McpServerToolType]
[Skill(Skills.Rendering, SkillDescriptions.Rendering)]
public sealed class ShotTools(IBlenderBridge bridge, RenderWatch watch, RenderGallery gallery) : BridgeToolBase(bridge)
{
    [McpServerTool(Name = "blender_render_object", Destructive = false, OpenWorld = false)]
    [Description(ToolDescriptions.Rendering.RenderObject)]
    public async Task<CallToolResult> RenderObjectAsync(
        [Description("Absolute path of the image to write.")] string path,
        [Description(ToolDescriptions.Parameters.ObjectName)] string? name = null,
        [Description("Several objects to frame together instead of one.")] string[]? names = null,
        [Description("Camera direction around the vertical axis in degrees; 0 looks from +X.")] double? azimuth = null,
        [Description("Camera height angle in degrees; 0 is level, 90 is straight down.")] double? elevation = null,
        [Description("Focal length in millimetres.")] double? lens = null,
        [Description("Space around the object; 1 fills the frame, 1.15 is the default.")] double? margin = null,
        [Description("Transparent background.")] bool transparent = true,
        [Description(ToolDescriptions.Parameters.RenderResolution)] int? resolutionX = null,
        [Description(ToolDescriptions.Parameters.RenderResolution)] int? resolutionY = null,
        [Description("Keep the temporary camera (SnailShot) in the scene.")] bool keepCamera = false,
        [Description(ToolDescriptions.Parameters.Preview)] bool preview = true,
        [Description(ToolDescriptions.Parameters.TimeoutSeconds)] int timeoutSeconds = 300,
        IProgress<ProgressNotificationValue>? progress = null,
        CancellationToken cancellationToken = default) =>
        ToolLimits.IsRenderable(resolutionX) && ToolLimits.IsRenderable(resolutionY)
            ? await WithinAsync(timeoutSeconds, async timeout => ToolResponse.Pictured(gallery.Remember(await watch.FollowAsync(
                ExchangeAsync(BridgeCommands.RenderObject,
                    new JsonObject().With("path", path).With("name", name).With("names", names).With("azimuth", azimuth).With("elevation", elevation).With("lens", lens)
                        .With("margin", margin).With("transparent", transparent).With("resolution_x", resolutionX).With("resolution_y", resolutionY).With("keep_camera", keepCamera).With("preview", preview),
                    timeout, cancellationToken),
                progress, 1, cancellationToken).ConfigureAwait(false)))).ConfigureAwait(false)
            : (ToolResponse.Failure(Messages.ResolutionTooLarge, Messages.ResolutionHint));

    [McpServerTool(Name = "blender_viewport_capture", Destructive = false, OpenWorld = false)]
    [Description(ToolDescriptions.Rendering.ViewportCapture)]
    public async Task<CallToolResult> ViewportCaptureAsync(
        [Description("Absolute path of the image to write.")] string path,
        [Description(ToolDescriptions.Parameters.Preview)] bool preview = true,
        CancellationToken cancellationToken = default) =>
        ToolResponse.Pictured(gallery.Remember(await ExchangeAsync(BridgeCommands.ViewportCapture, new JsonObject().With("path", path).With("preview", preview), TimeSpan.FromSeconds(60), cancellationToken).ConfigureAwait(false)));

    [McpServerTool(Name = "blender_inspect_image", ReadOnly = true, OpenWorld = false)]
    [Description(ToolDescriptions.Rendering.InspectImage)]
    public async Task<CallToolResult> InspectImageAsync(
        [Description("Absolute path of the image: PNG, JPEG, EXR, TIFF or anything Blender opens.")] string path,
        [Description(ToolDescriptions.Parameters.Preview)] bool preview = true,
        [Description("Longest edge of the preview in pixels; 512 by default, 1024 to read fine detail.")] int? previewSize = null,
        CancellationToken cancellationToken = default) =>
        ToolResponse.Pictured(gallery.Remember(await ExchangeAsync(BridgeCommands.InspectImage, new JsonObject().With("path", path).With("preview", preview).With("preview_size", previewSize), TimeSpan.FromSeconds(60), cancellationToken).ConfigureAwait(false)));

    [McpServerTool(Name = "blender_bake_texture", Destructive = false, OpenWorld = false)]
    [Description(ToolDescriptions.Rendering.BakeTexture)]
    public async Task<CallToolResult> BakeTextureAsync(
        [Description(ToolDescriptions.Parameters.MeshName)] string name,
        [Description("Absolute path of the PNG to write.")] string path,
        [Description("DIFFUSE, COMBINED, AO, NORMAL, ROUGHNESS, EMIT, SHADOW, POSITION, UV, GLOSSY, TRANSMISSION or ENVIRONMENT.")] string type = "DIFFUSE",
        [Description("Texture width in pixels, up to 4096.")] int width = 1024,
        [Description("Texture height in pixels; the width otherwise.")] int? height = null,
        [Description("Cycles samples; low values are fine for colour bakes.")] int? samples = null,
        [Description("Pixels of bleed around UV islands.")] int? margin = null,
        [Description("Name for the image datablock.")] string? imageName = null,
        [Description("Leave the image texture node in the materials.")] bool keepNode = false,
        [Description(ToolDescriptions.Parameters.Preview)] bool preview = true,
        [Description(ToolDescriptions.Parameters.TimeoutSeconds)] int timeoutSeconds = 600,
        IProgress<ProgressNotificationValue>? progress = null,
        CancellationToken cancellationToken = default) =>
        await WithinAsync(timeoutSeconds, async timeout => ToolResponse.Pictured(gallery.Remember(await watch.FollowAsync(
            ExchangeAsync(BridgeCommands.BakeTexture,
                new JsonObject().With("name", name).With("path", path).With("type", type).With("width", width).With("height", height).With("samples", samples)
                    .With("margin", margin).With("image_name", imageName).With("keep_node", keepNode).With("preview", preview),
                timeout, cancellationToken),
            progress, 1, cancellationToken).ConfigureAwait(false)))).ConfigureAwait(false);
}
