using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Snail.MCP.Blender.Application.Diagnostics;

/// <summary>What went over the link since the server started: how often each command ran, how long it took and how it failed.</summary>
/// <remarks>Counters live here rather than in a log so that blender_diagnose can show them to the model; the process is short-lived,
/// so they need no persistence and no reset. The same exchanges are published as metrics for a host that collects them — a container
/// beside Blender runs for weeks, and what a model reads in one call is not what an operator watches over a month.</remarks>
public sealed class BridgeTraffic
{
    /// <summary>The meter the counters are published under.</summary>
    public const string MeterName = "Snail.MCP.Blender";

    private readonly Counter<long>? _exchanges;
    private readonly Histogram<double>? _duration;
    private readonly Dictionary<string, Tally> _commands = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _failures = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Tally> _clients = new(StringComparer.OrdinalIgnoreCase);
    private readonly Lock _gate = new();
    private int _requests;
    private int _programs;
    private int _programCalls;
    private int _abandoned;
    private JsonObject? _abandonedLast;
    private JsonObject? _lastFailure;

    /// <param name="meters">The host's meter factory when it has one; without it the counters are kept but published nowhere.</param>
    public BridgeTraffic(IMeterFactory? meters = null)
    {
        if (meters is null)
        {
            return;
        }

        var meter = meters.Create(MeterName);

        _exchanges = meter.CreateCounter<long>("snail.blender.commands", unit: "{command}", description: "Commands sent to the add-on inside Blender.");
        _duration = meter.CreateHistogram<double>("snail.blender.command.duration", unit: "ms", description: "How long the add-on took to answer.");
    }

    /// <summary>Notes one exchange: the command, how long it took, how it failed, and who it was sent for.</summary>
    /// <remarks>Several clients share one link, so a report that counted only the totals could not say whose the failures were.</remarks>
    public void Record(BridgeCommand command, TimeSpan elapsed, BridgeError? error, string? client = null)
    {
        var tags = new TagList
        {
            { "command", command.Name },
            { "outcome", error is null ? "ok" : error.Type },
            { "client", client ?? "the server" },
        };

        _exchanges?.Add(1, tags);
        _duration?.Record(elapsed.TotalMilliseconds, tags);

        lock (_gate)
        {
            _requests++;

            if (!_commands.TryGetValue(command.Name, out var tally))
            {
                tally = new Tally();
                _commands[command.Name] = tally;
            }

            tally.Note(elapsed, error is not null);

            if (client is not null)
            {
                if (!_clients.TryGetValue(client, out var theirs))
                {
                    theirs = new Tally();
                    _clients[client] = theirs;
                }

                theirs.Note(elapsed, error is not null);
            }

            if (error is null)
            {
                return;
            }

            _failures[error.Type] = _failures.GetValueOrDefault(error.Type) + 1;
            _lastFailure = new JsonObject
            {
                ["command"] = command.Name,
                ["type"] = error.Type,
                ["message"] = error.Message,
                ["at"] = DateTimeOffset.UtcNow.ToString("O"),
            };
        }
    }

    /// <summary>Notes a program and the commands it sent, so the report shows how much of the traffic one call carried.</summary>
    public void Ran(int calls)
    {
        lock (_gate)
        {
            _programs++;
            _programCalls += calls;
        }
    }

    /// <summary>Notes a command the caller gave up on: Blender did not, so the count says how often work was left running unattended.</summary>
    public void Abandon(BridgeCommand command)
    {
        lock (_gate)
        {
            _abandoned++;
            _abandonedLast = new JsonObject
            {
                ["command"] = command.Name,
                ["at"] = DateTimeOffset.UtcNow.ToString("O"),
            };
        }
    }

    /// <summary>The counters as one report: totals, the ten busiest commands and the failures by type.</summary>
    public JsonObject Report()
    {
        lock (_gate)
        {
            var busiest = _commands
                .OrderByDescending(pair => pair.Value.Count)
                .ThenBy(pair => pair.Key, StringComparer.Ordinal)
                .Take(10)
                .Select(pair => (JsonNode)pair.Value.Describe(pair.Key));

            var failures = new JsonObject();

            foreach (var (type, count) in _failures.OrderByDescending(pair => pair.Value).ThenBy(pair => pair.Key, StringComparer.Ordinal))
            {
                failures[type] = count;
            }

            return new JsonObject
            {
                ["requests"] = _requests,
                ["failures"] = _failures.Values.Sum(),
                ["commands"] = new JsonArray([.. busiest]),
                ["failuresByType"] = failures,
                ["lastFailure"] = _lastFailure?.DeepClone(),
                ["clients"] = new JsonArray([.. _clients
                    .OrderByDescending(pair => pair.Value.Count)
                    .ThenBy(pair => pair.Key, StringComparer.Ordinal)
                    .Select(pair => (JsonNode)pair.Value.Describe(pair.Key, "client"))]),
                ["abandoned"] = _abandoned,
                ["lastAbandoned"] = _abandonedLast?.DeepClone(),
                ["escapeHatch"] = EscapeHatch(),
            };
        }
    }

    /// <summary>How much of the traffic went around the catalogue: scripts against the sequences and single calls that did the same work.</summary>
    /// <remarks>The measurement that made the tools worth building is this ratio, so it belongs where the model and the user can both
    /// read it; a share creeping back towards one says the tools are still not being found.</remarks>
    private JsonObject EscapeHatch()
    {
        var python = Count(BridgeCommands.Python.Name);
        var operators = Count(BridgeCommands.RunOperator.Name);

        return new JsonObject
        {
            ["python"] = python,
            ["runOperator"] = operators,
            ["sequences"] = Count(BridgeCommands.Run.Name),
            ["programs"] = _programs,
            ["programCalls"] = _programCalls,
            ["pythonShare"] = _requests == 0 ? 0 : Math.Round((double)python / _requests, 2),
        };
    }

    private int Count(string command) => _commands.TryGetValue(command, out var tally) ? tally.Count : 0;

    private sealed class Tally
    {
        private double _totalMilliseconds;
        private double _slowestMilliseconds;

        public int Count { get; private set; }

        public int Failures { get; private set; }

        public void Note(TimeSpan elapsed, bool failed)
        {
            Count++;
            Failures += failed ? 1 : 0;
            _totalMilliseconds += elapsed.TotalMilliseconds;
            _slowestMilliseconds = Math.Max(_slowestMilliseconds, elapsed.TotalMilliseconds);
        }

        /// <summary>The tally as one entry, under the key the list it belongs to names it by: a command in one, a client in the other.</summary>
        public JsonObject Describe(string name, string key = "command") => new()
        {
            [key] = name,
            ["count"] = Count,
            ["failures"] = Failures,
            ["averageMs"] = Math.Round(_totalMilliseconds / Count, 1),
            ["slowestMs"] = Math.Round(_slowestMilliseconds, 1),
        };
    }
}
