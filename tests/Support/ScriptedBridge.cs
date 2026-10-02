namespace Snail.MCP.Blender.Tests.Support;

/// <summary>An add-on that answers from a script: tests assert which command left with which parameters and feed the reply the real add-on would give.</summary>
public sealed class ScriptedBridge : IBlenderBridge
{
    private readonly Dictionary<string, Func<JsonObject?, BridgeReply>> _answers = new(StringComparer.Ordinal);

    public List<(BridgeCommand Command, JsonObject? Parameters, TimeSpan? Timeout)> Sent { get; } = [];

    public bool IsConnected { get; set; } = true;

    public ScriptedBridge Answer(BridgeCommand command, JsonNode? result)
    {
        _answers[command.Name] = _ => BridgeReply.Ok(result?.DeepClone());

        return this;
    }

    public ScriptedBridge Answer(BridgeCommand command, Func<JsonObject?, JsonNode?> result)
    {
        _answers[command.Name] = parameters => BridgeReply.Ok(result(parameters));

        return this;
    }

    public ScriptedBridge Fail(BridgeCommand command, BridgeError error)
    {
        _answers[command.Name] = _ => BridgeReply.Failed(error);

        return this;
    }

    public Task<BridgeReply> SendAsync(BridgeCommand command, JsonObject? parameters = null, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        Sent.Add((command, parameters, timeout));

        var reply = _answers.TryGetValue(command.Name, out var answer)
            ? answer(parameters)
            : BridgeReply.Failed(new BridgeError("Unscripted", $"no scripted answer for '{command.Name}'"));

        return Task.FromResult(reply);
    }

    public JsonObject? LastParameters => Sent[^1].Parameters;
}
