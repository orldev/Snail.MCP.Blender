namespace Snail.MCP.Blender.Tools.Prompts;

/// <summary>How to work with this server, delivered once at initialize; a wire contract, edited with caution.</summary>
/// <remarks>Claude Code keeps the first <see cref="ClientLimit"/> characters of a server's instructions and drops the rest. At 4946 characters
/// the text lost its whole tail in every session: how to reach files over HTTP, the units and what to do with a failed call. What a model
/// needs before its first call therefore comes first, and what a tool's own answer repeats is left to that answer.</remarks>
public static class ServerGuidance
{
    /// <summary>The most a client is known to keep of the instructions.</summary>
    public const int ClientLimit = 4096;

    public const string Instructions =
        """
        This server drives a running Blender through its Python add-on. Every tool is named blender_*, except
        find_tool, run_tools and run_program, which every server of the fleet spells alike.
        Start with blender_diagnose: it tells whether the add-on inside Blender answers and which machine Blender runs on.
        When it does not answer, blender_install_addon builds the zip and lists the steps; over HTTP the add-on comes with
        the Blender image beside this server instead.
        Every answer is JSON with an ok flag, and a failed call is also flagged isError. When ok is false, read error
        and hint and fix the call instead of retrying it verbatim; data.type names the failure.
        Rotations are degrees, distances are metres, colours are 0 to 1.
        Reached over HTTP, this server runs beside Blender and sees none of the client's folders: begin with blender_open_project
        named after the folder you work in, render into the renders folder it names, and run in your own shell the command that
        blender_upload and blender_download return, which moves the files through a link that expires.
        Over stdio Blender may run on another machine behind an SSH tunnel: every path you pass is a path there. The folder the
        client works in is the project, its twin there is files/<its name>, and uploads and downloads keep the two mirrored.
        Read before writing: blender_scene_info, then blender_object_info for the objects you will touch.
        Five layers, from most to least specific: the dedicated tools, which find_tool locates by what you are trying to do —
        its answer carries each tool's command and parameters, enough to call it straight away;
        run_program for a build of many steps or one that decides as it goes: a small JavaScript program that calls the
        commands itself (blender.add_primitive({...})) with loops and the results in hand, so twenty steps cost one call;
        run_tools for a list of steps known in advance (bridge commands, params in snake_case);
        blender_run_operator for any of Blender's operators, with blender_describe_operator for their properties;
        blender_python last, for the API none of those reaches: a script passes no validation, ignores leases and tells the
        journal nothing. A program is not a script: leases, the journal and validation hold for every step it sends.
        Deeper work lives in skills: modeling, materials, animation, rendering, post, farm, io, physics and video.
        blender_enable_skill loads one for this client and its tools join the list; blender_skills shows the state. The list
        holds only loaded tools, so ask find_tool before concluding that something has no tool; what it answers can be sent
        through run_program without loading anything.
        Renders inside a tool call block Blender until they finish; for anything longer than a preview use blender_render_job
        or blender_render_animation, which render in a second Blender process, and follow them with blender_render_job_status.
        blender_render_check before a long render reports what would make it fail or look wrong. Renders and captures come
        back with a small picture and exposure statistics: look at them before judging.
        Several agents in one Blender: blender_journal shows who changed what, blender_lease claims objects for an agent,
        blender_batch runs command lists in a separate Blender; blender_snapshot before risky edits.
        The prompts photoreal_product_shot, farm_exr_delivery and vse_grade_and_deliver lay out the studio order of tools
        for those tasks; the resources snail://renders/last and snail://journal hold the last picture and the journal.
        """;
}
