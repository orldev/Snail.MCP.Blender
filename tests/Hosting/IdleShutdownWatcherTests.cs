using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Time.Testing;
using Snail.MCP.Blender.Hosting;

namespace Snail.MCP.Blender.Tests.Hosting;

public class IdleShutdownWatcherTests
{
    private static readonly TimeSpan Poll = TimeSpan.FromMinutes(5);

    private static readonly IdleShutdownOptions Options = new(TimeSpan.FromMinutes(60), Poll);

    [Fact]
    public async Task Watcher_NothingHappens_StopsTheHost()
    {
        var (time, lifetime, _, watcher) = Build();

        await watcher.StartAsync(CancellationToken.None);
        await AdvanceAsync(time, TimeSpan.FromMinutes(65));

        Assert.True(lifetime.Stopped);

        await watcher.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Watcher_ToolCallBeforeTheDeadline_RestartsTheCountdown()
    {
        var (time, lifetime, activity, watcher) = Build();

        await watcher.StartAsync(CancellationToken.None);
        await AdvanceAsync(time, TimeSpan.FromMinutes(50));
        activity.Touch();

        await AdvanceAsync(time, TimeSpan.FromMinutes(50));
        Assert.False(lifetime.Stopped);

        await AdvanceAsync(time, TimeSpan.FromMinutes(15));
        Assert.True(lifetime.Stopped);

        await watcher.StopAsync(CancellationToken.None);
    }

    /// <summary>Advances one poll at a time, giving the watcher's loop a real moment to consume each tick.</summary>
    private static async Task AdvanceAsync(FakeTimeProvider time, TimeSpan total)
    {
        for (var elapsed = TimeSpan.Zero; elapsed < total; elapsed += Poll)
        {
            time.Advance(Poll);
            await Task.Delay(15);
        }
    }

    private static (FakeTimeProvider Time, FakeLifetime Lifetime, ActivityTracker Activity, IdleShutdownWatcher Watcher) Build()
    {
        var time = new FakeTimeProvider();
        var lifetime = new FakeLifetime();
        var activity = new ActivityTracker(time);
        var watcher = new IdleShutdownWatcher(
            activity,
            Options,
            lifetime,
            time,
            NullLogger<IdleShutdownWatcher>.Instance);

        return (time, lifetime, activity, watcher);
    }

    private sealed class FakeLifetime : IHostApplicationLifetime
    {
        private readonly CancellationTokenSource _stopping = new();

        public bool Stopped { get; private set; }

        public CancellationToken ApplicationStarted => CancellationToken.None;

        public CancellationToken ApplicationStopping => _stopping.Token;

        public CancellationToken ApplicationStopped => CancellationToken.None;

        public void StopApplication() => Stopped = true;
    }
}
