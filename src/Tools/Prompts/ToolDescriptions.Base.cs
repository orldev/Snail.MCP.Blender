namespace Snail.MCP.Blender.Tools.Prompts;

public static partial class ToolDescriptions
{
    /// <summary><c>DiagnosticsTools</c>.</summary>
    public static class Diagnostics
    {
        /// <summary><c>blender_diagnose</c></summary>
        public const string Diagnose =
            "Reports the server version, whether the add-on inside Blender answers (with the Blender version and whether it is " +
            "busy), the machine Blender runs on — platform, home and data directory, which is where every path in a tool call " +
            "lives — how clients reach this server (stdio, or HTTP with its public address and how long file links last), the SSH " +
            "tunnel to Blender when the server opens one, the project folder the client works in and its twin over there — over " +
            "HTTP a note instead, since the server sees none of the client's folders — whether the add-on installed in Blender is " +
            "the build this server ships and which commands it lacks if it is not, the effective configuration, the data " +
            "directory, and the traffic over the link since the server started: requests, failures by type, the busiest commands " +
            "with their timings, and how much of that traffic went through blender_python instead of the tools. Call it first when " +
            "a tool answers unexpectedly.";

        /// <summary><c>blender_install_addon</c></summary>
        public const string InstallAddOn =
            "Builds the add-on zip in the data directory and returns the path with the steps to install it in Blender (Preferences " +
            "→ Get Extensions → Install from Disk). Needed once per Blender installation and whenever blender_diagnose reports " +
            "that the installed add-on differs from the one this server ships. Over HTTP the add-on comes with the Blender image " +
            "beside this server, so a mismatch is fixed with the image rather than with this zip.";
    }

    /// <summary><c>BlenderResources</c>: read without a tool call.</summary>
    public static class Resources
    {
        /// <summary><c>snail://renders/last</c></summary>
        public const string LastRender = "The picture the last render, capture, bake or blender_inspect_image returned, as the image it travelled as.";

        /// <summary><c>snail://journal</c></summary>
        public const string Journal = "The add-on's journal of mutating commands with their agents, newest last, as blender_journal returns it.";
    }

    /// <summary><c>SkillTools</c>.</summary>
    public static class Skills
    {
        /// <summary><c>blender_skills</c></summary>
        public const string List =
            "Lists the skills: named groups of tools that are loaded on demand, with what each covers, whether it is " +
            "loaded and the tools it brings.";

        /// <summary><c>blender_enable_skill</c></summary>
        public const string Enable =
            "Loads a skill: its tools appear in the tool list right away (the client is notified). A skill stays " +
            "loaded until blender_disable_skill, unless this server is configured to unload idle ones.";

        /// <summary><c>blender_disable_skill</c></summary>
        public const string Disable = "Unloads a skill, removing its tools from the tool list.";
    }

    /// <summary><c>DiscoveryTools</c>.</summary>
    public static class Discovery
    {
        /// <summary><c>blender_find_tool</c></summary>
        public const string FindTool =
            "Finds the tools for a task. Ask in plain English words for what you are about to do (\"bevel the edges\", \"warm key light\", " +
            "\"render four frames in the background\"); the answer names the tools with their descriptions, the skill each belongs to, the " +
            "command each sends and every parameter it takes. That is enough to call them at once through blender_program, which needs no " +
            "skill loaded; pass enable to put the tools themselves in the tool list instead. Most of this server's tools are not in the tool " +
            "list until their skill is enabled, so a tool you cannot see is not a tool that does not exist: ask here before reaching for " +
            "blender_python.";
    }

