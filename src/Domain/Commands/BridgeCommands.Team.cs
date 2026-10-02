namespace Snail.MCP.Blender.Domain.Commands;

/// <summary>Commands of <c>addon/team.py</c>: several agents in one Blender: the journal and leases.</summary>
public static partial class BridgeCommands
{
    public static readonly BridgeCommand Journal = new("journal", AddOnModules.Team);

    public static readonly BridgeCommand Lease = new("lease", AddOnModules.Team);
}
