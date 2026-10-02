namespace Snail.MCP.Blender.Tests.Support;

/// <summary>A monitor link that answers from a script, or with nothing at all: what a render watch or a health report sees when it asks.</summary>
public sealed class SilentMonitor : IBlenderMonitor
{
    private readonly Queue<JsonObject> _states = new();
    private BridgeReply _ping = BridgeReply.Failed(new BridgeError("Unavailable", "no monitor"));

    public int Probes { get; private set; }

    public int Pings { get; private set; }

    public SilentMonitor Then(JsonObject state)
    {
        _states.Enqueue(state);

        return this;
    }

    public SilentMonitor Answering(JsonObject ping)
    {
        _ping = BridgeReply.Ok(ping);

        return this;
    }

    public SilentMonitor Failing(BridgeError error)
    {
        _ping = BridgeReply.Failed(error);

        return this;
    }

    public Task<BridgeReply> ProbeAsync(CancellationToken cancellationToken = default)
    {
        Probes++;

        return Task.FromResult(_states.Count > 0 ? BridgeReply.Ok(_states.Dequeue()) : BridgeReply.Failed(new BridgeError("Unavailable", "no monitor")));
    }

    public JsonObject? AskedFor { get; private set; }

    public Task<BridgeReply> PingAsync(JsonObject? parameters = null, CancellationToken cancellationToken = default)
    {
        Pings++;
        AskedFor = parameters;

        return Task.FromResult(_ping);
    }
}