    /// <summary><c>TransferTools</c>.</summary>
    public static class Transfer
    {
        /// <summary><c>blender_upload</c></summary>
        public const string Upload =
            "Copies a file, or every file under a folder, from this machine to the machine Blender runs on: textures, HDRIs, " +
            "models and .blend files a remote Blender cannot otherwise see. A relative localPath is read from the project — the " +
            "folder the client works in — and without a remotePath the file lands in the project's twin on Blender's machine at " +
            "the same relative path, so the two folders mirror each other; blender_diagnose names both under project. Sent in " +
            "chunks and checked by SHA-256 before the file takes its name; an existing file is kept unless overwrite. With Blender " +
            "on this same machine none of this is needed. Over HTTP the call copies nothing itself: remotePath is required, " +
            "relative to the files area and starting with the project's folder (robot/textures), and the reply holds a command to " +
            "run in a shell on the client's machine, from the folder you work in, before expires; it sends the file in parts of 16 " +
            "MB, a folder as one tar, checked by SHA-256 at the end.";

        /// <summary><c>blender_download</c></summary>
        public const string Download =
            "Copies a file, or every file under a folder, from the machine Blender runs on to this one: finished frames, render " +
            "job output, saved .blend files. Without a localPath it lands in the project — the folder the client works in — at the " +
            "relative path it had in the project's twin over there, or under its own name when it came from elsewhere; with no " +
            "project, in the downloads folder of this server's data directory. Checked by SHA-256 before the file takes its name, " +
            "and an existing local file is kept unless overwrite. Over HTTP the call copies nothing itself: remotePath must be " +
            "relative to the files area and start with the project's folder, and the reply holds a command to run in a shell on " +
            "the client's machine, from the folder you work in, before expires; it writes to localPath or to the same relative " +
            "place without the project's folder and keeps files that exist unless overwrite, while the server checks every file " +
            "against the SHA-256 the add-on computed and breaks the command off when one differs.";
    }

    /// <summary><c>SequenceTools</c>.</summary>
    public static class Sequence
    {
        /// <summary><c>blender_run</c></summary>
        public const string Run =
            "Runs a list of bridge commands in this Blender, in order, in one call: what to use for a build that takes many steps. " +
            "A step is a command name and its params — the tool name without blender_, so add_primitive, transform_object, " +
            "create_material — with the params named as the add-on reads them, the tool's parameter names in snake_case: " +
            "adaptive_threshold, not adaptiveThreshold. A key the command does not read fails the step as BadRequest, naming the " +
            "key it was probably meant to be, rather than vanishing from it. Each step passes the add-on's own checks, the same " +
            "lease guard and the same journal as a direct tool call. Generate the list with a loop instead of writing a script: " +
            "this, not blender_python, is what makes " +
            "repetitive building cheap. Stops at the first failed step unless told to continue, and returns every step that ran " +
            "with its result or its error; up to 500 steps, whose results are dropped from the reply once it grows past half a " +
            "megabyte, each dropped one marked omitted.";
    }

    /// <summary><c>SceneTools</c>.</summary>
    public static class Scene
    {
        /// <summary><c>blender_scene_info</c></summary>
        public const string Info =
            "The scene at a glance: file path, mode, frame range, render engine and resolution, object counts by type, " +
            "collections, the active and selected objects, the render camera. Start here before changing anything.";

        /// <summary><c>blender_list_objects</c></summary>
        public const string ListObjects =
            "Objects of the scene with type, location, rotation, scale, visibility and selection; filter by type or name.";

        /// <summary><c>blender_object_info</c></summary>
        public const string ObjectInfo =
            "One object in depth: transform, dimensions, parent and children, collections, modifiers, materials, " +
            "visibility and, for meshes, vertex, edge and face counts.";

        /// <summary><c>blender_select_objects</c></summary>
        public const string SelectObjects =
            "Selects objects by name, type or name fragment and makes the last one active. Many operators work on the " +
            "selection, so call this before blender_run_operator when the target matters.";
    }

    /// <summary><c>ObjectTools</c>.</summary>
    public static class Objects
    {
        /// <summary><c>blender_add_primitive</c></summary>
        public const string AddPrimitive =
            "Adds a mesh primitive at a location with rotation, scale and size, names it and makes it active. " +
            "Returns the new object's summary.";

