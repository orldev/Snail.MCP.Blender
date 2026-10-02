namespace Snail.MCP.Blender.Tests.Support;

/// <summary>The checkout the tests read their sources from.</summary>
public static class Repository
{
    /// <summary>Found above the test binary by its <c>src/Tools</c> folder.</summary>
    /// <remarks>Not by the add-on folder: a copy of the add-on is bundled next to the binary, so the bin folder answers to that and a test
    /// reading "the repository" would read the build output instead.</remarks>
    public static string Root
    {
        get
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);

            while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "src", "Tools")))
            {
                directory = directory.Parent;
            }

            return directory?.FullName ?? throw new DirectoryNotFoundException($"src/Tools not found above {AppContext.BaseDirectory}");
        }
    }
}
