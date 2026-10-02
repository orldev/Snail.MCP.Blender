namespace Snail.MCP.Blender.Tests.Support;

/// <summary>A tunnel that reports what the test says; null is a Blender on this machine.</summary>
public sealed class FakeTunnel(JsonObject? state = null) : ITunnel
{
    public JsonObject? Describe() => state?.DeepClone().AsObject();
}
