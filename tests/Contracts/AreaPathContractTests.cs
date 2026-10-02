using Snail.MCP.Blender.Application.Storage;

namespace Snail.MCP.Blender.Tests.Contracts;

/// <summary>One reading of a path, checked against the same cases on both sides.</summary>
/// <remarks>The two used to differ in what they trimmed and when they resolved, and the gap between them was how "x/.. " became an area root
/// and took every job in it. A case added here is answered by the server and, in the live suite, by the add-on itself.</remarks>
public class AreaPathContractTests
{
    /// <summary>Every case of the shared file, as both sides must read it.</summary>
    public static IReadOnlyList<(string Path, bool IsConfined, string Why)> Vectors { get; } = Read();

    public static TheoryData<string, bool, string> Cases()
    {
        var cases = new TheoryData<string, bool, string>();

        foreach (var (path, isConfined, why) in Vectors)
        {
            cases.Add(path, isConfined, why);
        }

        return cases;
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Path_AsTheServerReadsIt_MatchesTheSharedCases(string path, bool isConfined, string why) =>
        Assert.True(AreaPaths.IsConfined(path) == isConfined, $"'{path}' should be {(isConfined ? "confined" : "refused")}: {why}");

    /// <summary>The cases as the add-on's own tests run them, as one JSON array.</summary>
    public static string VectorFile() => Path.Combine(Path.GetDirectoryName(SourceFile())!, "area-paths.json");

    private static IReadOnlyList<(string, bool, string)> Read()
    {
        var file = JsonNode.Parse(File.ReadAllText(VectorFile()))!;

        return [.. file["vectors"]!.AsArray().Select(vector =>
            (vector!["path"]!.GetValue<string>(), vector["confined"]!.GetValue<bool>(), vector["why"]!.GetValue<string>()))];
    }

    private static string SourceFile()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "tests", "Contracts", "area-paths.json")))
        {
            directory = directory.Parent;
        }

        return Path.Combine(directory!.FullName, "tests", "Contracts", "area-paths.json");
    }
}
