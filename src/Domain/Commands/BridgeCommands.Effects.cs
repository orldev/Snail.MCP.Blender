namespace Snail.MCP.Blender.Domain.Commands;

/// <summary>Commands of <c>addon/effects.py</c>: the post stack, cryptomatte mattes and lens effects in the compositor.</summary>
public static partial class BridgeCommands
{
    public static readonly BridgeCommand CryptomatteMatte = new("cryptomatte_matte", AddOnModules.Effects);

    public static readonly BridgeCommand LensEffects = new("lens_effects", AddOnModules.Effects);
}
