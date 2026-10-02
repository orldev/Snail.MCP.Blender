namespace Snail.MCP.Blender.Domain.Commands;

/// <summary>Commands of <c>addon/physics.py</c>: rigid and soft bodies, cloth, fluids and particles.</summary>
public static partial class BridgeCommands
{
    public static readonly BridgeCommand AddPhysics = new("add_physics", AddOnModules.Physics);

    public static readonly BridgeCommand RemovePhysics = new("remove_physics", AddOnModules.Physics);

    public static readonly BridgeCommand AddParticles = new("add_particles", AddOnModules.Physics);

    public static readonly BridgeCommand BakePhysics = new("bake_physics", AddOnModules.Physics);
}
