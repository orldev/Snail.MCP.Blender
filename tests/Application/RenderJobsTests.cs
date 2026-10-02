using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using ModelContextProtocol.Server;
using Snail.MCP.Blender.Application.Rendering;
using Snail.MCP.Blender.Application.Sessions;
using Snail.MCP.Blender.Application.Skills;

namespace Snail.MCP.Blender.Tests.Application;

/// <summary>A background render outlives the skill idle timeout, so the skill that started it must survive expiry until the job ends.</summary>
public class RenderJobsTests
{
    private static readonly TimeSpan Idle = TimeSpan.FromMinutes(30);

    [Fact]
    public void Observe_RunningJob_KeepsTheRenderingSkillThroughExpiry()
    {
        var (catalog, jobs, time) = Build();
        catalog.Enable(Skills.Farm);

        jobs.Observe(Reply(new JsonObject { ["id"] = "job-1", ["state"] = "running" }));
        time.Advance(Idle + TimeSpan.FromMinutes(5));

        Assert.Empty(catalog.ExpireIdle(Idle));
        Assert.Equal(["job-1"], jobs.InFlight);
    }

    [Fact]
    public void Observe_JobFinished_ReleasesTheSkill()
    {
        var (catalog, jobs, time) = Build();
        catalog.Enable(Skills.Farm);
        jobs.Observe(Reply(new JsonObject { ["id"] = "job-1", ["state"] = "queued" }));

        jobs.Observe(Reply(new JsonObject { ["id"] = "job-1", ["state"] = "finished" }));
        time.Advance(Idle + TimeSpan.FromMinutes(5));

        Assert.Equal([Skills.Farm], catalog.ExpireIdle(Idle));
        Assert.Empty(jobs.InFlight);
    }

    [Fact]
    public void Observe_JobList_TracksEveryRunningEntry()
    {
        var (_, jobs, _) = Build();

        jobs.Observe(Reply(new JsonObject { ["jobs"] = new JsonArray(new JsonObject { ["id"] = "a", ["state"] = "finished" }, new JsonObject { ["id"] = "b", ["state"] = "running (started by another Blender session)" }) }));

        Assert.Equal(["b"], jobs.InFlight);
    }

    /// <summary>The jobs are Blender's and everyone sees them, but the farm tools a client keeps belong to the client waiting for frames:
    /// counted in one set for everybody, one client's job pinned another's skill and the client that started it was never released.</summary>
    [Fact]
    public void Observe_AJobOfOneClient_DoesNotKeepAnotherClientsSkillLoaded()
    {
        var time = new FakeTimeProvider();
        var skills = new List<Skill> { new(Skills.Farm, "probe", [typeof(ProbeTools)]) };
        var clients = new ClientSessions(skills, [], agent: null, new ServiceCollection().BuildServiceProvider(), time);
        var jobs = new RenderJobs(clients);
        var lighting = clients.Open(new McpServerOptions(), "lighting");
        var modeling = clients.Open(new McpServerOptions(), "modeling");

        lighting.Skills.Enable(Skills.Farm);
        modeling.Skills.Enable(Skills.Farm);

        using (clients.Serve(lighting))
        {
            jobs.Observe(Reply(new JsonObject { ["id"] = "job-1", ["state"] = "running" }));
        }

        using (clients.Serve(modeling))
        {
            jobs.Observe(Reply(new JsonObject { ["jobs"] = new JsonArray() }));
        }

        time.Advance(Idle + TimeSpan.FromMinutes(5));

        Assert.Equal(["job-1"], jobs.InFlight);
        Assert.Empty(lighting.Skills.ExpireIdle(Idle));
        Assert.Equal([Skills.Farm], modeling.Skills.ExpireIdle(Idle));
    }

    [Fact]
    public void Observe_FailedReply_PassesThroughUnchanged()
    {
        var (_, jobs, _) = Build();
        var reply = RecordedAnswers.Reply("the status of a render job that was never queued");

        Assert.Same(reply, jobs.Observe(reply));
        Assert.Empty(jobs.InFlight);
    }

    private static BridgeReply Reply(JsonObject result) => BridgeReply.Ok(result);

    private static (SkillCatalog Catalog, RenderJobs Jobs, FakeTimeProvider Time) Build()
    {
        var time = new FakeTimeProvider();
        var skills = new List<Skill> { new(Skills.Farm, "probe", [typeof(ProbeTools)]) };
        var clients = new ClientSessions(skills, [], agent: null, new ServiceCollection().BuildServiceProvider(), time);

        return (clients.Default.Skills, new RenderJobs(clients), time);
    }

    [McpServerToolType]
    public sealed class ProbeTools
    {
        [McpServerTool(Name = "blender_probe_render")]
        [Description("probe")]
        public string Probe() => "ok";
    }
}