        /// <summary><c>blender_delete_objects</c></summary>
        public const string DeleteObjects = "Deletes objects by name and/or the current selection. Returns what was deleted.";

        /// <summary><c>blender_duplicate_object</c></summary>
        public const string DuplicateObject =
            "Copies an object, its mesh included unless linked=true, into the same collections at an optional offset.";

        /// <summary><c>blender_transform_object</c></summary>
        public const string TransformObject =
            "Sets or, with relative=true, adjusts an object's location, rotation (degrees) and scale.";

        /// <summary><c>blender_update_object</c></summary>
        public const string UpdateObject =
            "Renames an object, hides or shows it in the viewport or in renders, and sets or clears its parent.";

        /// <summary><c>blender_add_text</c></summary>
        public const string AddText = "Adds a 3D text object with a body, size, extrusion, alignment and an optional font file; standing upright by default.";

        /// <summary><c>blender_add_empty</c></summary>
        public const string AddEmpty =
            "Adds an empty: a point with a display shape, used as a parent, a target for constraints or a reference image.";
    }

    /// <summary><c>CameraTools</c>.</summary>
    public static class Camera
    {
        /// <summary><c>blender_add_camera</c></summary>
        public const string AddCamera =
            "Adds a camera, optionally aimed at a point, and makes it the render camera. Defaults to Blender's " +
            "starting camera placement.";

        /// <summary><c>blender_set_camera</c></summary>
        public const string SetCamera =
            "Changes a camera: focal length, sensor, clipping, aim point, and whether it renders; the iris and focus live in blender_set_camera_optics.";
    }

    /// <summary><c>LightTools</c>.</summary>
    public static class Light
    {
        /// <summary><c>blender_add_light</c></summary>
        public const string AddLight =
            "Adds a light of a type with position, power, colour and the type's own settings (cone for SPOT, size for AREA, " +
            "angle for SUN, radius for POINT and SPOT).";

        /// <summary><c>blender_set_light</c></summary>
        public const string SetLight = "Changes a light's power, colour, shadows and type-specific settings.";
    }

    /// <summary><c>CollectionTools</c>.</summary>
    public static class Collections
    {
        /// <summary><c>blender_create_collection</c></summary>
        public const string CreateCollection = "Creates a collection under the scene root or under another collection.";

        /// <summary><c>blender_move_to_collection</c></summary>
        public const string MoveToCollection =
            "Moves objects into a collection, unlinking them from their other collections unless keepOthers=true.";

        /// <summary><c>blender_update_collection</c></summary>
        public const string UpdateCollection = "Hides or shows a collection in viewports and renders, excludes it from the view layer, or renames it.";
    }

    /// <summary><c>SceneTools</c>, scenes of the file.</summary>
    public static class Scenes
    {
        /// <summary><c>blender_scenes</c></summary>
        public const string Manage =
            "Lists, creates, activates, renames or removes scenes of the file: several shots in one .blend, each with its " +
            "own camera, frame range and render settings. A new scene is empty or a linked copy sharing the objects.";
    }

    /// <summary><c>TeamTools</c>.</summary>
    public static class Team
    {
        /// <summary><c>blender_journal</c></summary>
        public const string Journal =
            "What changed in this Blender and by whom: every mutating command with its agent, time, targets and outcome, " +
            "newest last. Read it before touching objects another agent may be working on, and after a batch lands.";

        /// <summary><c>blender_lease</c></summary>
        public const string Lease =
            "Claims objects or collections for this agent for a while: mutating commands from other agents on them are " +
            "refused with Leased until the lease is released or expires. list shows holders; clear drops every lease.";

        /// <summary><c>blender_batch</c></summary>
        public const string Batch =
            "Runs a list of bridge commands in a separate headless Blender on a copy of the file (or a given .blend) and " +
            "saves the result to a .blend: parallel work that does not touch this Blender. Link the output back with " +
            "blender_append_assets. Returns the batch id; poll blender_batch_status.";

