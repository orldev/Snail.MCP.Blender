using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Snail.MCP.Blender.Application.Skills;
using Snail.MCP.Blender.Tools.Prompts;

namespace Snail.MCP.Blender.Tools.Post;

/// <summary>The compositor node tree that post-processes renders: nodes, File Output, cryptomatte mattes.</summary>
[McpServerToolType]
[Skill(Skills.Post, SkillDescriptions.Post)]
public sealed class CompositorTools(IBlenderBridge bridge) : BridgeToolBase(bridge)
{
    [McpServerTool(Name = "blender_compositor", Destructive = false, OpenWorld = false)]
    [Description(ToolDescriptions.Rendering.Compositor)]
    public Task<CallToolResult> CompositorAsync(
        [Description("info (list nodes and links), clear (reset to Render Layers → output), add_node, link, remove_node.")]
        string action = "info",
        [Description("add_node: compositor node bl_idname or suffix: Blur, Glare, Filter, ColorBalance, HueSat, BrightContrast, Denoise, Lensdist, Defocus, Kuwahara, AlphaOver, Viewer, OutputFile, RLayers; Mix, Math and Noise from the shader set; Composite is the output.")]
        string? type = null,
        [Description("add_node: name for the node; remove_node: node to delete.")] string? name = null,
        [Description("add_node: input sockets by name, e.g. {\"Size\": 10}; menu sockets take their label, e.g. Filter {\"Type\": \"Box Sharpen\"}.")] JsonObject? inputs = null,
        [Description("add_node: settings by Python name, e.g. {\"filter_type\": \"GAUSS\"}, {\"glare_type\": \"FOG_GLOW\"}.")] JsonObject? properties = null,
        [Description("add_node: [x, y] position in the editor.")] double[]? location = null,
        [Description("link: source node name.")] string? fromNode = null,
        [Description("link: output socket on the source, e.g. Image, Depth, Normal.")] string? fromSocket = null,
        [Description("link: target node name.")] string? toNode = null,
        [Description("link: input socket on the target.")] string? toSocket = null,
        CancellationToken cancellationToken = default) =>
        SendAsync(BridgeCommands.Compositor,
            new JsonObject().With("action", action).With("type", type).With("name", name).With("node", name).With("inputs", inputs).With("properties", properties)
                .With("location", location).With("from_node", fromNode).With("from_socket", fromSocket).With("to_node", toNode).With("to_socket", toSocket),
            cancellationToken);

    [McpServerTool(Name = "blender_file_output", Destructive = false, OpenWorld = false)]
    [Description(ToolDescriptions.Output.FileOutput)]
    public Task<CallToolResult> FileOutputAsync(
        [Description("configure (create or update the node), info (list File Output nodes), remove.")] string action = "configure",
        [Description("Node name; File Output otherwise. Several nodes write to several places.")] string? name = null,
        [Description("Absolute folder the files go to.")] string? directory = null,
        [Description("File name prefix before the slot name and frame number, e.g. shot01_.")] string? fileName = null,
        [Description("OPEN_EXR_MULTILAYER (every slot in one file), OPEN_EXR, PNG, JPEG, TIFF, WEBP, DPX.")] string? fileFormat = null,
        [Description(ToolDescriptions.Parameters.ColorDepth)] int? colorDepth = null,
        [Description(ToolDescriptions.Parameters.ExrCodec)] string? exrCodec = null,
        [Description("Slots, one per pass, replacing the current ones: pass names such as Image, Depth, Normal, Mist, AO, Vector, Position, UV, Emit, Env, Shadow, IndexOB, CryptoObject00, or objects {\"pass\": \"Depth\", \"name\": \"Z\", \"file_format\": \"OPEN_EXR\", \"color_depth\": 32, \"save_as_render\": false}; save_as_render applies the view transform, off for data passes and for EXR unless asked.")] JsonArray? slots = null,
        [Description("One slot for every pass the Render Layers node offers.")] bool allPasses = false,
        CancellationToken cancellationToken = default) =>
        SendAsync(BridgeCommands.FileOutput,
            new JsonObject().With("action", action).With("name", name).With("directory", directory).With("file_name", fileName).With("file_format", fileFormat)
                .With("color_depth", colorDepth).With("exr_codec", exrCodec).With("slots", slots).With("all_passes", allPasses),
            cancellationToken);

    [McpServerTool(Name = "blender_cryptomatte_matte", Destructive = false, OpenWorld = false)]
    [Description(ToolDescriptions.Rendering.CryptomatteMatte)]
    public Task<CallToolResult> CryptomatteMatteAsync(
        [Description("Objects, materials or assets to matte, by name.")] string[] names,
        [Description("object (the default), material or asset: which cryptomatte layer the names live in.")] string layer = "object",
        [Description("Name for the Cryptomatte node; derived from the names otherwise.")] string? name = null,
        [Description("File Output node to write the matte through; created when missing.")] string? output = null,
        [Description("Folder for that File Output node.")] string? directory = null,
        [Description("Slot name in the File Output node; Matte otherwise.")] string? slot = null,
        [Description(ToolDescriptions.Parameters.Scene)] string? scene = null,
        CancellationToken cancellationToken = default) =>
        SendAsync(BridgeCommands.CryptomatteMatte,
            new JsonObject().With("names", names).With("layer", layer).With("name", name).With("output", output).With("directory", directory).With("slot", slot).With("scene", scene),
            cancellationToken);
}
