namespace Snail.MCP.Blender.Application.Storage;

/// <summary>Whether a path stays inside an area of the volume, by the rule the add-on applies too.</summary>
/// <remarks>The add-on trims a path and resolves "." and ".." before it acts. A check for exactly ".." let ".. " through, which became ".."
/// over there: the root of an area, or the folder of a running job, was deleted by a name this server had passed. A dot segment is refused
/// whatever surrounds it, on both sides, so the two readings have nothing left to disagree on.</remarks>
public static class AreaPaths
{
    /// <summary>Relative, in either slash, without a drive, a NUL or a segment that is "." or ".." once trimmed.</summary>
    /// <remarks>The other separator is a separator, not a refusal: the add-on reads <c>a\b.png</c> as a file in a folder, and the shared cases
    /// caught this server refusing what the add-on accepted. A UNC name and a drive are still absolute, and both are refused.</remarks>
    public static bool IsConfined(string path)
    {
        var trimmed = path.Trim().Replace('\\', '/');

        return !trimmed.StartsWith('/')
            && !trimmed.StartsWith('~')
            && !trimmed.Contains(':', StringComparison.Ordinal)
            && !trimmed.Contains('\0', StringComparison.Ordinal)
            && !trimmed.Split('/').Any(segment => segment.Trim() is "." or "..");
    }
}
