using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Snail.MCP.Blender.Application.Skills;
using Snail.MCP.Blender.Tools.Prompts;

namespace Snail.MCP.Blender.Tools.Modeling;

/// <summary>Modifiers of every type, driven by their Python property names.</summary>
[McpServerToolType]
[Skill(Skills.Modeling, SkillDescriptions.Modeling)]
public sealed class ModifierTools(IBlenderBridge bridge) : BridgeToolBase(bridge)
{
    [McpServerTool(Name = "blender_add_modifier", Destructive = false, OpenWorld = false)]
    [Description(ToolDescriptions.Modifiers.Add)]
    public Task<CallToolResult> AddAsync(
        [Description(ToolDescriptions.Parameters.ObjectName)] string name,
        [Description(ToolDescriptions.Parameters.ModifierType)] string type,
        [Description(ToolDescriptions.Parameters.ModifierSettings)] JsonObject? settings = null,
        [Description("Name for the modifier; the type's label otherwise.")] string? modifierName = null,
        [Description("Apply it to the geometry right away and drop it from the stack.")] bool apply = false,
        CancellationToken cancellationToken = default) =>
        SendAsync(BridgeCommands.AddModifier,
            new JsonObject().With("name", name).With("type", type).With("settings", settings).With("modifier_name", modifierName).With("apply", apply),
            cancellationToken);

    [McpServerTool(Name = "blender_update_modifier", Destructive = false, OpenWorld = false)]
    [Description(ToolDescriptions.Modifiers.Update)]
    public Task<CallToolResult> UpdateAsync(
        [Description(ToolDescriptions.Parameters.ObjectName)] string name,
        [Description(ToolDescriptions.Parameters.ModifierName)] string modifier,
        [Description(ToolDescriptions.Parameters.ModifierSettings)] JsonObject? settings = null,
        [Description("Rename the modifier.")] string? newName = null,
        CancellationToken cancellationToken = default) =>
        SendAsync(BridgeCommands.UpdateModifier,
            new JsonObject().With("name", name).With("modifier", modifier).With("settings", settings).With("new_name", newName),
            cancellationToken);

    [McpServerTool(Name = "blender_remove_modifier", Destructive = true, OpenWorld = false)]
    [Description(ToolDescriptions.Modifiers.Remove)]
    public Task<CallToolResult> RemoveAsync(
        [Description(ToolDescriptions.Parameters.ObjectName)] string name,
        [Description(ToolDescriptions.Parameters.ModifierName)] string modifier,
        CancellationToken cancellationToken = default) =>
        SendAsync(BridgeCommands.RemoveModifier, new JsonObject().With("name", name).With("modifier", modifier), cancellationToken);

    [McpServerTool(Name = "blender_apply_modifier", Destructive = true, OpenWorld = false)]
    [Description(ToolDescriptions.Modifiers.Apply)]
    public Task<CallToolResult> ApplyAsync(
        [Description(ToolDescriptions.Parameters.ObjectName)] string name,
        [Description(ToolDescriptions.Parameters.ModifierName)] string modifier,
        CancellationToken cancellationToken = default) =>
        SendAsync(BridgeCommands.ApplyModifier, new JsonObject().With("name", name).With("modifier", modifier), cancellationToken);

    [McpServerTool(Name = "blender_describe_modifier", ReadOnly = true, OpenWorld = false)]
    [Description(ToolDescriptions.Modifiers.Describe)]
    public Task<CallToolResult> DescribeAsync(
        [Description("Modifier type to document; empty lists every type Blender has.")] string? type = null,
        CancellationToken cancellationToken = default) =>
        SendAsync(BridgeCommands.DescribeModifier, new JsonObject().With("type", type), cancellationToken);
}
