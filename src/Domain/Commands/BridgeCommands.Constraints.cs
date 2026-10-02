namespace Snail.MCP.Blender.Domain.Commands;

/// <summary>Commands of <c>addon/constraints.py</c>: object constraints by RNA.</summary>
public static partial class BridgeCommands
{
    public static readonly BridgeCommand AddConstraint = new("add_constraint", AddOnModules.Constraints);

    public static readonly BridgeCommand UpdateConstraint = new("update_constraint", AddOnModules.Constraints);

    public static readonly BridgeCommand RemoveConstraint = new("remove_constraint", AddOnModules.Constraints);

    public static readonly BridgeCommand DescribeConstraint = new("describe_constraint", AddOnModules.Constraints);
}
