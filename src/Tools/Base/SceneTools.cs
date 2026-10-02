using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Snail.MCP.Blender.Tools.Prompts;

namespace Snail.MCP.Blender.Tools.Base;

/// <summary>Reading the scene and choosing what to work on.</summary>
[McpServerToolType]
public sealed class SceneTools(IBlenderBridge bridge) : BridgeToolBase(bridge)
{
    [McpServerTool(Name = "blender_scene_info", ReadOnly = true, OpenWorld = false)]
    [Description(ToolDescriptions.Scene.Info)]
    public Task<CallToolResult> SceneInfoAsync(CancellationToken cancellationToken = default) =>
        SendAsync(BridgeCommands.SceneInfo, [], cancellationToken);

    [McpServerTool(Name = "blender_list_objects", ReadOnly = true, OpenWorld = false)]
    [Description(ToolDescriptions.Scene.ListObjects)]
    public Task<CallToolResult> ListObjectsAsync(
        [Description(ToolDescriptions.Parameters.ObjectType)] string? type = null,
        [Description("Keep only objects whose name contains this text, case-insensitive.")] string? nameContains = null,
        [Description("How many objects to return at most; the total is reported regardless.")] int limit = 100,
        CancellationToken cancellationToken = default) =>
        SendAsync(BridgeCommands.ListObjects,
            new JsonObject().With("type", type).With("name_contains", nameContains).With("limit", limit),
            cancellationToken);

    [McpServerTool(Name = "blender_object_info", ReadOnly = true, OpenWorld = false)]
    [Description(ToolDescriptions.Scene.ObjectInfo)]
    public Task<CallToolResult> ObjectInfoAsync(
        [Description(ToolDescriptions.Parameters.ObjectName)] string name,
        CancellationToken cancellationToken = default) =>
        SendAsync(BridgeCommands.ObjectInfo, new JsonObject().With("name", name), cancellationToken);

    [McpServerTool(Name = "blender_select_objects", Destructive = false, OpenWorld = false)]
    [Description(ToolDescriptions.Scene.SelectObjects)]
    public Task<CallToolResult> SelectObjectsAsync(
        [Description("Object names to select.")] string[]? names = null,
        [Description(ToolDescriptions.Parameters.ObjectType)] string? type = null,
        [Description("Select objects whose name contains this text, case-insensitive.")] string? nameContains = null,
        [Description("replace (the default: deselect everything first), add (keep the current selection), none (deselect all), all (select all).")]
        string mode = "replace",
        CancellationToken cancellationToken = default) =>
        SendAsync(BridgeCommands.SelectObjects,
            new JsonObject().With("names", names).With("type", type).With("name_contains", nameContains).With("mode", mode),
            cancellationToken);

    [McpServerTool(Name = "blender_scenes", Destructive = true, OpenWorld = false)]
    [Description(ToolDescriptions.Scenes.Manage)]
    public Task<CallToolResult> ScenesAsync(
        [Description("list (the default), create, activate, rename, remove.")] string action = "list",
        [Description("Scene name for create, activate, rename and remove.")] string? name = null,
        [Description("create: EMPTY (the default), LINK_COPY (shares the objects) or FULL_COPY (needs Blender's window).")] string? copy = null,
        [Description("rename: the new name.")] string? newName = null,
        [Description("create: make the new scene the active one; needs Blender's window.")] bool activate = false,
        [Description("create: scene to copy from; the current one otherwise.")] string? scene = null,
        CancellationToken cancellationToken = default) =>
        SendAsync(BridgeCommands.Scenes,
            new JsonObject().With("action", action).With("name", name).With("copy", copy).With("new_name", newName).With("activate", activate).With("scene", scene),
            cancellationToken);
}
