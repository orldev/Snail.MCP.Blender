namespace Snail.MCP.Blender.Tools.Prompts;

/// <summary>How to work with this server, delivered once at initialize; a wire contract, edited with caution.</summary>
public static class ServerGuidance
{
    public const string Instructions =
        """
        This server drives a running Blender through its Python add-on. Every tool is named blender_*.
        Start with blender_diagnose: it tells whether the add-on inside Blender answers. When it does not,
        blender_install_addon builds the zip and lists the steps; the user installs it in Blender. Over HTTP the add-on
        comes with the Blender image beside this server instead.
        Read before writing: blender_scene_info, then blender_object_info for the objects you will touch.
        Five layers, from most to least specific: the dedicated tools (blender_add_primitive, blender_transform_object,
        blender_add_light and the rest), which blender_find_tool locates by what you are trying to do — its answer carries each
        tool's command and parameters, enough to call it straight away;
        blender_program for a build of many steps or one that decides as it goes: a small JavaScript program that calls the
        commands itself (blender.add_primitive({...})), with loops and the results in hand, so twenty steps cost one call and one
        reply instead of twenty of each, and only what it returns comes back;
        blender_run to send a list of steps in one call when the list is known in advance
        (a step names the bridge command and its params in snake_case, as the add-on reads them);
        blender_run_operator for any of Blender's operators, with blender_describe_operator for their properties;
        blender_python last, for the API none of those reaches: a script passes no validation, ignores leases and tells the
        journal nothing about what it touched, and its reply names the tools that already do the same work. A program is not a
        script: it reaches the commands only, so leases, the journal and validation hold for every step it sends.
        Deeper work lives in skills: modeling (mesh edits, modifiers, geometry nodes, curves), materials (shaders,
        textures, UVs), animation (keyframes, rigs, constraints, shape keys, drivers), rendering (world, renders,
        product shots, bakes, colour management, output formats, the physical camera, Cycles and EEVEE settings,
        view layers, light linking, camera moves, light rigs), post (compositor, File Output, cryptomatte mattes,
        lens effects and film grain), farm (background render jobs with a queue, the pre-flight check, the memory
        budget), io (import, export, assets from other files), physics (simulations, particles), video (the
        sequence editor: strips, cuts, grading, encoding, mixdown).
        Renders inside a tool call block Blender until they finish; for anything longer than a preview use
        blender_render_job, or blender_render_animation, which does the same by default: both render in a second Blender
        process, and blender_render_job_status follows them.
        blender_render_check before a long render reports what would make it fail or look wrong.
        Renders and captures come back with a small picture and exposure statistics: look at them before judging.
        The prompts photoreal_product_shot, farm_exr_delivery and vse_grade_and_deliver lay out the studio order of
        tools for those tasks.
        Several agents in one Blender: blender_journal shows who changed what, blender_lease claims objects for
        an agent, blender_batch runs command lists in a separate Blender; blender_snapshot before risky edits.
        blender_enable_skill loads a skill and its tools join the list at once; blender_skills shows the state, and the tool
        list holds only the loaded ones, so ask blender_find_tool before concluding that something has no tool. A skill needs
        loading only to call its tools by name: what blender_find_tool answers can be sent through blender_program as it is.
        Skills stay loaded until blender_disable_skill, unless the server is configured to unload idle ones; over HTTP every
        client of this server shares the loaded skills.
        Blender may run on another machine behind an SSH tunnel: every path you pass is a path there, blender_diagnose names
        that machine, and blender_upload and blender_download move files between it and this one. The folder the client works in
        is the project: its twin on Blender's machine is files/<its name>, uploads from the project land there at the same relative
        path and downloads come back to the same place, so name paths inside the twin and the two folders stay mirrored.
        Reached over HTTP, this server runs beside Blender and sees none of the client's folders: begin with blender_open_project
        named after the folder you work in, render into the renders folder it names, and run in your own shell the command that
        blender_upload and blender_download return, which moves the files through a link that expires.
        Rotations are degrees, distances are metres, colours are 0 to 1.
        Every answer is JSON with an ok flag, and a failed call is also flagged isError. When ok is false, read error
        and hint and fix the call instead of retrying it verbatim; data.type names the failure.
        The resources snail://renders/last and snail://journal hold the last picture and the journal for clients that read resources.
        """;
}
