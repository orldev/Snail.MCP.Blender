namespace Snail.MCP.Blender.Domain;

/// <summary>What this server expects of the add-on beyond its commands: the protocol it speaks and the things it must be able to do.</summary>
/// <remarks>The digest already says whether the two sides are the same build, but a difference is not a diagnosis: it cannot say what the
/// difference costs. A number and a list of names can. The number is raised only when a change would break a server written for the one
/// before it, so an add-on ahead of this server is left alone — it speaks everything this server knows and more, and refusing it would make
/// every upgrade a lockstep.
/// <para>The contract test keeps both equal to <c>PROTOCOL</c> and <c>CAPABILITIES</c> in <c>addon/diagnostics.py</c>.</para></remarks>
public static class AddOnProtocol
{
    /// <summary>The protocol this server speaks, and the oldest it works with.</summary>
    public const int Version = 1;

    /// <summary>Answers the file and heartbeat commands off Blender's main thread, so a download never waits behind a render.</summary>
    public const string Channels = "channels";

    /// <summary>Drops a queued request whose caller has stopped waiting instead of running it for nobody.</summary>
    public const string Deadlines = "deadlines";

    /// <summary>Enforces the Python setting and the allowed paths inside Blender, where the power is.</summary>
    public const string Policy = "policy";

    /// <summary>Keeps render jobs as one closed set of states, writes their files whole and logs every frame.</summary>
    public const string JobStates = "job-states";

    /// <summary>Says how long ago Blender's main thread last drained the queue, so a stalled dispatcher is visible from outside.</summary>
    public const string Pulse = "pulse";

    /// <summary>Everything this server counts on the add-on for.</summary>
    public static IReadOnlyList<string> Required { get; } = [Channels, Deadlines, Policy, JobStates, Pulse];
}
