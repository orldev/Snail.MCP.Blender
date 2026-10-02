namespace Snail.MCP.Blender.Application.Ports;

/// <summary>The link to the add-on running inside Blender: one command in, one reply out.</summary>
public interface IBlenderBridge
{
    bool IsConnected { get; }

    /// <summary>Sends a command and waits for its reply; link failures come back as a <see cref="BridgeError"/>, never as an exception.</summary>
    Task<BridgeReply> SendAsync(
        BridgeCommand command,
        JsonObject? parameters = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default);
}
