using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using ModelContextProtocol.Server;
using Snail.MCP.Blender.Application.Sessions;
using Snail.MCP.Blender.Application.Skills;

namespace Snail.MCP.Blender.Tests.Application;

/// <summary>The watchdog that unloads skills nobody uses: it runs on the clock it was given, and a session that keeps working keeps its tools.</summary>
public class SkillExpiryTests
{
    private static readonly TimeSpan Idle = TimeSpan.FromMinutes(30);

    [Fact]
    public async Task ASkillNobodyUsed_IsUnloaded_WhenTheIdleTimeHasPassed()
    {
        var (clients, tools, time) = Build();
        clients.Default.Skills.Enable("probe");
        using var expiry = new SkillExpiry(clients, new SkillExpiryOptions(Idle, TimeSpan.FromMinutes(1)), time, NullLogger<SkillExpiry>.Instance);

        await expiry.StartAsync(CancellationToken.None);
        time.Advance(Idle + TimeSpan.FromMinutes(1));

        Assert.True(await HasHappened(() => tools.Count == 0, time), "the idle skill kept its tools in the model's context");
    }

    /// <summary>Several clients share one server, each with a catalog of its own; a watchdog that watched only the default session would leave their tools loaded forever.</summary>
    [Fact]
    public async Task ASkillLoadedByANamedClient_IsUnloadedToo()
    {
        var (clients, tools, time) = Build();
        var lighting = clients.Open(new McpServerOptions(), "lighting");
        lighting.Skills.Enable("probe");
        using var expiry = new SkillExpiry(clients, new SkillExpiryOptions(Idle, TimeSpan.FromMinutes(1)), time, NullLogger<SkillExpiry>.Instance);

        await expiry.StartAsync(CancellationToken.None);
        time.Advance(Idle + TimeSpan.FromMinutes(1));

        Assert.True(await HasHappened(() => tools.Count == 0, time), "the named client's idle skill kept its tools");
    }

    /// <summary>Zero minutes is how the setting switches the watchdog off; a timer built from it would tick forever over an empty rule.</summary>
    [Fact]
    public async Task AnIdleTimeOfZero_LeavesTheWatchdogAsleep()
    {
        var (clients, tools, time) = Build();
        clients.Default.Skills.Enable("probe");
        using var expiry = new SkillExpiry(clients, SkillExpiryOptions.For(0), time, NullLogger<SkillExpiry>.Instance);

        await expiry.StartAsync(CancellationToken.None);
        time.Advance(TimeSpan.FromHours(8));

        Assert.False(await HasHappened(() => tools.Count == 0, time), "the watchdog ran although it was switched off");
        Assert.True(expiry.ExecuteTask!.IsCompleted);
    }

    [Fact]
    public async Task StoppingTheServer_EndsTheWatchdog_WithoutAFault()
    {
        var (clients, _, time) = Build();
        using var expiry = new SkillExpiry(clients, new SkillExpiryOptions(Idle, TimeSpan.FromMinutes(1)), time, NullLogger<SkillExpiry>.Instance);

        await expiry.StartAsync(CancellationToken.None);
        await expiry.StopAsync(CancellationToken.None);

        Assert.True(expiry.ExecuteTask!.IsCompleted);
        Assert.Null(expiry.ExecuteTask.Exception);
    }

    [Fact]
    public void TheIdleSetting_IsReadAsMinutes_AndCheckedEveryMinute()
    {
        var options = SkillExpiryOptions.For(45);

        Assert.Equal(TimeSpan.FromMinutes(45), options.IdleTimeout);
        Assert.Equal(TimeSpan.FromMinutes(1), options.PollInterval);
    }

    /// <summary>Waits for the watchdog's own thread to act on the clock the test moved, nudging the clock again while it waits.</summary>
    private static async Task<bool> HasHappened(Func<bool> condition, FakeTimeProvider time)
    {
        for (var attempt = 0; attempt < 50; attempt++)
        {
            if (condition())
            {
                return true;
            }

            await Task.Delay(10);
            time.Advance(TimeSpan.FromMinutes(1));
        }

        return condition();
    }

    private static (ClientSessions Clients, McpServerPrimitiveCollection<McpServerTool> Tools, FakeTimeProvider Time) Build()
    {
        var time = new FakeTimeProvider();
        var tools = new McpServerPrimitiveCollection<McpServerTool>();
        var services = new ServiceCollection().AddSingleton(new SkillCatalogTests.Marker("probe-marker")).BuildServiceProvider();
        var skills = SkillCatalog.Discover(typeof(SkillCatalogTests).Assembly);

        return (new ClientSessions(skills, tools, agent: null, services, time), tools, time);
    }
}
