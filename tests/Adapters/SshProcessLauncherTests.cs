using Snail.MCP.Blender.Adapters.Ssh;

namespace Snail.MCP.Blender.Tests.Adapters;

/// <summary>The real ssh under the tunnel's supervision: started without this server's standard streams, watched while it runs, and killed with whatever it started.</summary>
/// <remarks>The tunnel itself is tested against a fake launcher; this is the one place the launcher meets the system's ssh, so the seam is
/// known to fit. Nothing here opens a connection: one call asks ssh its version, the other sends it through a proxy command that only sleeps.</remarks>
public class SshProcessLauncherTests
{
    private static readonly SshProcessLauncher Launcher = new(NullLogger<SshProcessLauncher>.Instance);

    [SshFact]
    public async Task AnSshThatEndsByItself_HandsBackItsExitCode_AndTheLastThingItSaid()
    {
        using var ssh = Launcher.Start(["-V"]);

        var exitCode = await ssh.WaitForExitAsync(CancellationToken.None);

        Assert.True(ssh.Id > 0);
        Assert.Equal(0, exitCode);
        Assert.True(await HasSaidSomething(ssh), "ssh said nothing the tunnel could report as the reason it ended");
        Assert.Contains("OpenSSH", ssh.LastLine!, StringComparison.OrdinalIgnoreCase);

        ssh.Kill();
    }

    /// <summary>A tunnel is stopped by killing ssh and everything ssh started: the proxy command is a child process, and leaving it behind would hold the port.</summary>
    [SshFact(needsAProxyCommand: true)]
    public async Task AnSshThatWaitsForever_IsEndedByKill()
    {
        using var ssh = Launcher.Start(["-F", "/dev/null", "-o", "ProxyCommand=sleep 30", "-N", "blender.invalid"]);
        var ending = ssh.WaitForExitAsync(CancellationToken.None);

        Assert.False(ending.IsCompleted);

        ssh.Kill();

        await ending.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.True(ending.IsCompletedSuccessfully);
    }

    private static async Task<bool> HasSaidSomething(ISshProcess ssh)
    {
        for (var attempt = 0; attempt < 50 && ssh.LastLine is null; attempt++)
        {
            await Task.Delay(10);
        }

        return ssh.LastLine is not null;
    }
}

/// <summary>A fact that runs where the system's ssh can be started: it is absent on some Windows machines and in a bare container.</summary>
public sealed class SshFactAttribute : FactAttribute
{
    /// <param name="needsAProxyCommand">The test keeps ssh alive through a shell command and a null device, which only a Unix-like machine has.</param>
    public SshFactAttribute(bool needsAProxyCommand = false)
    {
        if (!Exists())
        {
            Skip = "ssh is not installed on this machine";
        }
        else if (needsAProxyCommand && OperatingSystem.IsWindows())
        {
            Skip = "the proxy command this test keeps ssh busy with is a Unix one";
        }
    }

    private static bool Exists() =>
        (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Any(folder => File.Exists(Path.Combine(folder, OperatingSystem.IsWindows() ? "ssh.exe" : "ssh")));
}
