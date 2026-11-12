using Microsoft.Extensions.DependencyInjection;
using Snail.MCP.Blender.Tools.Animation;
using Snail.MCP.Blender.Tools.Base;
using Snail.MCP.Blender.Tools.Farm;
using Snail.MCP.Blender.Tools.Io;
using Snail.MCP.Blender.Tools.Materials;
using Snail.MCP.Blender.Tools.Modeling;
using Snail.MCP.Blender.Tools.Physics;
using Snail.MCP.Blender.Tools.Post;
using Snail.MCP.Blender.Tools.Rendering;
using Snail.MCP.Blender.Tools.Video;

namespace Snail.MCP.Blender.Extensions;

/// <summary>Every tool class of the server, the skills' among them, each registered as any other.</summary>
/// <remarks>A class marked with Snail.Sdk's <c>[Skill]</c> is registered like the rest: every tool stays in the server's one collection, so the
/// protocol finds every call and <c>find_tool</c> every tool, and <c>WithSkills</c> leaves out of a session's list the skills it has not loaded.
/// The classes are named one by one rather than found by reflection, which is what a build that trims or compiles ahead of time can follow.</remarks>
internal static class ToolRegistrationExtensions
{
    extension(IMcpServerBuilder builder)
    {
        public IMcpServerBuilder WithBlenderTools() =>
            builder
                .WithBaseTools()
                .WithTools<ConstraintTools>()
                .WithTools<KeyframeTools>()
                .WithTools<MotionTools>()
                .WithTools<RigTools>()
                .WithTools<ShapeKeyTools>()
                .WithTools<BudgetTools>()
                .WithTools<JobTools>()
                .WithTools<IoTools>()
                .WithTools<MaterialTools>()
                .WithTools<NodeTools>()
                .WithTools<TextureTools>()
                .WithTools<CurveTools>()
                .WithTools<GeometryNodeTools>()
                .WithTools<GeometryTools>()
                .WithTools<MeshTools>()
                .WithTools<ModifierTools>()
                .WithTools<PhysicsTools>()
                .WithTools<CompositorTools>()
                .WithTools<PostTools>()
                .WithTools<EngineTools>()
                .WithTools<LayerTools>()
                .WithTools<OpticsTools>()
                .WithTools<OutputTools>()
                .WithTools<RenderFlagTools>()
                .WithTools<RenderTools>()
                .WithTools<ShotTools>()
                .WithTools<StagingTools>()
                .WithTools<DeliveryTools>()
                .WithTools<EditTools>()
                .WithTools<SequencerTools>();

        /// <summary>The tools that belong to no skill: listed to every session from its start.</summary>
        private IMcpServerBuilder WithBaseTools() =>
            builder
                .WithTools<CameraTools>()
                .WithTools<CollectionTools>()
                .WithTools<DiagnosticsTools>()
                .WithTools<FileTools>()
                .WithTools<LightTools>()
                .WithTools<ObjectTools>()
                .WithTools<OperatorTools>()
                .WithTools<SceneTools>()
                .WithTools<SequenceTools>()
                .WithTools<TeamTools>()
                .WithTools<TransferTools>();
    }
}
