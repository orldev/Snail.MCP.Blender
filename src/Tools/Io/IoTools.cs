using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Snail.MCP.Blender.Application.Skills;
using Snail.MCP.Blender.Tools.Prompts;

namespace Snail.MCP.Blender.Tools.Io;

/// <summary>Files in, files out.</summary>
[McpServerToolType]
[Skill(Skills.Io, SkillDescriptions.Io)]
public sealed class IoTools(IBlenderBridge bridge) : BridgeToolBase(bridge)
{
    [McpServerTool(Name = "blender_import_file", Destructive = false, OpenWorld = false)]
    [Description(ToolDescriptions.Io.Import)]
    public Task<CallToolResult> ImportAsync(
        [Description("Absolute path of the file to import.")] string path,
        [Description(ToolDescriptions.Parameters.ImportFormat)] string? format = null,
        [Description("Extra importer properties by name, as blender_describe_operator lists them for the importer, e.g. {\"global_scale\": 0.01}.")] JsonObject? options = null,
        [Description(ToolDescriptions.Parameters.TimeoutSeconds)] int timeoutSeconds = 120,
        CancellationToken cancellationToken = default) =>
        WithinAsync(timeoutSeconds, timeout => SendAsync(BridgeCommands.ImportFile,
            new JsonObject().With("path", path).With("format", format).With("options", options), cancellationToken, timeout));

    [McpServerTool(Name = "blender_export_file", Destructive = true, OpenWorld = false)]
    [Description(ToolDescriptions.Io.Export)]
    public Task<CallToolResult> ExportAsync(
        [Description("Absolute path of the file to write; folders are created.")] string path,
        [Description(ToolDescriptions.Parameters.ExportFormat)] string? format = null,
        [Description("Export only selected objects instead of the whole scene.")] bool selectedOnly = false,
        [Description("With selectedOnly: select exactly these objects first.")] string[]? names = null,
        [Description("Extra exporter properties by name, e.g. {\"apply_unit_scale\": true}, {\"export_apply\": true}.")] JsonObject? options = null,
        [Description(ToolDescriptions.Parameters.TimeoutSeconds)] int timeoutSeconds = 120,
        CancellationToken cancellationToken = default) =>
        WithinAsync(timeoutSeconds, timeout => SendAsync(BridgeCommands.ExportFile,
            new JsonObject().With("path", path).With("format", format).With("selected_only", selectedOnly).With("names", names).With("options", options), cancellationToken, timeout));

    [McpServerTool(Name = "blender_list_assets", ReadOnly = true, OpenWorld = false)]
    [Description(ToolDescriptions.Io.ListAssets)]
    public Task<CallToolResult> ListAssetsAsync(
        [Description("Absolute path of the .blend file.")] string path,
        [Description("Types to list: objects, collections, materials, node_groups, worlds, images, meshes, actions, cameras, lights, scenes, texts; every type otherwise.")] string[]? types = null,
        [Description("Only datablocks marked as assets.")] bool assetsOnly = false,
        CancellationToken cancellationToken = default) =>
        SendAsync(BridgeCommands.ListAssets, new JsonObject().With("path", path).With("types", types).With("assets_only", assetsOnly), cancellationToken, TimeSpan.FromSeconds(60));

    [McpServerTool(Name = "blender_append_assets", Destructive = false, OpenWorld = false)]
    [Description(ToolDescriptions.Io.AppendAssets)]
    public Task<CallToolResult> AppendAssetsAsync(
        [Description("Absolute path of the .blend file.")] string path,
        [Description("objects (the default), collections, materials, node_groups, worlds, images, meshes, actions, cameras, lights.")] string type = "objects",
        [Description("Names to bring in; everything of that type otherwise.")] string[]? names = null,
        [Description("Link instead of append: the data stays in the other file and updates with it.")] bool link = false,
        [Description("Collection to place objects or collections into; created when missing.")] string? collection = null,
        [Description(ToolDescriptions.Parameters.Scene)] string? scene = null,
        CancellationToken cancellationToken = default) =>
        SendAsync(BridgeCommands.AppendAssets,
            new JsonObject().With("path", path).With("type", type).With("names", names).With("link", link).With("collection", collection).With("scene", scene),
            cancellationToken, TimeSpan.FromSeconds(120));
}
