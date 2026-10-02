namespace Snail.MCP.Blender.Adapters.Blender;

/// <summary>The monitor link: the socket kept for a heartbeat and for progress, asked with a short timeout while the control link waits on a render.</summary>
/// <remarks>The add-on answers ping and progress from the socket thread out of plain dictionaries, so the probe returns even
/// while the main thread is deep in a frame; a separate connection is what keeps it from queueing behind that frame. That
/// is also why the health report pings here: on the control link it would wait for the render to end.</remarks>
public sealed class BlenderMonitor(BlenderLinks links) : IBlenderMonitor
{
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(3);

    public Task<BridgeReply> ProbeAsync(CancellationToken cancellationToken = default) =>
        links.SendAsync(BridgeCommands.Progress, timeout: ProbeTimeout, cancellationToken: cancellationToken);

    public Task<BridgeReply> PingAsync(JsonObject? parameters = null, CancellationToken cancellationToken = default) =>
        links.SendAsync(BridgeCommands.Ping, parameters, ProbeTimeout, cancellationToken);
}
