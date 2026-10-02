namespace Snail.MCP.Blender.Tests.Support;

/// <summary>Where a Blender binary is on this machine, if anywhere; the live tests skip without one.</summary>
public static class BlenderInstallation
{
    public const string PathVariable = "SNAIL_MCP_BLENDER_TESTS_BLENDER";

    private static readonly string[] Candidates =
    [
        "/Applications/Blender.app/Contents/MacOS/Blender",
        "/usr/bin/blender",
        "/usr/local/bin/blender",
        @"C:\Program Files\Blender Foundation\Blender 5.2\blender.exe",
    ];

    public static string? Find()
    {
        var explicitPath = Environment.GetEnvironmentVariable(PathVariable);

        if (!string.IsNullOrWhiteSpace(explicitPath))
        {
            return File.Exists(explicitPath) ? explicitPath : null;
        }

        return Candidates.FirstOrDefault(File.Exists);
    }
}

/// <summary>A fact that runs only where Blender is installed; elsewhere it is reported as skipped, never as green.</summary>
public sealed class BlenderFactAttribute : FactAttribute
{
    public BlenderFactAttribute()
    {
        if (BlenderInstallation.Find() is null)
        {
            Skip = $"Blender is not installed; set {BlenderInstallation.PathVariable} to its binary to run the live tests";
        }
    }
}
