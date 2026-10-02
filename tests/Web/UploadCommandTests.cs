using System.Diagnostics;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Snail.MCP.Blender.Application.Transfer;

namespace Snail.MCP.Blender.Tests.Web;

/// <summary>The command blender_upload hands the agent, run by a real shell against the server: curl, dd and tar do what the text promises.</summary>
public sealed class UploadCommandTests : IAsyncLifetime
{
    private readonly InMemoryVolume _volume = new();
    private readonly string _folder = Path.Combine(Path.GetTempPath(), $"snail-upload-{Guid.NewGuid():N}");
    private WebApplication _app = null!;

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(_folder);
        _app = await InProcessHttpServer.StartAsync(_volume.AddOn.Port);
    }

    public async Task DisposeAsync()
    {
        await _app.DisposeAsync();
        await _volume.DisposeAsync();
        Directory.Delete(_folder, recursive: true);
    }

    /// <summary>Two and a half parts: the command cuts the file with dd, and the last part carries the digest of the whole.</summary>
    [Fact]
    public async Task Upload_OfAFileLargerThanAPart_ArrivesWhole()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var commands = _app.Services.GetRequiredService<ClientCommands>();
        var bytes = RandomNumberGenerator.GetBytes((commands.UploadPartBytes * 2) + (commands.UploadPartBytes / 2));
        await File.WriteAllBytesAsync(Path.Combine(_folder, "scan.exr"), bytes);

        var output = await RunAsync(commands.Upload("scan.exr", "robot/scans/scan.exr", overwrite: false).Command);

        Assert.Contains($"uploaded {bytes.Length} bytes", output, StringComparison.Ordinal);
        Assert.Equal(bytes, _volume.Files["files/robot/scans/scan.exr"]);
    }

    [Fact]
    public async Task Upload_OfAFolder_UnpacksItsFiles_WithTheirLayout()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        Directory.CreateDirectory(Path.Combine(_folder, "textures", "wood"));
        await File.WriteAllTextAsync(Path.Combine(_folder, "textures", "stone.png"), "stone");
        await File.WriteAllTextAsync(Path.Combine(_folder, "textures", "wood", "oak.png"), "oak");

        await RunAsync(_app.Services.GetRequiredService<ClientCommands>().Upload("textures", "robot/textures", overwrite: false).Command);

        Assert.Equal("stone"u8.ToArray(), _volume.Files["files/robot/textures/stone.png"]);
        Assert.Equal("oak"u8.ToArray(), _volume.Files["files/robot/textures/wood/oak.png"]);
        Assert.DoesNotContain(_volume.Files.Keys, key => key.Contains(".snail-upload-", StringComparison.Ordinal));
    }

    private async Task<string> RunAsync(string command)
    {
        var start = new ProcessStartInfo("/bin/sh") { WorkingDirectory = _folder, RedirectStandardOutput = true, RedirectStandardError = true };
        start.ArgumentList.Add("-c");
        start.ArgumentList.Add(command);

        using var shell = Process.Start(start)!;
        var output = await shell.StandardOutput.ReadToEndAsync();
        var errors = await shell.StandardError.ReadToEndAsync();
        await shell.WaitForExitAsync();

        Assert.True(shell.ExitCode == 0, $"exit {shell.ExitCode}: {errors}");

        return output;
    }
}
