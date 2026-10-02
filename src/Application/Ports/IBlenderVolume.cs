namespace Snail.MCP.Blender.Application.Ports;

/// <summary>The volume link: a connection of its own for the add-on's data directory, whose commands the add-on answers from its socket thread.</summary>
/// <remarks>A download never waits behind a render on the control link, and a render never waits behind a download.</remarks>
public interface IBlenderVolume
{
    /// <summary>A timeout of its own for the chunks of a large file, which the add-on hashes whole before it answers the last one.</summary>
    Task<BridgeReply> SendAsync(BridgeCommand command, JsonObject parameters, TimeSpan? timeout = null, CancellationToken cancellationToken = default);
}
