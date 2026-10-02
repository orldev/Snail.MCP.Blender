using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Snail.MCP.Blender.Application.Skills;
using Snail.MCP.Blender.Tools.Prompts;

namespace Snail.MCP.Blender.Tools.Rendering;

/// <summary>What each object contributes to the render: holdout, shadows only, which rays see it, which pass indexes it.</summary>
[McpServerToolType]
[Skill(Skills.Rendering, SkillDescriptions.Rendering)]
public sealed class RenderFlagTools(IBlenderBridge bridge) : BridgeToolBase(bridge)
{
    [McpServerTool(Name = "blender_object_render_flags", Destructive = false, OpenWorld = false)]
    [Description(ToolDescriptions.Flags.ObjectRenderFlags)]
    public Task<CallToolResult> ObjectRenderFlagsAsync(
        [Description("Objects, or collection names to apply to every object inside.")] string[] names,
        [Description("Cut a transparent hole where the object is, for compositing over a plate.")] bool? holdout = null,
        [Description("Render only the shadows and reflections the object receives (Cycles).")] bool? shadowCatcher = null,
        [Description("Ray visibility per ray type.")] RayVisibility? visible = null,
        [Description("Value written to the IndexOB pass; an ID Mask node in blender_compositor isolates it.")] int? passIndex = null,
        [Description("Light group for lights and emissive meshes; empty string removes it.")] string? lightGroup = null,
        [Description("Caustics role.")] CausticsRole? caustics = null,
        [Description("Per-object Cycles motion blur.")] ObjectMotion? motionBlur = null,
        [Description("Shadow terminator offset 0 to 1; hides the shading artefact on low-poly smooth meshes.")] double? shadowTerminatorOffset = null,
        CancellationToken cancellationToken = default) =>
        SendAsync(BridgeCommands.ObjectRenderFlags,
            new JsonObject().With("names", names).With("holdout", holdout).With("shadow_catcher", shadowCatcher).With("visible", visible).With("pass_index", passIndex)
                .With("light_group", lightGroup).With("caustics", caustics).With("motion_blur", motionBlur).With("shadow_terminator_offset", shadowTerminatorOffset),
            cancellationToken);
}
