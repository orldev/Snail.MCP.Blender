using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Snail.MCP.Blender.Application.Skills;
using Snail.MCP.Blender.Tools.Prompts;

namespace Snail.MCP.Blender.Tools.Post;

/// <summary>The lens artefacts added after the render: the post stack on the beauty pass.</summary>
[McpServerToolType]
[Skill(Skills.Post, SkillDescriptions.Post)]
public sealed class PostTools(IBlenderBridge bridge) : BridgeToolBase(bridge)
{
    [McpServerTool(Name = "blender_lens_effects", Destructive = false, OpenWorld = false)]
    [Description(ToolDescriptions.Lens.Effects)]
    public Task<CallToolResult> EffectsAsync(
        [Description("Denoise node at the head of the stack.")] Denoise? denoise = null,
        [Description("Depth of field from the Z pass.")] Defocus? defocus = null,
        [Description("Motion blur from the Vector pass; scene motion blur must be off.")] VectorBlur? vectorBlur = null,
        [Description("Glare: bloom, streaks, ghosts.")] Glare? glare = null,
        [Description("Halation, the red halo of film around highlights.")] Halation? halation = null,
        [Description("Chromatic aberration and barrel distortion.")] ChromaticAberration? chromaticAberration = null,
        [Description("Darkened corners.")] Vignette? vignette = null,
        [Description("Film grain, animated and weighted by luminance.")] Grain? grain = null,
        [Description("Remove the stack and wire Render Layers straight to the output; the only way to take it down, since a call naming no effect is refused.")] bool clear = false,
        [Description(ToolDescriptions.Parameters.Scene)] string? scene = null,
        CancellationToken cancellationToken = default) =>
        SendAsync(BridgeCommands.LensEffects,
            new JsonObject().With("denoise", denoise).With("defocus", defocus).With("vector_blur", vectorBlur).With("glare", glare).With("halation", halation)
                .With("chromatic_aberration", chromaticAberration).With("vignette", vignette).With("grain", grain).With("clear", clear).With("scene", scene),
            cancellationToken);
}
