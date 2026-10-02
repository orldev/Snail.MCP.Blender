using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Snail.MCP.Blender.Tools.Prompts;

namespace Snail.MCP.Blender.Tools.Base;

/// <summary>The .blend file, the undo history and snapshots of the file.</summary>
[McpServerToolType]
public sealed class FileTools(IBlenderBridge bridge) : BridgeToolBase(bridge)
{
    private static readonly TimeSpan FileTimeout = TimeSpan.FromSeconds(120);

    [McpServerTool(Name = "blender_undo_redo", Destructive = true, OpenWorld = false)]
    [Description(ToolDescriptions.Files.UndoRedo)]
    public Task<CallToolResult> UndoRedoAsync(
        [Description("undo or redo.")] string action = "undo",
        [Description("How many steps.")] int steps = 1,
        CancellationToken cancellationToken = default) =>
        SendAsync(BridgeCommands.UndoRedo, new JsonObject().With("action", action).With("steps", steps), cancellationToken);

    [McpServerTool(Name = "blender_save_file", Destructive = true, OpenWorld = false)]
    [Description(ToolDescriptions.Files.SaveFile)]
    public Task<CallToolResult> SaveFileAsync(
        [Description("Absolute path of the .blend file; the current file's path otherwise.")] string? path = null,
        [Description("Write a copy and keep working in the current file.")] bool copy = false,
        CancellationToken cancellationToken = default) =>
        SendAsync(BridgeCommands.SaveFile, new JsonObject().With("path", path).With("copy", copy), cancellationToken, FileTimeout);

    [McpServerTool(Name = "blender_open_file", Destructive = true, OpenWorld = false)]
    [Description(ToolDescriptions.Files.OpenFile)]
    public Task<CallToolResult> OpenFileAsync(
        [Description("Absolute path of the .blend file.")] string path,
        [Description("Also load the window layout saved in the file.")] bool loadUi = false,
        CancellationToken cancellationToken = default) =>
        SendAsync(BridgeCommands.OpenFile, new JsonObject().With("path", path).With("load_ui", loadUi), cancellationToken, FileTimeout);

    [McpServerTool(Name = "blender_new_file", Destructive = true, OpenWorld = false)]
    [Description(ToolDescriptions.Files.NewFile)]
    public Task<CallToolResult> NewFileAsync(
        [Description("Start empty instead of with the default cube, light and camera.")] bool empty = false,
        CancellationToken cancellationToken = default) =>
        SendAsync(BridgeCommands.NewFile, new JsonObject().With("empty", empty), cancellationToken, FileTimeout);

    [McpServerTool(Name = "blender_open_project", Destructive = true, OpenWorld = false)]
    [Description(ToolDescriptions.Files.OpenProject)]
    public Task<CallToolResult> OpenProjectAsync(
        [Description("The project: one folder name, the name of the folder the client works in.")] string name,
        [Description("empty (the default) starts a new project from an empty scene; current makes the scene open now the project's.")] string from = "empty",
        [Description("Drop the unsaved changes of the open file instead of refusing.")] bool discard = false,
        CancellationToken cancellationToken = default) =>
        SendAsync(BridgeCommands.OpenProject, new JsonObject().With("name", name).With("from", from).With("discard", discard), cancellationToken, FileTimeout);

    [McpServerTool(Name = "blender_snapshot", Destructive = true, OpenWorld = false)]
    [Description(ToolDescriptions.Files.Snapshot)]
    public Task<CallToolResult> SnapshotAsync(
        [Description("save (the default), restore, list, remove.")] string action = "save",
        [Description("Snapshot name; a timestamp otherwise.")] string? name = null,
        [Description("save: a note to remember why.")] string? note = null,
        CancellationToken cancellationToken = default) =>
        SendAsync(BridgeCommands.Snapshot, new JsonObject().With("action", action).With("name", name).With("note", note), cancellationToken, FileTimeout);
}
