namespace Snail.MCP.Blender.Hosting;

/// <summary>How long the server may sit without tool calls and how often that is checked.</summary>
public sealed record IdleShutdownOptions(TimeSpan IdleTimeout, TimeSpan PollInterval)
{
    /// <summary>The watchdog a timeout setting asks for; zero minutes leaves it switched off, and the poll interval stays between one and five minutes.</summary>
    public static IdleShutdownOptions For(int idleTimeoutMinutes) =>
        new(TimeSpan.FromMinutes(idleTimeoutMinutes), TimeSpan.FromMinutes(Math.Clamp(idleTimeoutMinutes, 1, 5)));
}
