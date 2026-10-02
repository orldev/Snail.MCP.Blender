using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Snail.MCP.Blender.Tools.Prompts;

namespace Snail.MCP.Blender.Tools.Base;

/// <summary>Collections: the outliner's folders.</summary>
[McpServerToolType]
public sealed class CollectionTools(IBlenderBridge bridge) : BridgeToolBase(bridge)
{
    [McpServerTool(Name = "blender_create_collection", Destructive = false, OpenWorld = false)]
    [Description(ToolDescriptions.Collections.CreateCollection)]
    public Task<CallToolResult> CreateCollectionAsync(
        [Description("Name of the new collection.")] string name,
        [Description("Parent collection; the scene's root collection otherwise.")] string? parent = null,
        CancellationToken cancellationToken = default) =>
        SendAsync(BridgeCommands.CreateCollection, new JsonObject().With("name", name).With("parent", parent), cancellationToken);

    [McpServerTool(Name = "blender_move_to_collection", Destructive = false, OpenWorld = false)]
    [Description(ToolDescriptions.Collections.MoveToCollection)]
    public Task<CallToolResult> MoveToCollectionAsync(
        [Description("Names of the objects to move.")] string[] names,
        [Description("Target collection.")] string collection,
        [Description("Keep the objects in their other collections too (link instead of move).")] bool keepOthers = false,
        CancellationToken cancellationToken = default) =>
        SendAsync(BridgeCommands.MoveToCollection,
            new JsonObject().With("names", names).With("collection", collection).With("keep_others", keepOthers),
            cancellationToken);

    [McpServerTool(Name = "blender_update_collection", Destructive = false, OpenWorld = false)]
    [Description(ToolDescriptions.Collections.UpdateCollection)]
    public Task<CallToolResult> UpdateCollectionAsync(
        [Description("Collection name.")] string name,
        [Description("Hide or show it in every viewport.")] bool? hideViewport = null,
        [Description("Exclude from or include in renders.")] bool? hideRender = null,
        [Description("Exclude it from the view layer entirely (the checkbox in the outliner) or bring it back.")] bool? exclude = null,
        [Description("New name.")] string? newName = null,
        CancellationToken cancellationToken = default) =>
        SendAsync(BridgeCommands.UpdateCollection,
            new JsonObject().With("name", name).With("hide_viewport", hideViewport).With("hide_render", hideRender).With("exclude", exclude).With("new_name", newName),
            cancellationToken);
}
