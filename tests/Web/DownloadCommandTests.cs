using System.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Snail.MCP.Blender.Application.Transfer;

namespace Snail.MCP.Blender.Tests.Web;

/// <summary>The command blender_download hands the agent, run by a real shell against the server: files that exist are kept unless asked, and a
/// file that arrives different from the one on the volume fails the command.</summary>
public sealed class DownloadCommandTests : IAsyncLifetime
{
    private readonly InMemoryVolume _volume = new()
    {
        Files =
        {
            ["files/robot/renders/still.png"] = "frame"u8.ToArray(),
            ["files/robot/renders/turntable/f_0001.png"] = "first"u8.ToArray(),
            ["files/robot/renders/turntable/f_0002.png"] = "second"u8.ToArray(),
        },
    };

    private readonly string _folder = Path.Combine(Path.GetTempPath(), $"snail-download-{Guid.NewGuid():N}");
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

    [Fact]
    public async Task Download_OfAFileThatExists_KeepsItUnlessAskedToReplace()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var local = Path.Combine(_folder, "renders", "still.png");
        Directory.CreateDirectory(Path.GetDirectoryName(local)!);
        await File.WriteAllTextAsync(local, "mine");

        var kept = await RunAsync(Commands().Download("robot/renders/still.png", "renders/still.png", isFolder: false, overwrite: false).Command);
        var keptContent = await File.ReadAllTextAsync(local);
        var replaced = await RunAsync(Commands().Download("robot/renders/still.png", "renders/still.png", isFolder: false, overwrite: true).Command);

        Assert.NotEqual(0, kept.Exit);
        Assert.Contains("overwrite", kept.Errors, StringComparison.Ordinal);
        Assert.Equal("mine", keptContent);
        Assert.Equal(0, replaced.Exit);
        Assert.Equal("frame", await File.ReadAllTextAsync(local));
    }

    [Fact]
    public async Task Download_OfAFolder_KeepsTheFilesThatExist_AndBringsTheRest()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var turntable = Path.Combine(_folder, "renders", "turntable");
        Directory.CreateDirectory(turntable);
        await File.WriteAllTextAsync(Path.Combine(turntable, "f_0001.png"), "mine");

        var kept = await RunAsync(Commands().Download("robot/renders/turntable", "renders/turntable", isFolder: true, overwrite: false).Command);

        Assert.True(kept.Exit == 0, kept.Errors);
        Assert.Equal("mine", await File.ReadAllTextAsync(Path.Combine(turntable, "f_0001.png")));
        Assert.Equal("second", await File.ReadAllTextAsync(Path.Combine(turntable, "f_0002.png")));

        var replaced = await RunAsync(Commands().Download("robot/renders/turntable", "renders/turntable", isFolder: true, overwrite: true).Command);

        Assert.True(replaced.Exit == 0, replaced.Errors);
        Assert.Equal("first", await File.ReadAllTextAsync(Path.Combine(turntable, "f_0001.png")));
    }

    /// <summary>The server checks each file against the digest the add-on computed as it streams it, and breaks the transfer off when they differ;
    /// the command has to fail then rather than leave a damaged file under the frame's name.</summary>
    [Fact]
    public async Task Download_OfAFileThatArrivesDifferent_FailsAndLeavesNothingUnderItsName()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        _volume.Corrupted.Add("files/robot/renders/still.png");
        _volume.Corrupted.Add("files/robot/renders/turntable/f_0002.png");

        var file = await RunAsync(Commands().Download("robot/renders/still.png", "renders/still.png", isFolder: false, overwrite: false).Command);
        var folder = await RunAsync(Commands().Download("robot/renders/turntable", "renders/turntable", isFolder: true, overwrite: false).Command);

        Assert.NotEqual(0, file.Exit);
        Assert.False(File.Exists(Path.Combine(_folder, "renders", "still.png")));
        Assert.Empty(Directory.EnumerateFiles(Path.Combine(_folder, "renders"), "*.part"));
        Assert.NotEqual(0, folder.Exit);
    }

    /// <summary>tar used to unpack straight into the target, so a transfer broken off mid-file left that file cut short under its own name, and a
    /// retry without overwrite kept it and reported success.</summary>
    [Fact]
    public async Task Download_OfAFolderThatBreaksOff_LeavesNoFileBehind_SoARetryBringsItWhole()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var turntable = Path.Combine(_folder, "renders", "turntable");
        _volume.Corrupted.Add("files/robot/renders/turntable/f_0002.png");

        var broken = await RunAsync(Commands().Download("robot/renders/turntable", "renders/turntable", isFolder: true, overwrite: false).Command);
        var leftBehind = File.Exists(Path.Combine(turntable, "f_0002.png"));
        _volume.Corrupted.Clear();
        var retried = await RunAsync(Commands().Download("robot/renders/turntable", "renders/turntable", isFolder: true, overwrite: false).Command);

        Assert.NotEqual(0, broken.Exit);
        Assert.False(leftBehind, "a file cut short stayed under its own name");
        Assert.True(retried.Exit == 0, retried.Errors);
        Assert.Equal("second", await File.ReadAllTextAsync(Path.Combine(turntable, "f_0002.png")));
        Assert.Equal(["f_0001.png", "f_0002.png"], Directory.EnumerateFileSystemEntries(turntable).Select(Path.GetFileName).Order());
    }

    private ClientCommands Commands() => _app.Services.GetRequiredService<ClientCommands>();

    private async Task<(int Exit, string Output, string Errors)> RunAsync(string command)
    {
        var start = new ProcessStartInfo("/bin/sh") { WorkingDirectory = _folder, RedirectStandardOutput = true, RedirectStandardError = true };
        start.ArgumentList.Add("-c");
        start.ArgumentList.Add(command);

        using var shell = Process.Start(start)!;
        var output = await shell.StandardOutput.ReadToEndAsync();
        var errors = await shell.StandardError.ReadToEndAsync();
        await shell.WaitForExitAsync();

        return (shell.ExitCode, output, errors);
    }
}
