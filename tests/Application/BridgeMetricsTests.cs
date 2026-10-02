using System.Diagnostics.Metrics;
using Microsoft.Extensions.DependencyInjection;
using Snail.MCP.Blender.Application.Diagnostics;

namespace Snail.MCP.Blender.Tests.Application;

/// <summary>The same exchanges the report counts are published as metrics, for a host that watches a container for weeks.</summary>
public class BridgeMetricsTests
{
    [Fact]
    public void Command_ThatWasSent_IsCountedUnderItsClientAndOutcome()
    {
        using var services = new ServiceCollection().AddMetrics().BuildServiceProvider();
        var traffic = new BridgeTraffic(services.GetRequiredService<IMeterFactory>());
        var seen = new List<(string Command, string Outcome, string Client)>();
        using var listener = new MeterListener();

        listener.InstrumentPublished = (instrument, heard) =>
        {
            if (instrument.Meter.Name == BridgeTraffic.MeterName && instrument.Name == "snail.blender.commands")
            {
                heard.EnableMeasurementEvents(instrument);
            }
        };

        listener.SetMeasurementEventCallback<long>((_, _, tags, _) =>
            seen.Add((Tag(tags, "command"), Tag(tags, "outcome"), Tag(tags, "client"))));
        listener.Start();

        traffic.Record(BridgeCommands.SceneInfo, TimeSpan.FromMilliseconds(4), null, "lighting");
        traffic.Record(BridgeCommands.ObjectInfo, TimeSpan.FromMilliseconds(2), RecordedAnswers.Error("object_info for a name no object has"), "modeling");

        Assert.Equal([("scene_info", "ok", "lighting"), ("object_info", "NotFound", "modeling")], seen);
    }

    /// <summary>A host without a meter factory — the stdio server — keeps its counters and publishes nowhere, rather than refusing to start.</summary>
    [Fact]
    public void Traffic_WithoutAMeterFactory_StillCounts()
    {
        var traffic = new BridgeTraffic();

        traffic.Record(BridgeCommands.SceneInfo, TimeSpan.FromMilliseconds(4), null, "lighting");

        Assert.Equal(1, traffic.Report()["requests"]!.GetValue<int>());
        Assert.Equal("lighting", traffic.Report()["clients"]!.AsArray()[0]!["client"]!.ToString());
    }

    private static string Tag(ReadOnlySpan<KeyValuePair<string, object?>> tags, string name)
    {
        foreach (var tag in tags)
        {
            if (tag.Key == name)
            {
                return tag.Value?.ToString() ?? string.Empty;
            }
        }

        return string.Empty;
    }
}
