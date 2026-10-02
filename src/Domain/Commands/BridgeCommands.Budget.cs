namespace Snail.MCP.Blender.Domain.Commands;

/// <summary>Commands of <c>addon/budget.py</c>: the memory and time budget of a render, probed at a small size.</summary>
public static partial class BridgeCommands
{
    public static readonly BridgeCommand RenderBudget = new("render_budget", AddOnModules.Budget);
}
