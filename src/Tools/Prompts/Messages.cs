namespace Snail.MCP.Blender.Tools.Prompts;

/// <summary>Human-facing texts: errors and hints in tool responses, seen by the model after the call; tool descriptions live in <see cref="ToolDescriptions"/>.</summary>
public static class Messages
{
    public const string AddOnUnavailableHint =
        "Start Blender and make sure the Snail Bridge extension is installed and listening on the same port: " +
        "blender_install_addon builds the zip, Blender installs it via Preferences → Get Extensions → Install from Disk, " +
        "and the Snail panel in the 3D viewport sidebar (N) shows the state. With Blender on another machine, blender_diagnose shows whether the SSH tunnel to it is up.";

    public const string TimeoutHint =
        "Blender is busy or a modal operator is waiting for input. Finish it in Blender, or pass a longer timeoutSeconds " +
        "for renders and imports.";

    public const string RenderTimeoutHint =
        "The render is still going inside Blender: the wait ended, the render did not, and a command already on Blender's main thread " +
        "cannot be interrupted from outside. blender_diagnose says what Blender is busy with and answers while it is busy. Renders that " +
        "must be stoppable belong in blender_render_job, which renders in a second Blender and blender_render_job_cancel ends; " +
        "blender_render_animation does that by default.";

    public const string BackgroundRenderNote =
        "Rendering in a second Blender: this one stays free, blender_render_job_status follows the frames and blender_render_job_cancel " +
        "stops it. Pass background=false to render here instead, which blocks Blender and cannot be cancelled.";

    /// <summary>What a busy Blender is doing, said where the model asks why nothing answers.</summary>
    public static string BusyWith(string command) =>
        Uninterruptible.Contains(command)
            ? $"Blender is running '{command}' on its main thread and commands queue behind it. It cannot be interrupted from outside: cancelling a call stops the waiting, not the work. blender_render_job renders in a second Blender and can be cancelled."
            : $"Blender is running '{command}' on its main thread; commands queue behind it until it ends.";

    private static bool HoldsBlender(Domain.BridgeError error) =>
        error.Details?["command"]?.GetValue<string>() is { } command && Uninterruptible.Contains(command);

    public const string DroppedHint = "The add-on closed the connection; the next call reconnects on its own.";

    public const string MalformedHint = "Update the add-on in Blender to the version bundled with this server.";

    public const string OperatorHint = "blender_describe_operator lists the operator's properties, their types and defaults.";

    public const string BusyHint =
        "Blender's queue of waiting commands is full; wait for the running command to finish, or move long renders to blender_render_job.";

    public const string UnauthorizedHint =
        "The add-on has a token set in its preferences (Snail Bridge → Token); set SNAIL_MCP_BLENDER_BRIDGE__TOKEN on this server to the same value.";

    public const string PollFailedHint =
        "The operator's poll rejected the context: check the mode, the active object and the selection, " +
        "or pass area=VIEW_3D for editors that need a 3D viewport.";

    public static readonly IReadOnlyList<string> InstallSteps =
    [
        "In Blender open Edit → Preferences → Get Extensions.",
        "Open the drop-down arrow in the top-right corner and choose Install from Disk.",
        "Pick the zip; the extension is enabled automatically.",
        "In the 3D viewport press N, open the Snail tab and check that the bridge is listening.",
    ];

    /// <summary>Commands that hold Blender's main thread for as long as they take and cannot be interrupted from outside; a timeout on one of them means the work goes on.</summary>
    private static readonly HashSet<string> Uninterruptible = new(StringComparer.Ordinal)
    {
        Domain.Commands.BridgeCommands.RenderImage.Name, Domain.Commands.BridgeCommands.RenderAnimation.Name, Domain.Commands.BridgeCommands.RenderObject.Name,
        Domain.Commands.BridgeCommands.BakeTexture.Name, Domain.Commands.BridgeCommands.SequencerRender.Name, Domain.Commands.BridgeCommands.BakePhysics.Name,
    };

    /// <summary>The hint that fits an error of the add-on or of the link; null when there is nothing useful to add.</summary>
    public static string? HintFor(Domain.BridgeError error) => error.Type switch
    {
        Domain.BridgeError.UnavailableType => AddOnUnavailableHint,
        Domain.BridgeError.TimeoutType => HoldsBlender(error) ? RenderTimeoutHint : TimeoutHint,
        Domain.BridgeError.DroppedType => DroppedHint,
        Domain.BridgeError.MalformedType => MalformedHint,
        "UnknownOperator" or "UnknownParameter" => OperatorHint,
        "PollFailed" or "NoArea" => PollFailedHint,
        "Unauthorized" => UnauthorizedHint,
        "UnknownCommand" => StaleAddOnHint,
        Domain.BridgeError.BusyType => BusyHint,
        Domain.BridgeError.TokenUnreadableType => TokenUnreadableHint,
        _ => null,
    };

    public const string ProgramStoppedHint =
        "What the program sent before it stopped stands: read the commands list in the reply, then send a shorter program that carries on from there.";

    public const string TokenUnreadableHint =
        "This server's user needs to read the token file; in Docker both containers read one secret, so its mode must let both of their users read it.";

    public const string StaleAddOnHint =
        "The add-on installed in Blender is older than this server: run blender_install_addon, reinstall the zip it names, and check blender_diagnose, whose addOn block lists what the two sides disagree on.";

    public const string ResolutionTooLarge = "The requested resolution is above the 4096 pixel limit per edge.";

