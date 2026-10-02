namespace Snail.MCP.Blender.Hosting;

/// <summary>When the last MCP tool was called; the idle watchdog reads it, the transport writes it.</summary>
public interface IActivityTracker
{
    DateTime LastActivityUtc { get; }

    void Touch();
}
