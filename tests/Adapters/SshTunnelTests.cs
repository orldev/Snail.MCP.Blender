using Microsoft.Extensions.Time.Testing;
using Snail.MCP.Blender.Adapters.Ssh;
using Snail.MCP.Blender.Configuration;

namespace Snail.MCP.Blender.Tests.Adapters;

/// <summary>The tunnel the server keeps to a Blender on another machine: the command it runs, and how it behaves when ssh ends.</summary>
public sealed class SshTunnelTests
{
    private static BlenderLinkOptions Link(string? host = "artist@203.0.113.42", string? key = "/Users/me/.ssh/id_ed25519") =>
        new() { Port = 9877, Tunnel = { Host = host, Key = key, RemotePort = 9876 } };

    [Fact]
    public void Arguments_ForwardTheLinkPortToTheAddOnsPort_AndNeverAskForAnything()
    {
        var arguments = SshTunnel.Arguments(Link()).ToList();

        Assert.Equal("artist@203.0.113.42", arguments[^1]);
        Assert.Contains("127.0.0.1:9877:127.0.0.1:9876", arguments);
        Assert.Contains("BatchMode=yes", arguments);
        Assert.Contains("ExitOnForwardFailure=yes", arguments);
        Assert.Contains("-n", arguments);
        Assert.Equal("/Users/me/.ssh/id_ed25519", arguments[arguments.IndexOf("-i") + 1]);
    }

    [Fact]
    public void Arguments_WithoutAKey_LeaveTheChoiceToSshItself()
    {
        var arguments = SshTunnel.Arguments(Link(key: null));

        Assert.DoesNotContain("-i", arguments);
        Assert.DoesNotContain("IdentitiesOnly=yes", arguments);
    }

    [Fact]
    public async Task WithoutAHost_NothingStarts_AndTheReportHasNoTunnel()
    {
        var launcher = new ScriptedLauncher();
        using var tunnel = new SshTunnel(Link(host: null), launcher, new FakeTimeProvider(), NullLogger<SshTunnel>.Instance);

        await tunnel.StartAsync(CancellationToken.None);
        await tunnel.StopAsync(CancellationToken.None);

        Assert.Equal(0, launcher.Starts);
        Assert.Null(tunnel.Describe());
    }

    /// <summary>A refused key or a dropped connection is not the end of the tunnel: it is reopened, and the reason stays readable in the health report.</summary>
    [Fact]
    public async Task AnEndedTunnel_IsReopened_AndTheReasonStaysReadable()
    {
        var time = new FakeTimeProvider();
        var launcher = new ScriptedLauncher();
        launcher.Next(exitCode: 255, lastLine: "artist@203.0.113.42: Permission denied (publickey).");
        launcher.Next();
        using var tunnel = new SshTunnel(Link(), launcher, time, NullLogger<SshTunnel>.Instance);

        await tunnel.StartAsync(CancellationToken.None);
        await Until(() => launcher.Starts == 1 && tunnel.Describe()!["up"]!.GetValue<bool>() == false);

        for (var step = 0; step < 50 && launcher.Starts < 2; step++)
        {
            time.Advance(TimeSpan.FromSeconds(1));
            await Task.Delay(10);
        }

        await Until(() => tunnel.Describe()!["up"]!.GetValue<bool>());
        var state = tunnel.Describe()!;

        Assert.Equal(2, state["starts"]!.GetValue<int>());
        Assert.Contains("Permission denied", state["lastError"]!.ToString(), StringComparison.Ordinal);
        Assert.Equal("127.0.0.1:9877 -> 9876", state["forward"]!.ToString());

        await tunnel.StopAsync(CancellationToken.None);

        Assert.True(launcher.Process(1).Killed);
        Assert.False(tunnel.Describe()!["up"]!.GetValue<bool>());
    }

    private static async Task Until(Func<bool> condition)
    {
        for (var attempt = 0; attempt < 300 && !condition(); attempt++)
        {
            await Task.Delay(10);
        }

        Assert.True(condition());
    }

    private sealed class ScriptedLauncher : ISshProcessLauncher
    {
        private readonly Lock _gate = new();
        private readonly Queue<ScriptedProcess> _next = new();
        private readonly List<ScriptedProcess> _started = [];

        public int Starts
        {
            get
            {
                lock (_gate)
                {
                    return _started.Count;
                }
            }
        }

        public ScriptedProcess Process(int index)
        {
            lock (_gate)
            {
                return _started[index];
            }
        }

        public void Next(int? exitCode = null, string? lastLine = null)
        {
            lock (_gate)
            {
                _next.Enqueue(new ScriptedProcess(1000 + _next.Count, exitCode, lastLine));
            }
        }

        public ISshProcess Start(IReadOnlyList<string> arguments)
        {
            lock (_gate)
            {
                var process = _next.Count > 0 ? _next.Dequeue() : new ScriptedProcess(2000 + _started.Count, null, null);
                _started.Add(process);

                return process;
            }
        }
    }

    private sealed class ScriptedProcess(int id, int? exitCode, string? lastLine) : ISshProcess
    {
        private readonly TaskCompletionSource<int> _exit = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int Id => id;

        public string? LastLine => lastLine;

        public bool Killed { get; private set; }

        public Task<int> WaitForExitAsync(CancellationToken cancellationToken) =>
            exitCode is { } code ? Task.FromResult(code) : _exit.Task.WaitAsync(cancellationToken);

        public void Kill()
        {
            Killed = true;
            _exit.TrySetResult(-1);
        }

        public void Dispose()
        {
        }
    }
}
