namespace Snail.MCP.Blender.Domain.Commands;

/// <summary>Commands of <c>addon/operators.py</c>: any bpy operator with its documentation, and Python itself.</summary>
public static partial class BridgeCommands
{
    public static readonly BridgeCommand Python = new("python", AddOnModules.Operators);

    public static readonly BridgeCommand RunOperator = new("run_operator", AddOnModules.Operators);

    public static readonly BridgeCommand DescribeOperator = new("describe_operator", AddOnModules.Operators);
}
