namespace Snail.MCP.Blender.Application.Transfer;

/// <summary>Paths on the machine Blender runs on, read without assuming it is this one: a Windows path is absolute there even when this server runs on macOS.</summary>
public static class RemotePaths
{
    /// <summary>Rooted on any of the platforms Blender runs on: <c>/home/…</c>, <c>C:\…</c>, <c>C:/…</c>, <c>\\server\…</c> or <c>~</c>.</summary>
    /// <remarks>Read after trimming, as the add-on reads it: a leading space hid <c>~/…/startup/x.py</c> from this check, and the add-on then
    /// trimmed it and wrote a startup script with Python switched off.</remarks>
    public static bool IsAbsolute(string path)
    {
        var trimmed = path.Trim();

        return trimmed.Length > 0
            && (trimmed[0] is '/' or '\\' or '~' || (trimmed.Length >= 3 && char.IsAsciiLetter(trimmed[0]) && trimmed[1] == ':' && trimmed[2] is '/' or '\\'));
    }

    /// <summary>The last segment, whichever separator the other machine uses.</summary>
    public static string NameOf(string path)
    {
        var trimmed = path.TrimEnd('/', '\\');
        var cut = trimmed.LastIndexOfAny(['/', '\\']);

        return cut < 0 ? trimmed : trimmed[(cut + 1)..];
    }

    /// <summary>A child named in the separator the root already uses, for showing a path the way its own machine writes it.</summary>
    public static string Child(string root, string name) => $"{root.TrimEnd('/', '\\')}{(root.Contains('\\', StringComparison.Ordinal) ? '\\' : '/')}{name}";

    /// <summary>A child path with a forward slash, which Blender's Python accepts on Windows too.</summary>
    public static string Join(string root, string relative) => $"{root.TrimEnd('/', '\\')}/{relative.TrimStart('/', '\\')}";
}
