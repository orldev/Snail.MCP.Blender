using Snail.MCP.Blender.Application.Sessions;

namespace Snail.MCP.Blender.Application.Rendering;

/// <summary>Background renders in flight, so the farm skill stays loaded while one runs and the model can still poll or cancel it.</summary>
/// <remarks>A job outlives any skill expiry the server is configured with: two hours of frames against minutes of idle. The states come from the
/// add-on's replies, which is the only place they are known, so every job tool routes its reply through here. Jobs are counted per client: the
/// jobs are Blender's and everyone sees them, but a tool list belongs to one client, and the client that needs its farm tools kept is the one
/// waiting for frames. Counted in one set for everybody, one client's job pinned another client's skill, and the client that started it was
/// never unpinned.</remarks>
public sealed class RenderJobs(ClientSessions clients)
{
    private readonly Dictionary<ClientSession, HashSet<string>> _inFlight = [];
    private readonly Lock _gate = new();

    /// <summary>Every job any client is still waiting for, by id.</summary>
    public IReadOnlyList<string> InFlight
    {
        get
        {
            lock (_gate)
            {
                return [.. _inFlight.Values.SelectMany(jobs => jobs).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];
            }
        }
    }

    /// <summary>Reads job states out of a reply and pins or releases the farm skill accordingly; the reply passes through untouched.</summary>
    public BridgeReply Observe(BridgeReply reply)
    {
        if (!reply.IsOk || reply.Result is not JsonObject result)
        {
            return reply;
        }

        var jobs = result["jobs"] is JsonArray listed ? listed.OfType<JsonObject>().ToList() : [result];

        var asking = clients.Current;

        lock (_gate)
        {
            var theirs = _inFlight.TryGetValue(asking, out var known) ? known : _inFlight[asking] = new HashSet<string>(StringComparer.Ordinal);

            foreach (var job in jobs)
            {
                Note(theirs, job);
            }

            if (theirs.Count > 0)
            {
                asking.Skills.Pin(Domain.Skills.Farm);
            }
            else
            {
                asking.Skills.Unpin(Domain.Skills.Farm);
            }
        }

        return reply;
    }

    private static void Note(HashSet<string> theirs, JsonObject job)
    {
        if (job["id"]?.GetValue<string>() is not { } id || job["state"]?.GetValue<string>() is not { } state)
        {
            return;
        }

        if (IsInFlight(state))
        {
            theirs.Add(id);
        }
        else
        {
            theirs.Remove(id);
        }
    }

    private static bool IsInFlight(string state) =>
        JobStates.IsActive(state);
}
