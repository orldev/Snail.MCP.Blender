namespace Snail.MCP.Blender.Application.Ports;

/// <summary>The monitor link, answered from the add-on's socket thread: alive while the control link waits on a render.</summary>
public interface IBlenderMonitor
{
    /// <summary>What the render in flight is doing.</summary>
    Task<BridgeReply> ProbeAsync(CancellationToken cancellationToken = default);

    /// <summary>Whether the add-on answers at all, with its Blender version, its own build and whether the main thread is busy.</summary>
    /// <remarks><c>commands: true</c> asks it to list the command names it registers, which is what a stale install is diagnosed from.</remarks>
    Task<BridgeReply> PingAsync(JsonObject? parameters = null, CancellationToken cancellationToken = default);
}
