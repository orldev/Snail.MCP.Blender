namespace Snail.MCP.Blender.Adapters.Blender;

/// <summary>The volume link: the socket the data commands are carried on, with a timeout of its own for a large file.</summary>
public sealed class BlenderVolume(BlenderLinks links) : IBlenderVolume
{
    public Task<BridgeReply> SendAsync(BridgeCommand command, JsonObject parameters, TimeSpan? timeout = null, CancellationToken cancellationToken = default) =>
        links.SendAsync(command, parameters, timeout, cancellationToken);
}
