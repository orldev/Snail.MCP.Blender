using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Snail.MCP.Blender.Application.Skills;
using Snail.MCP.Blender.Tools.Prompts;

namespace Snail.MCP.Blender.Tools.Farm;

/// <summary>Whether the frame fits the machine, measured before an hour of rendering finds out.</summary>
[McpServerToolType]
[Skill(Skills.Farm, SkillDescriptions.Farm)]
public sealed class BudgetTools(IBlenderBridge bridge) : BridgeToolBase(bridge)
{
    [McpServerTool(Name = "blender_render_budget", Destructive = false, OpenWorld = false)]
    [Description(ToolDescriptions.Budget.Estimate)]
    public Task<CallToolResult> EstimateAsync(
        [Description("Probe render at low resolution to time the frame and measure peak memory; omit to skip.")] Probe? probe = null,
        [Description("Limits to apply now.")] Limits? apply = null,
        [Description("Prepare the file for a farm.")] FarmPack? packForFarm = null,
        [Description("Memory the render may use in MB; 80 percent of system memory otherwise.")] int? memoryLimitMb = null,
        [Description(ToolDescriptions.Parameters.Scene)] string? scene = null,
        [Description(ToolDescriptions.Parameters.TimeoutSeconds)] int timeoutSeconds = 300,
        CancellationToken cancellationToken = default) =>
        WithinAsync(timeoutSeconds, timeout => SendAsync(BridgeCommands.RenderBudget,
            new JsonObject().With("probe", probe).With("apply", apply).With("pack_for_farm", packForFarm).With("memory_limit_mb", memoryLimitMb).With("scene", scene), cancellationToken, timeout));
}
