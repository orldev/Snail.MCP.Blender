namespace Snail.MCP.Blender.Domain;

/// <summary>What came back for one command: a result, or the error that replaced it.</summary>
public sealed record BridgeReply(JsonNode? Result, BridgeError? Error)
{
    public bool IsOk => Error is null;

    public static BridgeReply Ok(JsonNode? result) => new(result, null);

    public static BridgeReply Failed(BridgeError error) => new(null, error);
}
