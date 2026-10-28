using System.Text.RegularExpressions;

namespace Snail.MCP.Blender.Tests.Contracts;

/// <summary>The .NET SDK is pinned in two places, global.json and the image the server is built in, and the two name the same SDK.</summary>
/// <remarks>global.json rolls forward over patches only, so an image on a newer feature band refuses the build with exit code 155 and a
/// line saying no SDK was found — after the image was already pulled, in the one job that builds it. That is how the first push of the
/// repository met them: Dependabot moves global.json and the Dockerfiles as separate ecosystems, and each of its two pull requests failed
/// alone. Here the disagreement is named, in every test run, before an image is built.</remarks>
public partial class ToolchainContractTests
{
    [Fact]
    public void ServerImage_IsBuiltWithTheSdkGlobalJsonAsksFor()
    {
        var wanted = JsonNode.Parse(File.ReadAllText(Path.Combine(Repository.Root, "global.json")))!["sdk"]!["version"]!.GetValue<string>();
        var dockerfile = File.ReadAllText(Path.Combine(Repository.Root, "deploy", "mcp", "Dockerfile"));
        var built = SdkImage().Match(dockerfile);
        var runs = RuntimeImage().Match(dockerfile);

        Assert.True(built.Success, "deploy/mcp/Dockerfile names no mcr.microsoft.com/dotnet/sdk image by version");
        Assert.True(runs.Success, "deploy/mcp/Dockerfile names no mcr.microsoft.com/dotnet/aspnet image by version");
        Assert.True(
            built.Groups["version"].Value == wanted,
            $"global.json asks for SDK {wanted} and deploy/mcp/Dockerfile builds with sdk:{built.Groups["version"].Value}; move both in one change");
        Assert.True(
            MajorMinor(runs.Groups["version"].Value) == MajorMinor(wanted),
            $"the server is built with SDK {wanted} and runs on aspnet:{runs.Groups["version"].Value}, a different .NET");
    }

    /// <summary>Dependabot leaves the SDK band to the standard and follows the build image only inside it.</summary>
    /// <remarks>A multi-ecosystem group meant to move global.json and the image together brought a pull request that moved global.json
    /// alone. The band is shared by every Snail repository and raised by hand in Snail.Sdk first, so Dependabot does not track it.</remarks>
    [Fact]
    public void Dependabot_FollowsTheBuildImageOnlyInsideTheSdkBand()
    {
        var wanted = JsonNode.Parse(File.ReadAllText(Path.Combine(Repository.Root, "global.json")))!["sdk"]!["version"]!.GetValue<string>();
        var entries = File.ReadAllText(Path.Combine(Repository.Root, ".github", "dependabot.yml"))
            .Split("\n  - package-ecosystem:", StringSplitOptions.None)
            .Skip(1)
            .ToList();
        var image = entries.Single(entry => entry.TrimStart().StartsWith("docker", StringComparison.Ordinal) && entry.Contains("/deploy/mcp", StringComparison.Ordinal));
        var ceiling = SdkCeiling().Match(image);

        Assert.DoesNotContain(entries, entry => entry.TrimStart().StartsWith("dotnet-sdk", StringComparison.Ordinal));
        Assert.True(ceiling.Success, "the /deploy/mcp docker updates do not cap the dotnet/sdk image");
        Assert.True(ceiling.Groups["version"].Value == NextBand(wanted), $"global.json is on SDK {wanted}, and the image is capped at {ceiling.Groups["version"].Value} instead of {NextBand(wanted)}");
    }

    private static string MajorMinor(string version) => string.Join('.', version.Split('.').Take(2));

    private static string NextBand(string version) => $"{MajorMinor(version)}.{(int.Parse(version.Split('.')[2], System.Globalization.CultureInfo.InvariantCulture) / 100 + 1) * 100}";

    [GeneratedRegex(@"^FROM mcr\.microsoft\.com/dotnet/sdk:(?<version>\d+\.\d+\.\d+)", RegexOptions.Multiline)]
    private static partial Regex SdkImage();

    [GeneratedRegex(@"^FROM mcr\.microsoft\.com/dotnet/aspnet:(?<version>\d+\.\d+\.\d+)", RegexOptions.Multiline)]
    private static partial Regex RuntimeImage();

    [GeneratedRegex(@"dependency-name:\s*""\*dotnet/sdk""\s*\n\s*versions:\s*\["">=\s*(?<version>\d+\.\d+\.\d+)""\]")]
    private static partial Regex SdkCeiling();
}
