using Snail.MCP.Blender.Application.Diagnostics;

namespace Snail.MCP.Blender.Tests.Application;

public class BlenderLifetimeTests
{
    private static BlenderLifetime Lifetime() => new(TimeProvider.System, NullLogger<BlenderLifetime>.Instance);

    private static BridgeReply Ping(int pid, JsonObject? crash = null) =>
        BridgeReply.Ok(new JsonObject
        {
            ["blender"] = "5.2.1",
            ["machine"] = new JsonObject { ["platform"] = "linux", ["background"] = true, ["pid"] = pid, ["uptime_s"] = 12.5, ["crash_report"] = crash },
        });

    [Fact]
    public void Describe_WhileTheSameProcessAnswers_ReportsNothing()
    {
        var lifetime = Lifetime();

        lifetime.Seen(Ping(7));
        lifetime.Seen(Ping(7));

        Assert.Null(lifetime.Describe());
    }

    /// <summary>The whole point: a Blender that died and came back answers like the one that died, and only the pid says otherwise.</summary>
    [Fact]
    public void Describe_AfterTheProcessChanged_NamesBothPidsTheCrashReportAndWhatWasLost()
    {
        var lifetime = Lifetime();
        var crash = new JsonObject { ["path"] = "/tmp/00-serdce.crash.txt", ["written_at"] = "2026-10-14 02:41:39", ["age_s"] = 20.0 };

        lifetime.Seen(Ping(7));
        lifetime.Seen(Ping(9, crash));

        var restart = lifetime.Describe()!;

        Assert.Equal(7, restart["previousPid"]!.GetValue<int>());
        Assert.Equal(9, restart["pid"]!.GetValue<int>());
        Assert.Equal("/tmp/00-serdce.crash.txt", restart["crashReport"]!["path"]!.ToString());
        Assert.Contains("file on disk", restart["note"]!.ToString(), StringComparison.Ordinal);
        Assert.False(string.IsNullOrWhiteSpace(restart["at"]!.ToString()));
    }

    /// <summary>A restart stays in the report: the client that has to read the scene again may not be the one that was answered first.</summary>
    [Fact]
    public void Describe_AfterARestartAndFurtherPings_KeepsReportingIt()
    {
        var lifetime = Lifetime();

        lifetime.Seen(Ping(7));
        lifetime.Seen(Ping(9));
        lifetime.Seen(Ping(9));

        Assert.Equal(9, lifetime.Describe()!["pid"]!.GetValue<int>());
    }

    /// <summary>An add-on older than this server names no process, and a server that cannot tell must not claim a restart.</summary>
    [Fact]
    public void Describe_WhenTheAddOnNamesNoProcess_ReportsNothing()
    {
        var lifetime = Lifetime();

        lifetime.Seen(BridgeReply.Ok(new JsonObject { ["machine"] = new JsonObject { ["platform"] = "linux" } }));
        lifetime.Seen(BridgeReply.Ok(new JsonObject { ["machine"] = new JsonObject { ["platform"] = "linux" } }));
        lifetime.Seen(BridgeReply.Failed(new BridgeError("Unavailable", "no answer")));

        Assert.Null(lifetime.Describe());
    }
}
