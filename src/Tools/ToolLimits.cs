namespace Snail.MCP.Blender.Tools;

/// <summary>Limits the descriptions promise; guard tests keep the prose and these numbers equal.</summary>
public static class ToolLimits
{
    /// <summary>Longest wait for one command; renders and imports of big files fit, a hung Blender does not hold the client forever.</summary>
    public const int MaxTimeoutSeconds = 600;

    /// <summary>Largest render edge in pixels; above it a single frame can exhaust memory or run for hours on a laptop.</summary>
    public const int MaxRenderResolution = 4096;

    public static TimeSpan Timeout(int seconds) => TimeSpan.FromSeconds(seconds);

    /// <summary>Whether a requested timeout is one the server accepts; out of range is refused with the limit, never clamped in silence.</summary>
    public static bool IsTimeout(int seconds) => seconds is >= 1 and <= MaxTimeoutSeconds;

    public static bool IsRenderable(int? edge) => edge is null or (>= 1 and <= MaxRenderResolution);
}