    public const string PythonDisabled = "blender_python is switched off by configuration on this server.";

    public const string HttpTokenMissing =
        "The HTTP server needs the token clients present: set SNAIL_MCP_BLENDER_HTTP__TOKEN, or SNAIL_MCP_BLENDER_HTTP__TOKEN_FILE to a file that holds it.";

    public const string ScopeHint =
        "This is about the key, not the scene: the same call with a key that opens this kind of work will go through. " +
        "An operator mints one with 'snail-mcp-blender clients add <name> --scope <scopes>'.";

    public const string PythonDisabledHint =
        "Use the dedicated tools — blender_find_tool names them and loads their skill — or blender_run_operator, or send many commands at once with blender_run; " +
        "an administrator enables Python with SNAIL_MCP_BLENDER_PYTHON=allow.";

    public const string ScriptOperatorDisabled = "Operators that run scripts are switched off together with blender_python on this server.";

    public const string BatchPythonDisabled = "A batch may not contain the python command while blender_python is switched off.";

    public const string NoProjectNote =
        "The client started this server in the home folder or at the root, which is no project, so downloads land in this server's downloads folder. " +
        "Start the client in the project's folder, or set SNAIL_MCP_BLENDER_PROJECT_DIRECTORY.";

    public const string TunnelDownHint =
        "The SSH tunnel to Blender's machine is not up and the server keeps reopening it; lastError says why: a key the host refuses, " +
        "a host key not trusted yet (connect once by hand with ssh to accept it), or the local port held by another process, such as a " +
        "tunnel an earlier server left behind.";

    public const string UploadOutsideFilesRefused =
        "With blender_python switched off, uploads land only in the add-on's files folder: a file written anywhere else on Blender's machine can be code it runs later.";

    public const string UploadOutsideFilesHint = "Pass a relative remotePath; it lands under the files folder blender_diagnose names.";

    public const string HttpUploadNeedsAPlace =
        "This server is reached over HTTP and sees none of the client's folders, so an upload needs a remotePath inside the files area: the project's folder and a path in it.";

    public const string HttpDownloadOutsideFiles =
        "This server is reached over HTTP and hands out only what lies in the files area, so remotePath must be relative to it, starting with the project's folder.";

    public const string HttpUploadHint = "Name the path the way blender_open_project names the project: robot/textures, robot/renders/shot_0001.png.";

    public const string HttpCommandNote =
        "Nothing has moved yet: run command in a shell on the client's machine, from the folder you work in, before expires. " +
        "The link inside works only for this path and needs no token.";

    public static string DeletedFromThePage(int count, string size) =>
        count == 1 ? $"Deleted one item, {size} freed." : $"Deleted {count} items, {size} freed.";

    public static string CancelledFromThePage(string job) => $"Asked {job} to stop; it ends after the frame it is on.";

    public const string NothingSelected = "Nothing was selected to delete.";

    public const string NotOneName = "A name to delete is one entry of the folder shown, without slashes.";

    public const string FolderOutsideTheArea = "The folder the form names leaves its area; nothing was deleted.";

    public const string HttpProjectNote =
        "This server is reached over HTTP and sees none of the client's folders. blender_open_project with the name of the folder you work in " +
        "opens or creates the project beside Blender, and blender_upload and blender_download return the commands that move files between the two.";

    public const string SequencePythonDisabled = "A sequence may not contain the python command while blender_python is switched off.";

    public const string PythonAdviceNote =
        "These operations have dedicated tools. A tool validates its parameters, respects the leases other agents hold, and lands in the journal with the objects it touched, " +
        "none of which a script does; blender_run sends many of them in one call.";

    public const string PythonCoveredHint =
        "Call the tools the data names — blender_find_tool loads the skills they live in — or send them as one blender_run sequence. " +
        "blender_python stays open for API this server has no tool for; an administrator relaxes this with SNAIL_MCP_BLENDER_PYTHON=allow.";

    public const string NoToolForHint =
        "Ask in English words for what the tool would do, not for an identifier: \"bevel the edges\" rather than \"bmesh.ops.bevel\". " +
        "blender_skills lists the areas the catalogue covers; blender_run_operator reaches any operator Blender has.";

    /// <summary>Why a script was refused where the catalogue already does its work; the covered operations travel with it as data.</summary>
    public static string PythonCovered(int operations) =>
        $"This script does {operations} things this server has tools for, and blender_python is configured as the fallback for what the tools cannot do.";

    public static string StepPythonCovered(int step, int operations) =>
        $"Step {step} is a script that does {operations} things this server has tools for, and blender_python is configured as the fallback for what the tools cannot do; no step was sent.";

    public static string NoToolFor(string intent) => $"No tool of this server matches '{intent}'.";

    public const string TimeoutOutOfRange = "timeoutSeconds is outside the 1 to 600 range this server accepts.";

    public const string TimeoutRangeHint = "Pass a value from 1 to 600; renders longer than that belong in blender_render_job.";

    public const string NoRenderYet = "No render, capture, bake or inspected image has returned a picture in this session yet.";

    public const string NoRenderYetHint = "Call blender_render_image, blender_render_object, blender_viewport_capture or blender_inspect_image first.";

    public const string ResolutionHint = "Keep both edges at 4096 or below; render tiles or upscale outside Blender for more.";

    public static string UnknownSkill(string name) => $"There is no skill named '{name}'.";

    public static string KnownSkills(IEnumerable<string> names) => $"blender_skills lists them: {string.Join(", ", names)}.";
}
