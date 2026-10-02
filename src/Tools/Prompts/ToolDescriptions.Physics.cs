namespace Snail.MCP.Blender.Tools.Prompts;

public static partial class ToolDescriptions
{
    /// <summary><c>PhysicsTools</c>.</summary>
    public static class Physics
    {
        /// <summary><c>blender_add_physics</c></summary>
        public const string Add =
            "Adds a simulation to an object: rigid body, cloth, soft body, collision, fluid or dynamic paint, with settings. " +
            "Rigid bodies need the animation played or the cache baked to move.";

        /// <summary><c>blender_remove_physics</c></summary>
        public const string Remove = "Removes a simulation or the particle systems from an object.";

        /// <summary><c>blender_add_particles</c></summary>
        public const string Particles = "Adds an emitter or hair particle system to a mesh with count, timing, length and further settings.";

        /// <summary><c>blender_bake_physics</c></summary>
        public const string Bake = "Bakes every physics cache of the scene up to a frame, or frees them; needed before rendering simulations.";
    }
}
