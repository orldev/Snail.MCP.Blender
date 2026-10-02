using Snail.MCP.Blender.Application.Sessions;
using Snail.MCP.Blender.Configuration;

namespace Snail.MCP.Blender.Adapters.Blender;

/// <summary>The three links to the add-on, and the one place that decides which of them carries a command.</summary>
/// <remarks>Each channel is a socket of its own, because the add-on answers the control channel on Blender's main thread and the other two
/// from the socket thread: sharing one socket would put a download and a heartbeat in the queue behind a render. Which channel a command
/// belongs to is the command's own, read from the schema both sides share, so a call site that holds only the bridge still sends
/// <c>file_get</c> down the data link — it used to be the port a caller happened to have injected that decided, and the promise held only
/// by convention.</remarks>
public sealed class BlenderLinks(BlenderLinkOptions options, ILogger<BlenderConnection> logger, ClientSessions? clients = null) : IBlenderBridge, IAsyncDisposable
{
    private readonly BlenderConnection _control = new(options, logger, clients);

    private readonly BlenderConnection _data = new(options, logger, clients);

    private readonly BlenderConnection _monitor = new(options, logger, clients);

    /// <summary>Whether the control link is up; the other two connect when something is asked of them.</summary>
    public bool IsConnected => _control.IsConnected;

    /// <summary>Where the token of the last connect came from, which the health report names.</summary>
    public string TokenSource => _control.TokenSource;

    public BlenderConnection Of(BridgeChannel channel) => channel switch
    {
        BridgeChannel.Data => _data,
        BridgeChannel.Monitor => _monitor,
        _ => _control,
    };

    public Task<BridgeReply> SendAsync(
        BridgeCommand command,
        JsonObject? parameters = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default) =>
        Of(command.Channel).SendAsync(command, parameters, timeout, cancellationToken);

    public async ValueTask DisposeAsync()
    {
        await _control.DisposeAsync().ConfigureAwait(false);
        await _data.DisposeAsync().ConfigureAwait(false);
        await _monitor.DisposeAsync().ConfigureAwait(false);
    }
}
