using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Snail.MCP.Blender.Application.Skills;
using Snail.MCP.Blender.Tools.Prompts;

namespace Snail.MCP.Blender.Tools.Physics;

/// <summary>Simulations and particles.</summary>
[McpServerToolType]
[Skill(Skills.Physics, SkillDescriptions.Physics)]
public sealed class PhysicsTools(IBlenderBridge bridge) : BridgeToolBase(bridge)
{
    [McpServerTool(Name = "blender_add_physics", Destructive = false, OpenWorld = false)]
    [Description(ToolDescriptions.Physics.Add)]
    public Task<CallToolResult> AddAsync(
        [Description(ToolDescriptions.Parameters.ObjectName)] string name,
        [Description(ToolDescriptions.Parameters.PhysicsKind)] string type,
        [Description("Settings by Python name on the physics block, e.g. rigid_body {\"mass\": 2, \"collision_shape\": \"MESH\"}, cloth {\"quality\": 8, \"mass\": 0.3}, fluid_domain {\"domain_type\": \"LIQUID\", \"resolution_max\": 64}, fluid_flow {\"flow_type\": \"LIQUID\", \"flow_behavior\": \"INFLOW\"}.")]
        JsonObject? settings = null,
        CancellationToken cancellationToken = default) =>
        SendAsync(BridgeCommands.AddPhysics, new JsonObject().With("name", name).With("type", type).With("settings", settings), cancellationToken);

    [McpServerTool(Name = "blender_remove_physics", Destructive = true, OpenWorld = false)]
    [Description(ToolDescriptions.Physics.Remove)]
    public Task<CallToolResult> RemoveAsync(
        [Description(ToolDescriptions.Parameters.ObjectName)] string name,
        [Description("Which physics to remove: a kind from blender_add_physics, or particles.")] string type,
        CancellationToken cancellationToken = default) =>
        SendAsync(BridgeCommands.RemovePhysics, new JsonObject().With("name", name).With("type", type), cancellationToken);

    [McpServerTool(Name = "blender_add_particles", Destructive = false, OpenWorld = false)]
    [Description(ToolDescriptions.Physics.Particles)]
    public Task<CallToolResult> ParticlesAsync(
        [Description(ToolDescriptions.Parameters.MeshName)] string name,
        [Description("EMITTER or HAIR.")] string type = "EMITTER",
        [Description("Number of particles or hairs.")] int? count = null,
        [Description("HAIR: length in metres.")] double? hairLength = null,
        [Description("EMITTER: first frame of emission.")] int? frameStart = null,
        [Description("EMITTER: last frame of emission.")] int? frameEnd = null,
        [Description("EMITTER: frames a particle lives.")] int? lifetime = null,
        [Description("Name for the particle system.")] string? systemName = null,
        [Description("Further particle settings by Python name, e.g. {\"render_type\": \"OBJECT\", \"instance_object\": \"Leaf\", \"particle_size\": 0.1, \"normal_factor\": 2}.")]
        JsonObject? settings = null,
        CancellationToken cancellationToken = default) =>
        SendAsync(BridgeCommands.AddParticles,
            new JsonObject().With("name", name).With("type", type).With("count", count).With("hair_length", hairLength).With("frame_start", frameStart)
                .With("frame_end", frameEnd).With("lifetime", lifetime).With("system_name", systemName).With("settings", settings),
            cancellationToken);

    [McpServerTool(Name = "blender_bake_physics", Destructive = false, OpenWorld = false)]
    [Description(ToolDescriptions.Physics.Bake)]
    public Task<CallToolResult> BakeAsync(
        [Description("Last frame to simulate; the scene end otherwise.")] int? frameEnd = null,
        [Description("Discard every cache instead of baking.")] bool free = false,
        [Description(ToolDescriptions.Parameters.TimeoutSeconds)] int timeoutSeconds = 600,
        CancellationToken cancellationToken = default) =>
        WithinAsync(timeoutSeconds, timeout => SendAsync(BridgeCommands.BakePhysics, new JsonObject().With("frame_end", frameEnd).With("free", free), cancellationToken, timeout));
}
