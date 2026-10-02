namespace Snail.MCP.Blender.Domain.Commands;

/// <summary>Commands of <c>addon/files.py</c>: undo history, the .blend file itself and the project folder that holds it.</summary>
public static partial class BridgeCommands
{
    public static readonly BridgeCommand UndoRedo = new("undo_redo", AddOnModules.Files);

    public static readonly BridgeCommand SaveFile = new("save_file", AddOnModules.Files);

    public static readonly BridgeCommand OpenFile = new("open_file", AddOnModules.Files);

    public static readonly BridgeCommand NewFile = new("new_file", AddOnModules.Files);

    public static readonly BridgeCommand OpenProject = new("open_project", AddOnModules.Files);
}
