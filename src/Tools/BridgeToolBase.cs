using ModelContextProtocol.Protocol;
using Snail.MCP.Blender.Tools.Prompts;

namespace Snail.MCP.Blender.Tools;

/// <summary>Shared base of add-on-backed tools: sends one command and turns the reply into a tool result.</summary>
public abstract class BridgeToolBase(IBlenderBridge bridge)
{
    protected async Task<CallToolResult> SendAsync(BridgeCommand command, JsonObject arguments, CancellationToken cancellationToken, TimeSpan? timeout = null) =>
        ToolResponse.From(await bridge.SendAsync(command, arguments, timeout, cancellationToken).ConfigureAwait(false));

    /// <summary>Runs <paramref name="send"/> with the requested timeout, or refuses a value outside the range the descriptions promise.</summary>
    protected static Task<CallToolResult> WithinAsync(int timeoutSeconds, Func<TimeSpan, Task<CallToolResult>> send) =>
        ToolLimits.IsTimeout(timeoutSeconds)
            ? send(ToolLimits.Timeout(timeoutSeconds))
            : Task.FromResult(ToolResponse.Failure(Messages.TimeoutOutOfRange, Messages.TimeoutRangeHint));

    /// <summary>The raw reply, for tools that watch progress while it is on its way or turn a preview into image content.</summary>
    protected Task<BridgeReply> ExchangeAsync(BridgeCommand command, JsonObject arguments, TimeSpan? timeout, CancellationToken cancellationToken) =>
        bridge.SendAsync(command, arguments, timeout, cancellationToken);
}
