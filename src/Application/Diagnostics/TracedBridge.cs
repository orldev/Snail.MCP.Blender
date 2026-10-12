using System.Diagnostics;
using Snail.MCP.Blender.Application.Sessions;

namespace Snail.MCP.Blender.Application.Diagnostics;

/// <summary>The bridge with a stopwatch around every command: each exchange lands in the log and in <see cref="BridgeTraffic"/>, the reply passes through untouched.</summary>
/// <remarks>Timing sits in front of the link rather than inside it so the TCP client stays about sockets, and so the monitor link,
/// which probes every two seconds during a render, does not inflate the counters.</remarks>
public sealed class TracedBridge(IBlenderBridge link, BridgeTraffic traffic, ILogger<TracedBridge> logger, ClientSessions? clients = null) : IBlenderBridge
{
    public bool IsConnected => link.IsConnected;

    public async Task<BridgeReply> SendAsync(BridgeCommand command, JsonObject? parameters = null, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        var started = Stopwatch.GetTimestamp();
        BridgeReply reply;

        try
        {
            reply = await link.SendAsync(command, parameters, timeout, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            traffic.Abandon(command);
            Log.CommandCancelled(logger, command.Name, Stopwatch.GetElapsedTime(started).TotalMilliseconds);

            throw;
        }

        var elapsed = Stopwatch.GetElapsedTime(started);
        traffic.Record(command, elapsed, reply.Error, clients?.Current.Name);

        if (reply.IsOk)
        {
            Log.CommandAnswered(logger, command.Name, elapsed.TotalMilliseconds);
        }
        else if (reply.Error!.Type == "UnknownCommand")
        {
            Log.CommandUnknownToAddOn(logger, command.Name);
        }
        else
        {
            Log.CommandFailed(logger, command.Name, elapsed.TotalMilliseconds, reply.Error.Type, reply.Error.Message);
        }

        return reply;
    }
}
