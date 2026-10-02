namespace Snail.MCP.Blender.Domain.Commands;

/// <summary>Commands of <c>addon/output.py</c>: colour management, output format and File Output nodes.</summary>
public static partial class BridgeCommands
{
    public static readonly BridgeCommand SetColorManagement = new("set_color_management", AddOnModules.Output);

    public static readonly BridgeCommand SetOutput = new("set_output", AddOnModules.Output);

    public static readonly BridgeCommand FileOutput = new("file_output", AddOnModules.Output);
}
