using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Snail.MCP.Blender.Application.Skills;
using Snail.MCP.Blender.Tools.Prompts;

namespace Snail.MCP.Blender.Tools.Materials;

/// <summary>Textures wired into the shader, and the UVs they need.</summary>
[McpServerToolType]
[Skill(Skills.Materials, SkillDescriptions.Materials)]
public sealed class TextureTools(IBlenderBridge bridge) : BridgeToolBase(bridge)
{
    [McpServerTool(Name = "blender_load_image_texture", Destructive = false, OpenWorld = false)]
    [Description(ToolDescriptions.Textures.LoadImage)]
    public Task<CallToolResult> LoadImageAsync(
        [Description(ToolDescriptions.Parameters.MaterialName)] string material,
        [Description("Absolute path of the image file.")] string path,
        [Description("Name for the image node.")] string? name = null,
        [Description(ToolDescriptions.Parameters.ConnectTo)] string? connectTo = "Base Color",
        [Description("Colour space: sRGB for colour images, Non-Color for roughness, normal and height maps.")] string? colorspace = null,
        [Description("FLAT (UV), BOX, SPHERE or TUBE projection.")] string? projection = null,
        CancellationToken cancellationToken = default) =>
        SendAsync(BridgeCommands.LoadImageTexture,
            new JsonObject().With("material", material).With("path", path).With("name", name).With("connect_to", connectTo).With("colorspace", colorspace).With("projection", projection),
            cancellationToken);

    [McpServerTool(Name = "blender_procedural_texture", Destructive = false, OpenWorld = false)]
    [Description(ToolDescriptions.Textures.Procedural)]
    public Task<CallToolResult> ProceduralAsync(
        [Description(ToolDescriptions.Parameters.MaterialName)] string material,
        [Description("noise, voronoi, musgrave, magic, wave, brick, checker or gradient.")] string kind = "noise",
        [Description("Name for the texture node.")] string? name = null,
        [Description("Texture inputs by socket name, e.g. {\"Scale\": 5, \"Detail\": 8, \"Roughness\": 0.6}.")] JsonObject? inputs = null,
        [Description(ToolDescriptions.Parameters.NodeProperties)] JsonObject? properties = null,
        [Description(ToolDescriptions.Parameters.ConnectTo)] string? connectTo = "Base Color",
        [Description("Insert a colour ramp: [{\"position\": 0, \"color\": [r, g, b]}, ...] with stops, or [] for a default black-to-white ramp.")] JsonArray? colorRamp = null,
        [Description("Bump strength when connecting to Normal.")] double? bumpStrength = null,
        CancellationToken cancellationToken = default) =>
        SendAsync(BridgeCommands.ProceduralTexture,
            new JsonObject().With("material", material).With("kind", kind).With("name", name).With("inputs", inputs).With("properties", properties)
                .With("connect_to", connectTo).With("color_ramp", RampSpec(colorRamp)).With("bump_strength", bumpStrength),
            cancellationToken);

    [McpServerTool(Name = "blender_unwrap_uv", Destructive = false, OpenWorld = false)]
    [Description(ToolDescriptions.Textures.UnwrapUv)]
    public Task<CallToolResult> UnwrapUvAsync(
        [Description(ToolDescriptions.Parameters.MeshName)] string name,
        [Description("smart (Smart UV Project, the default), unwrap (needs seams), cube, sphere or cylinder.")] string method = "smart",
        [Description("Space between UV islands, 0 to 1.")] double margin = 0.02,
        [Description("smart: faces meeting at a sharper angle in degrees start a new island.")] double? angleLimit = null,
        CancellationToken cancellationToken = default) =>
        SendAsync(BridgeCommands.UnwrapUv,
            new JsonObject().With("name", name).With("method", method).With("margin", margin).With("angle_limit", angleLimit),
            cancellationToken);

    /// <summary>An empty array asks for the default ramp; the add-on reads a bare true as "ramp, default stops".</summary>
    private static JsonNode? RampSpec(JsonArray? ramp) =>
        ramp is null ? null : ramp.Count == 0 ? JsonValue.Create(true) : ramp.DeepClone();
}
