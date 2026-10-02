namespace Snail.MCP.Blender.Domain.Commands;

/// <summary>Commands of <c>addon/interchange.py</c>: import and export in the formats Blender ships operators for.</summary>
public static partial class BridgeCommands
{
    public static readonly BridgeCommand ImportFile = new("import_file", AddOnModules.Interchange);

    public static readonly BridgeCommand ExportFile = new("export_file", AddOnModules.Interchange);
}
