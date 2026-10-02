using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Snail.MCP.Blender.Configuration;
using Snail.MCP.Blender.Tests.Contracts;

namespace Snail.MCP.Blender.Tests.Support;

/// <summary>A background Blender running the add-on from the repository on a free port, driven by <c>Support/headless_driver.py</c>.</summary>
public sealed class HeadlessBlender : IDisposable
{
    private readonly Process _process;

    private HeadlessBlender(Process process, int port, string dataDirectory)
    {
        _process = process;
        Port = port;
        DataDirectory = dataDirectory;
    }

    public int Port { get; }

    /// <summary>The data directory this Blender and the workers it starts write their state into; a fresh one per instance keeps the test journal away from the user's.</summary>
    public string DataDirectory { get; }

    /// <summary>Starts Blender on the add-on from the repository, or on a copy of it under <paramref name="packageName"/>: the folder name an installed extension gets; <paramref name="token"/> makes the bridge demand that token.
    /// <paramref name="commandLine"/> starts it through the <c>snail_bridge</c> command line parser instead, with the port prepended.</summary>
    public static async Task<HeadlessBlender> StartAsync(string? packageName = null, string? token = null, IReadOnlyList<string>? commandLine = null)
    {
        var binary = BlenderInstallation.Find() ?? throw new InvalidOperationException("Blender is not installed");
        var port = FreePort();
        var driverPath = Path.Combine(AppContext.BaseDirectory, "Support", "headless_driver.py");
        var (root, package) = packageName is null ? (RepositoryRoot(), "addon") : (CopyAddOnAs(packageName), packageName);
        var dataDirectory = Path.Combine(Path.GetTempPath(), $"snail-data-{Guid.NewGuid():N}");

        var start = new ProcessStartInfo
        {
            FileName = binary,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        start.Environment["SNAIL_MCP_BLENDER_DATA_DIRECTORY"] = dataDirectory;

        if (token is not null)
        {
            start.Environment["SNAIL_TEST_TOKEN"] = token;
        }

        if (commandLine is not null)
        {
            start.Environment["SNAIL_TEST_COMMAND_LINE"] = string.Join('\n', commandLine);
        }

        foreach (var argument in new[] { "-b", "--factory-startup", "--python", driverPath, "--", root, package, port.ToString() })
        {
            start.ArgumentList.Add(argument);
        }

        var process = Process.Start(start)!;
        var blender = new HeadlessBlender(process, port, dataDirectory);

        await blender.WaitUntilReadyAsync();

        return blender;
    }

    /// <summary>The link options a server would derive: no token configured, so the one the add-on generated is read from the data directory.</summary>
    public BlenderLinkOptions Link => new() { Port = Port, ConnectTimeoutSeconds = 5, RequestTimeoutSeconds = 120, TokenFile = Path.Combine(DataDirectory, "state", "token") };

    private async Task WaitUntilReadyAsync()
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(90));
        _ = Task.Run(async () =>
        {
            while (await _process.StandardError.ReadLineAsync() is not null)
            {
            }
        });

        while (await _process.StandardOutput.ReadLineAsync(deadline.Token) is { } line)
        {
            if (line.Contains("SNAIL_READY", StringComparison.Ordinal) || line.StartsWith("Snail Bridge listening on", StringComparison.Ordinal))
            {
                _ = Task.Run(async () =>
                {
                    while (await _process.StandardOutput.ReadLineAsync() is not null)
                    {
                    }
                });

                return;
            }
        }

        throw new InvalidOperationException("Blender exited before the bridge was ready");
    }

    private static string RepositoryRoot() => Path.GetDirectoryName(AddOnContractTests.AddOnSource())!;

    private static string CopyAddOnAs(string packageName)
    {
        var root = Path.Combine(Path.GetTempPath(), $"snail-package-{Guid.NewGuid():N}");
        var target = Path.Combine(root, packageName);
        Directory.CreateDirectory(target);

        foreach (var file in Directory.EnumerateFiles(AddOnContractTests.AddOnSource()))
        {
            File.Copy(file, Path.Combine(target, Path.GetFileName(file)));
        }

        return root;
    }

    private static int FreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();

        return port;
    }

    public void Dispose()
    {
        if (!_process.HasExited) _process.Kill(entireProcessTree: true);

        _process.Dispose();

        if (Directory.Exists(DataDirectory)) Directory.Delete(DataDirectory, recursive: true);
    }
}
