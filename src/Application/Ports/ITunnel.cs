namespace Snail.MCP.Blender.Application.Ports;

/// <summary>The tunnel to a Blender on another machine, as the health report shows it; null when Blender runs on this one.</summary>
public interface ITunnel
{
    JsonObject? Describe();
}