        /// <summary><c>blender_batch_status</c></summary>
        public const string BatchStatus =
            "Progress of a batch: state, how many steps finished and the last 20 with their results or errors, the output file and " +
            "the log tail. Without an id, lists recent batches.";
    }

    /// <summary><c>FileTools</c>.</summary>
    public static class Files
    {
        /// <summary><c>blender_undo_redo</c></summary>
        public const string UndoRedo = "Steps back or forward through Blender's undo history.";

        /// <summary><c>blender_save_file</c></summary>
        public const string SaveFile = "Saves the .blend file to its path or to a new one; copy=true writes a copy without switching to it.";

        /// <summary><c>blender_open_file</c></summary>
        public const string OpenFile =
            "Opens a .blend file, replacing the current scene without asking; save first if the current work matters.";

        /// <summary><c>blender_new_file</c></summary>
        public const string NewFile =
            "Starts a new file from the default template, or empty, replacing the current scene without asking.";

        /// <summary><c>blender_open_project</c></summary>
        public const string OpenProject =
            "Opens the project named after the folder the client works in: files/<name>/<name>.blend on Blender's machine, with " +
            "renders and textures folders beside it, all created when the project is new. from=current makes the open scene the " +
            "project's, and is refused when the project already has its .blend. Render into the renders path the reply names and " +
            "upload textures into textures: the project stays one folder, which blender_download brings back whole and, over HTTP, " +
            "the server's pages list and clean. Unless from=current, refuses while the open file has unsaved changes, unless " +
            "discard.";

        /// <summary><c>blender_snapshot</c></summary>
        public const string Snapshot =
            "Saves a copy of the whole file to fall back to, lists the copies, restores one or removes it. A restore replaces " +
            "the current file and saves it back to its own path; a file that was never saved stays at the snapshot path until " +
            "blender_save_file moves it. Take one before a series of edits; cheaper than undo by steps.";
    }

    /// <summary><c>OperatorTools</c>.</summary>
    public static class Operators
    {
        /// <summary><c>blender_run_operator</c></summary>
        public const string RunOperator =
            "Runs any bpy operator with its properties: everything Blender's menus do, thousands of operators. " +
            "Properties are validated against the operator before it runs; blender_describe_operator lists them. " +
            "Mesh editing operators need Edit Mode and usually area=VIEW_3D. Prefer the dedicated tools when one exists.";

        /// <summary><c>blender_describe_operator</c></summary>
        public const string DescribeOperator =
            "Documents an operator: label, description, and every property with type, default, limits and enum values.";

        /// <summary><c>blender_python</c></summary>
        public const string Python =
            "Runs Python inside Blender with bpy available and returns stdout plus the value assigned to result. " +
            "The last resort, and there is a procedure before it: blender_find_tool for what you are about to do, blender_run for a build " +
            "of many steps, blender_run_operator for a single operator. What a script does passes no validation, ignores the leases other " +
            "agents hold and lands in the journal as unguarded, so nobody can see which objects it touched; the reply names the tools that " +
            "already cover what it did. Blender's Python API is fully reachable, which is what this is for: what no tool of this server does.";

        /// <summary><c>blender_program</c></summary>
        public const string Program =
            "Runs a small JavaScript program that calls the commands itself: blender.add_primitive({kind: \"cube\", name: \"Crate\"}) and so on for " +
            "every command of the catalog, with loops, conditions and the results in hand. Use it when a build takes many steps, when each step " +
            "depends on what the last one answered, or when only a summary of a long listing matters: the steps run here, and only what the " +
            "program returns comes back, instead of one call and one reply per step. Return a value from the program; log(...) adds a line to " +
            "the reply. A failed command throws {type, message, details}, which the program may catch and carry on. This is not Blender's " +
            "Python: the program reaches nothing but the commands, and blender_python stays the last resort for what no command does. " +
            "The reply lists every command it sent. Limits: 200 commands, the timeout it was given, and one program's memory.";
    }
}
