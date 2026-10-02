using Snail.MCP.Blender.Configuration;

namespace Snail.MCP.Blender.Application.Transfer;

/// <summary>The folder the client works in, and its twin on Blender's machine: where files come from and go back to when nobody names a place.</summary>
/// <remarks>The client starts the server in the folder it works in, so that folder is the project without anyone naming it. Its twin is the
/// add-on's files/&lt;folder name&gt;, so an upload from inside the project lands in the twin at the same relative path and a download from the
/// twin comes back to the same place — the two folders mirror each other. The home folder and the filesystem root are no project: a client
/// started there would scatter renders across them, so downloads fall back to the server's own downloads folder.</remarks>
public sealed class LocalProject(ServerConfig config)
{
    /// <summary>The project folder, or null when the server runs in the home folder, at the root, or with no folder configured.</summary>
    public string? Directory => Usable(config.ProjectDirectory);

    public string? Name => Directory is { } directory ? Path.GetFileName(Path.TrimEndingDirectorySeparator(directory)) : null;

    /// <summary>A local path as given: absolute, or relative to the project and, without one, to the server's working directory.</summary>
    public string Local(string path) => Path.GetFullPath(path, Directory ?? Environment.CurrentDirectory);

    /// <summary>Where an upload lands when the caller names no target: in the twin at the same relative path when it comes from inside the project, under its own name otherwise.</summary>
    public string UploadTarget(string local)
    {
        var own = RemotePaths.NameOf(local);

        if (Directory is not { } directory || Name is not { } project)
        {
            return own;
        }

        var relative = Path.GetRelativePath(directory, local);

        return relative == "."
            ? project
            : relative.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(relative)
                ? own
                : RemotePaths.Join(project, relative.Replace(Path.DirectorySeparatorChar, '/'));
    }

    /// <summary>Where a download lands when the caller names no place: back at the same relative path when it comes from the twin, in the project under its own name when it comes from elsewhere, in the server's downloads folder when there is no project.</summary>
    public string DownloadTarget(string remote)
    {
        if (Directory is not { } directory || Name is not { } project)
        {
            return Path.Combine(config.DataDirectory, "downloads", RemotePaths.NameOf(remote));
        }

        if (!RemotePaths.IsAbsolute(remote))
        {
            var segments = remote.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);

            if (segments.Length > 0 && string.Equals(segments[0], project, StringComparison.OrdinalIgnoreCase))
            {
                return segments.Length == 1 ? directory : Path.Combine([directory, .. segments[1..]]);
            }
        }

        return Path.Combine(directory, RemotePaths.NameOf(remote));
    }

    /// <summary>The project and its twin, the latter named on Blender's machine when the add-on said where its files folder is.</summary>
    public JsonObject Describe(string? remoteFiles) => new()
    {
        ["directory"] = Directory,
        ["name"] = Name,
        ["twin"] = Name is { } name && remoteFiles is not null ? RemotePaths.Child(remoteFiles, name) : null,
        ["downloads"] = Directory ?? Path.Combine(config.DataDirectory, "downloads"),
    };

    private static string? Usable(string? directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
        {
            return null;
        }

        var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory));
        var home = Path.TrimEndingDirectorySeparator(ServerPaths.Home);

        return string.Equals(full, home, StringComparison.OrdinalIgnoreCase) || string.Equals(full, Path.TrimEndingDirectorySeparator(Path.GetPathRoot(full) ?? full), StringComparison.OrdinalIgnoreCase)
            ? null
            : full;
    }
}
