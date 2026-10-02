using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Snail.MCP.Blender.Tools;

/// <summary>Tells the client whose call changed the tool list, in the reply to that call.</summary>
/// <remarks>The collection announces a change to every session by itself, on a session's own stream. A client of the sessionless protocol
/// revision holds no such stream, and Claude Code over HTTP never saw a skill's tools arrive; the server bound to the request sends the
/// notice into the response the client is already reading. A client that has both hears it twice, which costs one extra tools/list.</remarks>
internal static class ToolListNotice
{
    public static Task SendAsync(McpServer? server, CancellationToken cancellationToken) =>
        server?.SendNotificationAsync(NotificationMethods.ToolListChangedNotification, cancellationToken) ?? Task.CompletedTask;
}
