using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Snail.MCP.Blender.Application.Skills;
using Snail.MCP.Blender.Tools.Prompts;

namespace Snail.MCP.Blender.Tools.Rendering;

/// <summary>How a scene splits into renders: view layers with their collections, passes and mattes, and which lights reach which objects.</summary>
[McpServerToolType]
[Skill(Skills.Rendering, SkillDescriptions.Rendering)]
public sealed class LayerTools(IBlenderBridge bridge) : BridgeToolBase(bridge)
{
    [McpServerTool(Name = "blender_view_layer", Destructive = false, OpenWorld = false)]
    [Description(ToolDescriptions.Layers.ViewLayer)]
    public Task<CallToolResult> ViewLayerAsync(
        [Description("configure (the default; creates the layer when the name is new), create, activate, list, remove.")] string action = "configure",
        [Description("View layer name; the active one otherwise.")] string? name = null,
        [Description("Rename the layer.")] string? newName = null,
        [Description("Render this layer; false skips it in renders.")] bool? use = null,
        [Description("Per-layer sample override for Cycles; 0 uses the scene samples.")] int? samples = null,
        [Description("Which collections the layer renders, holds out or keeps for bounce light only.")] LayerCollections? collections = null,
        [Description("Passes to switch: {\"z\": true, \"normal\": true, \"mist\": true, \"vector\": true, \"ambient_occlusion\": true, \"diffuse_direct\": true, \"shadow_catcher\": true, \"cryptomatte_object\": true}.")] JsonObject? passes = null,
        [Description("Cryptomatte passes.")] Cryptomatte? cryptomatte = null,
        [Description("AOVs to create or keep: [\"Mask\"] or [{\"name\": \"Wetness\", \"type\": \"VALUE\"}]; shaders write them through an AOV Output node.")] JsonArray? aovs = null,
        [Description("AOV names to remove.")] string[]? removeAovs = null,
        [Description("Light groups to create: [\"Key\", \"Fill\", \"Rim\"]; each renders as its own Combined pass.")] string[]? lightGroups = null,
        [Description("Light group names to remove.")] string[]? removeLightGroups = null,
        [Description("Which lights belong to which group: {\"Key\": [\"Sun\"], \"Fill\": [\"Area\", \"World\"]}.")] JsonObject? lightGroupMembers = null,
        [Description("Material name that overrides every material on this layer (clay renders); empty removes it.")] string? materialOverride = null,
        [Description("Render the layer with Freestyle lines.")] bool? freestyle = null,
        [Description(ToolDescriptions.Parameters.Scene)] string? scene = null,
        CancellationToken cancellationToken = default) =>
        SendAsync(BridgeCommands.ViewLayer,
            new JsonObject().With("action", action).With("name", name).With("new_name", newName).With("use", use).With("samples", samples).With("collections", collections)
                .With("passes", passes).With("cryptomatte", cryptomatte).With("aovs", aovs).With("remove_aovs", removeAovs).With("light_groups", lightGroups)
                .With("remove_light_groups", removeLightGroups).With("light_group_members", lightGroupMembers).With("material_override", materialOverride)
                .With("freestyle", freestyle).With("scene", scene),
            cancellationToken);

    [McpServerTool(Name = "blender_light_linking", Destructive = false, OpenWorld = false)]
    [Description(ToolDescriptions.Layers.LightLinking)]
    public Task<CallToolResult> LightLinkingAsync(
        [Description("The light object (or emissive mesh) whose reach changes.")] string light,
        [Description("Objects or collections that receive its light.")] string[]? receivers = null,
        [Description("include: the light reaches only the receivers; exclude: everything except them.")] string receiverMode = "include",
        [Description("Objects or collections that cast its shadow.")] string[]? blockers = null,
        [Description("include: only the blockers cast this light's shadow; exclude: everything except them.")] string blockerMode = "include",
        [Description("Remove the light's receiver and blocker collections first.")] bool clear = false,
        CancellationToken cancellationToken = default) =>
        SendAsync(BridgeCommands.LightLinking,
            new JsonObject().With("light", light).With("receivers", receivers).With("receiver_mode", receiverMode).With("blockers", blockers)
                .With("blocker_mode", blockerMode).With("clear", clear),
            cancellationToken);
}
