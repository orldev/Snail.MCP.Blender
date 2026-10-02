namespace Snail.MCP.Blender.Domain.Commands;

/// <summary>Commands of <c>addon/engines.py</c>: Cycles, EEVEE, Freestyle and motion blur settings.</summary>
public static partial class BridgeCommands
{
    public static readonly BridgeCommand SetCycles = new("set_cycles", AddOnModules.Engines);

    public static readonly BridgeCommand SetEevee = new("set_eevee", AddOnModules.Engines);

    public static readonly BridgeCommand SetFreestyle = new("set_freestyle", AddOnModules.Engines);

    public static readonly BridgeCommand SetMotionBlur = new("set_motion_blur", AddOnModules.Engines);
}
