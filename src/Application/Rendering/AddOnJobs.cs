using System.Text.Json.Serialization.Metadata;
using ModelContextProtocol;
using Snail.Sdk.Mcp.Hosting.Jobs;

namespace Snail.MCP.Blender.Application.Rendering;

/// <summary>A render or a batch the add-on runs in a second Blender, followed here as a job of the server.</summary>
/// <remarks>
/// The add-on owns the work and its state file; this server asks it where the work stands every <see cref="Interval"/>
/// until the state is a terminal one, reports each answer as the job's progress, and ends the job with the last answer
/// as its result. The asking is the server's, not the model's: a client subscribed to the job's resource is told when
/// it ends, and a model reads the resource when it wants. Every answer also passes <see cref="RenderJobs.Observe"/>,
/// which keeps the farm skill loaded while the client's work runs.
/// </remarks>
public sealed class AddOnJobs(IBlenderBridge bridge, Jobs jobs, RenderJobs tracked, TimeProvider clock)
{
    /// <summary>The add-on's answer as the job's result: a JSON object, which the SDK's options know how to write.</summary>
    private static readonly JsonTypeInfo<JsonObject> AnswerType = (JsonTypeInfo<JsonObject>)McpJsonUtilities.DefaultOptions.GetTypeInfo(typeof(JsonObject));

    /// <summary>How often the add-on is asked; long enough that a question never competes with the render for Blender's attention.</summary>
    public TimeSpan Interval { get; init; } = TimeSpan.FromSeconds(2);

    /// <summary>Starts a render with <paramref name="start"/> and follows it with <paramref name="status"/> as a job of the server; a refusal of the add-on is answered as it is.</summary>
    public Task<JobStart> StartRenderAsync(BridgeCommand start, BridgeCommand status, JsonObject arguments, CancellationToken cancellationToken) =>
        StartAsync("render", start, status, arguments, Frames, cancellationToken);

    /// <summary>Starts a batch with <paramref name="start"/> and follows it with <paramref name="status"/> as a job of the server; a refusal of the add-on is answered as it is.</summary>
    public Task<JobStart> StartBatchAsync(BridgeCommand start, BridgeCommand status, JsonObject arguments, CancellationToken cancellationToken) =>
        StartAsync("batch", start, status, arguments, Steps, cancellationToken);

    private async Task<JobStart> StartAsync(string kind, BridgeCommand start, BridgeCommand status, JsonObject arguments, Func<JsonObject, JobProgress> progressOf, CancellationToken cancellationToken)
    {
        var started = tracked.Observe(await bridge.SendAsync(start, arguments, TimeSpan.FromSeconds(120), cancellationToken).ConfigureAwait(false));

        if (!started.IsOk || started.Result is not JsonObject first || first["id"]?.GetValue<string>() is not { } id)
        {
            return new JobStart(null, started);
        }

        var job = jobs.Start(kind, (progress, token) => FollowAsync(status, id, first, progressOf, progress, token), AnswerType);

        return new JobStart(job, started);
    }

    /// <summary>Asks the add-on until the work has ended, reporting each answer; the last answer is the result.</summary>
    private async Task<JsonObject> FollowAsync(BridgeCommand status, string id, JsonObject first, Func<JsonObject, JobProgress> progressOf, IProgress<JobProgress> progress, CancellationToken cancellationToken)
    {
        var last = first;

        progress.Report(progressOf(last));

        while (JobStates.IsActive(last["state"]?.GetValue<string>()))
        {
            await Task.Delay(Interval, clock, cancellationToken).ConfigureAwait(false);
            var asked = tracked.Observe(await bridge.SendAsync(status, new JsonObject { ["id"] = id, ["log_lines"] = 20 }, null, cancellationToken).ConfigureAwait(false));

            if (!asked.IsOk)
            {
                throw new InvalidOperationException(asked.Error?.Message ?? $"the add-on no longer answers for {id}");
            }

            if (asked.Result is JsonObject answer)
            {
                last = answer;
                progress.Report(progressOf(last));
            }
        }

        return Ended(last);
    }

    /// <summary>A job that failed or was cancelled by the add-on ends the server's job the same way, with the add-on's word for it.</summary>
    private static JsonObject Ended(JsonObject last)
    {
        var state = last["state"]?.GetValue<string>();

        if (state == "cancelled")
        {
            throw new OperationCanceledException(last["error"]?.GetValue<string>() ?? "cancelled by the add-on");
        }

        if (state is "failed" or "interrupted")
        {
            throw new InvalidOperationException(last["error"]?.GetValue<string>() ?? $"the add-on reports the job {state}");
        }

        return last;
    }

    private static JobProgress Frames(JsonObject state) =>
        new(state["frames_done"]?.GetValue<double>() ?? 0, state["frames_total"]?.GetValue<double>() is > 0 and var total ? total : null,
            $"{state["state"]?.GetValue<string>()}, frame {state["frame"]?.ToString() ?? "-"}, {state["frames_done"]?.ToString() ?? "0"} written");

    private static JobProgress Steps(JsonObject state) =>
        new(state["steps_done"]?.GetValue<double>() ?? 0, state["steps_total"]?.GetValue<double>() is > 0 and var total ? total : null,
            $"{state["state"]?.GetValue<string>()}, {state["steps_done"]?.ToString() ?? "0"} steps done");
}

/// <summary>What starting a job gives: the job when the add-on took the work, and the add-on's first answer either way.</summary>
/// <param name="Job">The job of the server, null when the add-on refused.</param>
/// <param name="First">The add-on's answer to the start.</param>
public sealed record JobStart(Job? Job, BridgeReply First);
