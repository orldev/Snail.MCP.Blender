using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Snail.MCP.Blender.Application.Skills;
using Snail.MCP.Blender.Tools.Prompts;

namespace Snail.MCP.Blender.Tools.Rendering;

/// <summary>What a render becomes on disk: colour management, format with depth and codec, stamps, region, File Output nodes.</summary>
[McpServerToolType]
[Skill(Skills.Rendering, SkillDescriptions.Rendering)]
public sealed class OutputTools(IBlenderBridge bridge) : BridgeToolBase(bridge)
{
    [McpServerTool(Name = "blender_set_color_management", Destructive = false, OpenWorld = false)]
    [Description(ToolDescriptions.Output.ColorManagement)]
    public Task<CallToolResult> ColorManagementAsync(
        [Description("sRGB (the default), Display P3, Rec.1886, Rec.2020, Rec.2100-PQ, Rec.2100-HLG.")] string? displayDevice = null,
        [Description("AgX (the default, filmic highlights), Filmic, Standard (no tone mapping), Khronos PBR Neutral, ACES 1.3, ACES 2.0, False Color, Raw.")] string? viewTransform = null,
        [Description("Look of the view transform: None, Punchy, Greyscale, Very High Contrast, High Contrast, Medium High Contrast, Base Contrast, Medium Low Contrast, Low Contrast, Very Low Contrast; the AgX prefix is added when needed.")] string? look = null,
        [Description("Exposure in stops; 0 is neutral.")] double? exposure = null,
        [Description("Display gamma; 1 is neutral.")] double? gamma = null,
        [Description("Apply the RGB curves of the colour management panel.")] bool? useCurves = null,
        [Description("White balance of the view.")] WhiteBalance? whiteBalance = null,
        [Description("Colour space of sequencer strips, e.g. sRGB, ACEScg, AgX Log, Linear Rec.709.")] string? sequencerColorspace = null,
        [Description(ToolDescriptions.Parameters.Scene)] string? scene = null,
        CancellationToken cancellationToken = default) =>
        SendAsync(BridgeCommands.SetColorManagement,
            new JsonObject().With("display_device", displayDevice).With("view_transform", viewTransform).With("look", look).With("exposure", exposure).With("gamma", gamma)
                .With("use_curves", useCurves).With("white_balance", whiteBalance).With("sequencer_colorspace", sequencerColorspace).With("scene", scene),
            cancellationToken);

    [McpServerTool(Name = "blender_set_output", Destructive = false, OpenWorld = false)]
    [Description(ToolDescriptions.Output.Settings)]
    public Task<CallToolResult> SettingsAsync(
        [Description("Output path template: a folder plus a prefix, #### where the frame number goes, e.g. /renders/shot01/beauty_####.")] string? path = null,
        [Description("PNG, JPEG, OPEN_EXR, OPEN_EXR_MULTILAYER, TIFF, WEBP, BMP, DPX, CINEON, TARGA, HDR, JPEG2000, AVIF, or FFMPEG for video.")] string? fileFormat = null,
        [Description("BW, RGB or RGBA.")] string? colorMode = null,
        [Description(ToolDescriptions.Parameters.ColorDepth)] int? colorDepth = null,
        [Description(ToolDescriptions.Parameters.ExrCodec)] string? exrCodec = null,
        [Description("PNG compression 0 to 100; 15 is the default, higher is smaller and slower.")] int? compression = null,
        [Description("JPEG, WebP and AVIF quality 0 to 100.")] int? quality = null,
        [Description("Overwrite frames that exist; false skips them, the way farms share a frame range.")] bool? overwrite = null,
        [Description("Write an empty placeholder before rendering each frame so other machines skip it.")] bool? placeholder = null,
        [Description("Append the format's extension to the file name.")] bool? fileExtension = null,
        [Description("FOLLOW_SCENE applies the view transform to the file, OVERRIDE keeps the file's own (used for linear EXR).")] string? colorManagement = null,
        [Description("Metadata burnt into the image.")] Stamp? stamp = null,
        [Description("Render region [minX, minY, maxX, maxY] as fractions of the frame, 0 to 1; pass null to render the whole frame.")] double[]? region = null,
        [Description("Crop the output to the region instead of leaving the rest empty.")] bool? cropToRegion = null,
        [Description(ToolDescriptions.Parameters.ResolutionPreset)] string? preset = null,
        [Description("Stereoscopic or multi-view rendering.")] Stereo? stereo = null,
        [Description(ToolDescriptions.Parameters.Scene)] string? scene = null,
        CancellationToken cancellationToken = default) =>
        SendAsync(BridgeCommands.SetOutput,
            new JsonObject().With("path", path).With("file_format", fileFormat).With("color_mode", colorMode).With("color_depth", colorDepth).With("exr_codec", exrCodec)
                .With("compression", compression).With("quality", quality).With("overwrite", overwrite).With("placeholder", placeholder).With("file_extension", fileExtension)
                .With("color_management", colorManagement).With("stamp", stamp).With("region", region).With("crop_to_region", cropToRegion).With("preset", preset)
                .With("stereo", stereo).With("scene", scene),
            cancellationToken);
}
