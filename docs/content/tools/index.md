# Tools

149 tools in 44 families: 42 always on, the rest in skills that load on demand.
The names, descriptions and schemas are generated from the server itself, so they are exactly what the model receives in `tools/list`.

Skills load with `blender_enable_skill`, or through `find_tool` for the tools a search returns. A loaded skill stays loaded until `blender_disable_skill`, unless `SNAIL_MCP_BLENDER_SKILL_IDLE_MINUTES` unloads idle ones; `farm` stays while a render job runs. The table shows the first sentence of each description.

| Tool | Family | Skill | What it does |
| --- | --- | --- | --- |
| [`blender_diagnose`](diagnostics.md#blender_diagnose) | Diagnostics | — | Reports the server version, whether the add-on inside Blender answers (with the Blender version and whether it is busy), the machine Blender runs on — platfo… |
| [`blender_install_addon`](diagnostics.md#blender_install_addon) | Diagnostics | — | Builds the add-on zip in the data directory and returns the path with the steps to install it in Blender (Preferences → Get Extensions → Install from Disk). |
| [`blender_skills`](skills.md#blender_skills) | Skills | — | Lists the skills: named groups of tools that are loaded on demand, with what each covers, whether it is loaded and the tools it brings. |
| [`blender_enable_skill`](skills.md#blender_enable_skill) | Skills | — | Loads a skill: its tools appear in the tool list right away (the client is notified). |
| [`blender_disable_skill`](skills.md#blender_disable_skill) | Skills | — | Unloads a skill, removing its tools from the tool list. |
| [`find_tool`](discovery.md#find_tool) | Finding a tool | — | Finds the tools for a task. |
| [`run_program`](programs.md#run_program) | Programs | — | Runs a small JavaScript program that calls the commands itself: blender.add_primitive({kind: "cube", name: "Crate"}) and so on for every command of the catal… |
| [`run_tools`](sequence.md#run_tools) | Sequences | — | Runs a list of bridge commands in this Blender, in order, in one call: what to use for a build that takes many steps. |
| [`blender_upload`](transfer.md#blender_upload) | Files between machines | — | Copies a file, or every file under a folder, from this machine to the machine Blender runs on: textures, HDRIs, models and .blend files a remote Blender cann… |
| [`blender_download`](transfer.md#blender_download) | Files between machines | — | Copies a file, or every file under a folder, from the machine Blender runs on to this one: finished frames, render job output, saved .blend files. |
| [`blender_scene_info`](scene.md#blender_scene_info) | Scene | — | The scene at a glance: file path, mode, frame range, render engine and resolution, object counts by type, collections, the active and selected objects, the r… |
| [`blender_list_objects`](scene.md#blender_list_objects) | Scene | — | Objects of the scene with type, location, rotation, scale, visibility and selection; filter by type or name. |
| [`blender_object_info`](scene.md#blender_object_info) | Scene | — | One object in depth: transform, dimensions, parent and children, collections, modifiers, materials, visibility and, for meshes, vertex, edge and face counts. |
| [`blender_select_objects`](scene.md#blender_select_objects) | Scene | — | Selects objects by name, type or name fragment and makes the last one active. |
| [`blender_scenes`](scene.md#blender_scenes) | Scene | — | Lists, creates, activates, renames or removes scenes of the file: several shots in one .blend, each with its own camera, frame range and render settings. |
| [`blender_add_primitive`](objects.md#blender_add_primitive) | Objects | — | Adds a mesh primitive at a location with rotation, scale and size, names it and makes it active. |
| [`blender_delete_objects`](objects.md#blender_delete_objects) | Objects | — | Deletes objects by name and/or the current selection. |
| [`blender_duplicate_object`](objects.md#blender_duplicate_object) | Objects | — | Copies an object, its mesh included unless linked=true, into the same collections at an optional offset. |
| [`blender_transform_object`](objects.md#blender_transform_object) | Objects | — | Sets or, with relative=true, adjusts an object's location, rotation (degrees) and scale. |
| [`blender_update_object`](objects.md#blender_update_object) | Objects | — | Renames an object, hides or shows it in the viewport or in renders, and sets or clears its parent. |
| [`blender_add_text`](objects.md#blender_add_text) | Objects | — | Adds a 3D text object with a body, size, extrusion, alignment and an optional font file; standing upright by default. |
| [`blender_add_empty`](objects.md#blender_add_empty) | Objects | — | Adds an empty: a point with a display shape, used as a parent, a target for constraints or a reference image. |
| [`blender_add_camera`](camera.md#blender_add_camera) | Camera | — | Adds a camera, optionally aimed at a point, and makes it the render camera. |
| [`blender_set_camera`](camera.md#blender_set_camera) | Camera | — | Changes a camera: focal length, sensor, clipping, aim point, and whether it renders; the iris and focus live in blender_set_camera_optics. |
| [`blender_add_light`](light.md#blender_add_light) | Light | — | Adds a light of a type with position, power, colour and the type's own settings (cone for SPOT, size for AREA, angle for SUN, radius for POINT and SPOT). |
| [`blender_set_light`](light.md#blender_set_light) | Light | — | Changes a light's power, colour, shadows and type-specific settings. |
| [`blender_create_collection`](collections.md#blender_create_collection) | Collections | — | Creates a collection under the scene root or under another collection. |
| [`blender_move_to_collection`](collections.md#blender_move_to_collection) | Collections | — | Moves objects into a collection, unlinking them from their other collections unless keepOthers=true. |
| [`blender_update_collection`](collections.md#blender_update_collection) | Collections | — | Hides or shows a collection in viewports and renders, excludes it from the view layer, or renames it. |
| [`blender_undo_redo`](files.md#blender_undo_redo) | Files, projects, snapshots and undo | — | Steps back or forward through Blender's undo history. |
| [`blender_save_file`](files.md#blender_save_file) | Files, projects, snapshots and undo | — | Saves the .blend file to its path or to a new one; copy=true writes a copy without switching to it. |
| [`blender_open_file`](files.md#blender_open_file) | Files, projects, snapshots and undo | — | Opens a .blend file, replacing the current scene without asking; save first if the current work matters. |
| [`blender_new_file`](files.md#blender_new_file) | Files, projects, snapshots and undo | — | Starts a new file from the default template, or empty, replacing the current scene without asking. |
| [`blender_open_project`](files.md#blender_open_project) | Files, projects, snapshots and undo | — | Opens the project named after the folder the client works in: files/&lt;name&gt;/&lt;name&gt;.blend on Blender's machine, with renders and textures folders beside it, al… |
| [`blender_snapshot`](files.md#blender_snapshot) | Files, projects, snapshots and undo | — | Saves a copy of the whole file to fall back to, lists the copies, restores one or removes it. |
| [`blender_journal`](team.md#blender_journal) | Journal, leases and batches | — | What changed in this Blender and by whom: every mutating command with its agent, time, targets and outcome, newest last. |
| [`blender_lease`](team.md#blender_lease) | Journal, leases and batches | — | Claims objects or collections for this agent for a while: mutating commands from other agents on them are refused with Leased until the lease is released or … |
| [`blender_batch`](team.md#blender_batch) | Journal, leases and batches | — | Runs a list of bridge commands in a separate headless Blender on a copy of the file (or a given .blend) and saves the result to a .blend: parallel work that … |
| [`blender_batch_status`](team.md#blender_batch_status) | Journal, leases and batches | — | Progress of a batch: state, how many steps finished and the last 20 with their results or errors, the output file and the log tail. |
| [`blender_run_operator`](operators.md#blender_run_operator) | Operators and Python | — | Runs any bpy operator with its properties: everything Blender's menus do, thousands of operators. |
| [`blender_describe_operator`](operators.md#blender_describe_operator) | Operators and Python | — | Documents an operator: label, description, and every property with type, default, limits and enum values. |
| [`blender_python`](operators.md#blender_python) | Operators and Python | — | Runs Python inside Blender with bpy available and returns stdout plus the value assigned to result. |
| [`blender_mesh_select`](mesh.md#blender_mesh_select) | Mesh editing | modeling | Selects vertices, edges and faces of a mesh: all, none, invert, by index, by the direction faces point (by_normal with an axis), by coordinate range (by_posi… |
| [`blender_mesh_extrude`](mesh.md#blender_mesh_extrude) | Mesh editing | modeling | Extrudes the selected faces along their normal by a distance; the new faces become the selection, so repeated calls build a tower. |
| [`blender_mesh_inset`](mesh.md#blender_mesh_inset) | Mesh editing | modeling | Insets the selected faces by a thickness, optionally pushing the inset in or out. |
| [`blender_mesh_bevel`](mesh.md#blender_mesh_bevel) | Mesh editing | modeling | Bevels the selected edges (or vertices) with a width and segment count; with nothing selected, every edge. |
| [`blender_mesh_subdivide`](mesh.md#blender_mesh_subdivide) | Mesh editing | modeling | Subdivides the selected edges (or all) with a number of cuts and optional smoothing. |
| [`blender_mesh_merge`](mesh.md#blender_mesh_merge) | Mesh editing | modeling | Merges vertices closer than a distance (Merge by Distance); the selection or all vertices. |
| [`blender_mesh_normals`](mesh.md#blender_mesh_normals) | Mesh editing | modeling | Recalculates face normals to point outside, or inside on request. |
| [`blender_mesh_shade`](mesh.md#blender_mesh_shade) | Mesh editing | modeling | Sets smooth or flat shading on the whole mesh; with an angle, edges sharper than it stay flat. |
| [`blender_mesh_geometry`](mesh.md#blender_mesh_geometry) | Mesh editing | modeling | Reads the mesh: vertex positions with indices and faces as vertex index lists, in local or world space, limited to a count; the counts cover the whole mesh. |
| [`blender_mesh_set_vertices`](mesh.md#blender_mesh_set_vertices) | Mesh editing | modeling | Moves vertices by index to new positions or by offsets; the direct way to sculpt exact shapes. |
| [`blender_add_modifier`](modifiers.md#blender_add_modifier) | Modifiers | modeling | Adds a modifier of any type to an object with its settings, optionally applying it at once. |
| [`blender_update_modifier`](modifiers.md#blender_update_modifier) | Modifiers | modeling | Changes settings of an existing modifier, or renames it. |
| [`blender_remove_modifier`](modifiers.md#blender_remove_modifier) | Modifiers | modeling | Removes a modifier from the stack without applying it. |
| [`blender_apply_modifier`](modifiers.md#blender_apply_modifier) | Modifiers | modeling | Bakes a modifier into the mesh and removes it from the stack; irreversible except through undo. |
| [`blender_describe_modifier`](modifiers.md#blender_describe_modifier) | Modifiers | modeling | Lists every modifier type, or documents one type's settings with types, defaults, limits and enum values for blender_add_modifier and blender_update_modifier. |
| [`blender_add_curve`](curves.md#blender_add_curve) | Curves | modeling | Adds a curve: bezier, nurbs or poly drawn through given points, or a circle or path primitive, with optional thickness (bevelDepth) that turns it into a tube. |
| [`blender_set_curve`](curves.md#blender_set_curve) | Curves | modeling | Changes a curve's thickness, extrusion, resolution, fill, and the objects used as bevel profile or taper. |
| [`blender_convert_to_mesh`](curves.md#blender_convert_to_mesh) | Curves | modeling | Converts a curve, text or metaball object into a mesh, keeping the original on request. |
| [`blender_join_objects`](geometry.md#blender_join_objects) | Geometry | modeling | Joins several objects into one; the target keeps its name, origin and modifiers. |
| [`blender_set_origin`](geometry.md#blender_set_origin) | Geometry | modeling | Moves an object's origin to its geometry, the 3D cursor or its centre of mass, or moves the geometry to the origin. |
| [`blender_apply_transforms`](geometry.md#blender_apply_transforms) | Geometry | modeling | Bakes location, rotation and scale into the mesh data, resetting them to identity; needed before booleans, arrays and exports that expect unit scale. |
| [`blender_build_geometry_nodes`](geometry-nodes.md#blender_build_geometry_nodes) | Geometry Nodes | modeling | Builds a Geometry Nodes tree on an object from a specification of nodes and links; Group Input and Group Output exist already, so link the geometry through t… |
| [`blender_set_geometry_inputs`](geometry-nodes.md#blender_set_geometry_inputs) | Geometry Nodes | modeling | Sets the exposed inputs of a Geometry Nodes modifier by name. |
| [`blender_create_material`](materials.md#blender_create_material) | Materials | materials | Creates a node material with Principled BSDF settings and optionally assigns it to an object. |
| [`blender_set_material`](materials.md#blender_set_material) | Materials | materials | Changes Principled BSDF settings of an existing material, or renames it. |
| [`blender_assign_material`](materials.md#blender_assign_material) | Materials | materials | Assigns a material to an object: by default the object becomes that material, which fills slot 0 and points every face at it. |
| [`blender_list_materials`](materials.md#blender_list_materials) | Materials | materials | Every material in the file with its user count. |
| [`blender_material_info`](materials.md#blender_material_info) | Materials | materials | A material in depth: Principled settings and the full node graph with node types, input values and links. |
| [`blender_set_viewport_shading`](materials.md#blender_set_viewport_shading) | Materials | materials | Switches every 3D viewport to wireframe, solid, material preview or rendered shading. |
| [`blender_add_node`](nodes.md#blender_add_node) | Shader nodes | materials | Adds a shader node to a material with input values and settings; link it with blender_link_nodes. |
| [`blender_set_node`](nodes.md#blender_set_node) | Shader nodes | materials | Changes input values, settings, name or label of a node. |
| [`blender_link_nodes`](nodes.md#blender_link_nodes) | Shader nodes | materials | Connects an output socket of one node to an input socket of another, replacing any existing link there. |
| [`blender_remove_node`](nodes.md#blender_remove_node) | Shader nodes | materials | Deletes a node and its links. |
| [`blender_build_node_graph`](nodes.md#blender_build_node_graph) | Shader nodes | materials | Builds several nodes and their links in one call from a specification; the way to assemble a whole shader (texture → ramp → bump → Principled) at once. |
| [`blender_load_image_texture`](textures.md#blender_load_image_texture) | Textures and UVs | materials | Loads an image file into an Image Texture node and wires it into the Principled BSDF. |
| [`blender_procedural_texture`](textures.md#blender_procedural_texture) | Textures and UVs | materials | Adds a procedural texture (noise, voronoi, brick and the rest), optionally through a colour ramp, and wires it into a Principled BSDF input; Normal goes thro… |
| [`blender_unwrap_uv`](textures.md#blender_unwrap_uv) | Textures and UVs | materials | Unwraps a mesh's UVs by Smart UV Project, seams, cube, sphere or cylinder projection. |
| [`blender_set_frame`](keyframes.md#blender_set_frame) | Keyframes | animation | Sets the current frame, the scene's frame range and the frame rate; returns the effective values. |
| [`blender_insert_keyframe`](keyframes.md#blender_insert_keyframe) | Keyframes | animation | Keys properties of an object on a frame, optionally setting their values first (rotation in degrees). |
| [`blender_delete_keyframes`](keyframes.md#blender_delete_keyframes) | Keyframes | animation | Deletes keys on a frame or channels; with neither, strips all animation from the object. |
| [`blender_list_keyframes`](keyframes.md#blender_list_keyframes) | Keyframes | animation | Every animation curve of an object with its keys, interpolation and modifiers, plus its drivers. |
| [`blender_set_interpolation`](keyframes.md#blender_set_interpolation) | Keyframes | animation | Sets the interpolation and easing of keys, on all curves or a frame range. |
| [`blender_move_keyframes`](keyframes.md#blender_move_keyframes) | Keyframes | animation | Shifts keys in time and/or stretches their timing around a pivot frame. |
| [`blender_add_fcurve_modifier`](keyframes.md#blender_add_fcurve_modifier) | Keyframes | animation | Adds an F-Curve modifier to the object's animation curves: repeat with CYCLES, jitter with NOISE, and the rest. |
| [`blender_add_marker`](keyframes.md#blender_add_marker) | Keyframes | animation | Adds a timeline marker, optionally binding a camera to it so the render switches cameras there. |
| [`blender_add_armature`](rig.md#blender_add_armature) | Rigging | animation | Creates an armature from a list of bones with heads, tails and parents. |
| [`blender_parent_to_armature`](rig.md#blender_parent_to_armature) | Rigging | animation | Skins a mesh to an armature: parents it and creates vertex groups, with automatic weights by default. |
| [`blender_pose_bone`](rig.md#blender_pose_bone) | Rigging | animation | Moves, rotates or scales a bone in pose mode and optionally keys it on a frame. |
| [`blender_set_vertex_weights`](rig.md#blender_set_vertex_weights) | Rigging | animation | Sets weights of a vertex group by index list, for all vertices or for the selected ones. |
| [`blender_follow_path`](motion.md#blender_follow_path) | Paths and drivers | animation | Makes an object travel along a curve over a frame range with a Follow Path constraint and a linear path animation. |
| [`blender_add_driver`](motion.md#blender_add_driver) | Paths and drivers | animation | Drives a property with a Python expression over variables read from other objects and the frame. |
| [`blender_add_constraint`](constraints.md#blender_add_constraint) | Constraints | animation | Adds a constraint of any type to an object or a bone with its settings: aim a camera with TRACK_TO, follow with COPY_LOCATION, limit motion, IK for rigs. |
| [`blender_update_constraint`](constraints.md#blender_update_constraint) | Constraints | animation | Changes settings, influence, enabled state or name of a constraint. |
| [`blender_remove_constraint`](constraints.md#blender_remove_constraint) | Constraints | animation | Removes a constraint from an object or a bone. |
| [`blender_describe_constraint`](constraints.md#blender_describe_constraint) | Constraints | animation | Lists every constraint type, or documents one type's settings with types, defaults and enum values. |
| [`blender_add_shape_key`](shape-keys.md#blender_add_shape_key) | Shape keys | animation | Adds a shape key (blend shape), optionally shaping it by vertex offsets; blender_mesh_geometry gives the indices. |
| [`blender_set_shape_key`](shape-keys.md#blender_set_shape_key) | Shape keys | animation | Sets a shape key's value and slider range, optionally keying the value on a frame. |
| [`blender_add_physics`](physics.md#blender_add_physics) | Physics | physics | Adds a simulation to an object: rigid body, cloth, soft body, collision, fluid or dynamic paint, with settings. |
| [`blender_remove_physics`](physics.md#blender_remove_physics) | Physics | physics | Removes a simulation or the particle systems from an object. |
| [`blender_add_particles`](physics.md#blender_add_particles) | Physics | physics | Adds an emitter or hair particle system to a mesh with count, timing, length and further settings. |
| [`blender_bake_physics`](physics.md#blender_bake_physics) | Physics | physics | Bakes every physics cache of the scene up to a frame, or frees them; needed before rendering simulations. |
| [`blender_render_settings`](rendering.md#blender_render_settings) | Rendering | rendering | Sets the render engine, resolution, samples, denoising, background transparency, pixel filter width (sharpness) and camera; every value optional. |
| [`blender_render_image`](rendering.md#blender_render_image) | Rendering | rendering | Renders the current or a given frame of a scene and view layer to an image file and returns its path, size, duration, peak memory, a preview to look at and e… |
| [`blender_render_animation`](rendering.md#blender_render_animation) | Rendering | rendering | Renders the frame range as an image sequence or a video. |
| [`blender_set_world`](rendering.md#blender_set_world) | Rendering | rendering | Sets the world: background colour and strength, an HDRI environment with rotation, and mist. |
| [`blender_set_color_management`](output.md#blender_set_color_management) | Colour management and output | rendering | Sets colour management: display device, view transform (AgX, Filmic, Standard, ACES, Khronos PBR Neutral), look, exposure, gamma, white balance and the seque… |
| [`blender_set_output`](output.md#blender_set_output) | Colour management and output | rendering | Sets the render output: path template with #### for the frame number, format, colour depth, EXR codec, PNG compression, JPEG quality, overwrite and placehold… |
| [`blender_set_cycles`](engines.md#blender_set_cycles) | Cycles, EEVEE, motion blur and Freestyle | rendering | Switches to Cycles and sets its sampling in depth: samples, adaptive sampling with threshold and minimum, a time limit, the denoiser with prefilter, passes a… |
| [`blender_set_eevee`](engines.md#blender_set_eevee) | Cycles, EEVEE, motion blur and Freestyle | rendering | Switches to EEVEE and sets its quality: samples, shadows with rays, steps and resolution, ray tracing with its options, fast GI, volumetrics, clamping, depth… |
| [`blender_set_motion_blur`](engines.md#blender_set_motion_blur) | Cycles, EEVEE, motion blur and Freestyle | rendering | Sets motion blur for renders: on or off, shutter time in frames, shutter position, Cycles rolling shutter, EEVEE steps and maximum blur, and whether the view… |
| [`blender_set_freestyle`](engines.md#blender_set_freestyle) | Cycles, EEVEE, motion blur and Freestyle | rendering | Freestyle line rendering: on or off, line thickness and mode, crease angle, and line sets with their edge types, visibility, colour and thickness; for toon, … |
| [`blender_view_layer`](layers.md#blender_view_layer) | View layers and light linking | rendering | Creates, configures, lists, activates or removes view layers: which collections are excluded, holdout or indirect-only, the render passes, cryptomatte (objec… |
| [`blender_light_linking`](layers.md#blender_light_linking) | View layers and light linking | rendering | Cycles light and shadow linking: which objects or collections a light illuminates (include: only these; exclude: all but these) and which cast its shadow. |
| [`blender_set_camera_optics`](optics.md#blender_set_camera_optics) | Camera optics | rendering | The physical camera: sensor size and fit, focal length or field of view, lens shift, clip range, perspective, orthographic or panoramic type, aperture with f… |
| [`blender_camera_move`](staging.md#blender_camera_move) | Camera moves and light rigs | rendering | Animates a camera move around a subject over a frame range: turntable, orbit, dolly, push-in or crane, with the camera aimed at the subject. |
| [`blender_light_rig`](staging.md#blender_light_rig) | Camera moves and light rigs | rendering | Builds a three-point rig of area lights around a subject: key, fill and rim at studio angles, sized and powered from the subject's size, each with a colour t… |
| [`blender_object_render_flags`](flags.md#blender_object_render_flags) | Object render flags | rendering | Per-object rendering flags for integration and passes: holdout, shadow catcher, visibility per ray type, pass index for the object index pass, light group, c… |
| [`blender_render_object`](shots.md#blender_render_object) | Product shots, captures and bakes | rendering | Renders one object (or a few) alone: a temporary camera frames them from a chosen angle, everything else is hidden from the render, the background is transpa… |
| [`blender_viewport_capture`](shots.md#blender_viewport_capture) | Product shots, captures and bakes | rendering | Grabs the 3D viewport as the user sees it, in its current shading, to a file; fast feedback without a render. |
| [`blender_inspect_image`](shots.md#blender_inspect_image) | Product shots, captures and bakes | rendering | Looks at an image file: a preview the model can see, plus exposure statistics (mean luminance, clipped highlights and shadows, a histogram). |
| [`blender_bake_texture`](shots.md#blender_bake_texture) | Product shots, captures and bakes | rendering | Bakes a material channel (colour, normal, AO, roughness, combined) into a PNG through the mesh's UVs with Cycles; the way to take procedural materials into g… |
| [`blender_render_job`](jobs.md#blender_render_job) | Background render jobs and the pre-flight check | farm | Starts a background render: the file is saved as a copy and a second Blender renders it, so this Blender and the tools stay free. |
| [`blender_render_job_status`](jobs.md#blender_render_job_status) | Background render jobs and the pre-flight check | farm | Progress of a background render: state, frames done of total, current frame and camera, elapsed and estimated remaining seconds, peak memory, the files writt… |
| [`blender_render_job_cancel`](jobs.md#blender_render_job_cancel) | Background render jobs and the pre-flight check | farm | Stops a background render; frames already written stay on disk. |
| [`blender_render_check`](jobs.md#blender_render_check) | Background render jobs and the pre-flight check | farm | Pre-flight before a long render: camera, writable output and free disk, unsaved file or changes, missing textures and libraries, disabled view layers, frame … |
| [`blender_render_budget`](budget.md#blender_render_budget) | Render budget | farm | What the frame will cost before rendering it: texture memory by size and depth, geometry after modifiers, instances, particles and hair, the Cycles devices a… |
| [`blender_lens_effects`](post.md#blender_lens_effects) | Lens effects | post | Rebuilds the post stack on the beauty pass in a compositor's order: denoise (with the denoising data passes), depth of field or motion blur from passes, glar… |
| [`blender_compositor`](compositor.md#blender_compositor) | Compositor | post | Reads or edits the compositor by hand: list nodes, reset to Render Layers → output, add a node with inputs and settings, link sockets, remove a node. |
| [`blender_file_output`](compositor.md#blender_file_output) | Compositor | post | Configures a File Output node in the compositor: a folder, a format with its depth and codec, and one slot per render pass wired from the Render Layers node,… |
| [`blender_cryptomatte_matte`](compositor.md#blender_cryptomatte_matte) | Compositor | post | Adds a Cryptomatte node that mattes the named objects, materials or assets, switching the cryptomatte pass on, and optionally writes the matte through a File… |
| [`blender_import_file`](io.md#blender_import_file) | Import, export and assets | io | Imports a model or scene file into the current scene and lists the objects it added; the format follows the extension. |
| [`blender_export_file`](io.md#blender_export_file) | Import, export and assets | io | Exports the scene or the selected objects to a file; the format follows the extension. |
| [`blender_list_assets`](io.md#blender_list_assets) | Import, export and assets | io | Lists what another .blend file holds by type: objects, collections, materials, node groups, worlds, images and more. |
| [`blender_append_assets`](io.md#blender_append_assets) | Import, export and assets | io | Appends (copies) or links objects, collections, materials, node groups, worlds or images from another .blend file into the scene, optionally into a collectio… |
| [`blender_sequencer_info`](video.md#blender_sequencer_info) | Video editing | video | The timeline: every strip with its channel, frames, trims, blend and source, plus the frame range, fps, resolution and output format. |
| [`blender_sequencer_add_strip`](video.md#blender_sequencer_add_strip) | Video editing | video | Adds a strip to the timeline: a movie (with its sound), an image or an image sequence from a folder or file list, a sound file, a 3D scene, a solid colour or… |
| [`blender_sequencer_add_effect`](video.md#blender_sequencer_add_effect) | Video editing | video | Adds a transition or effect strip: a cross-fade or wipe between two strips where they overlap, a blur, glow or speed change on one, or a generated colour or … |
| [`blender_sequencer_update_strip`](video.md#blender_sequencer_update_strip) | Video editing | video | Moves, trims, mutes, fades, re-blends, transforms or renames a strip; changes text, colour or volume. |
| [`blender_sequencer_remove_strip`](video.md#blender_sequencer_remove_strip) | Video editing | video | Removes one strip, or every strip with all=true. |
| [`blender_sequencer_render`](video.md#blender_sequencer_render) | Video editing | video | Encodes the timeline to a video file with FFmpeg: container by extension, codec, quality and audio codec, or whatever blender_sequencer_encode prepared. |
| [`blender_sequencer_split`](cuts.md#blender_sequencer_split) | Cuts, meta strips, proxies, timing and grading | video | Cuts a strip at a frame into two strips on the same channel, keeping both or one side; movie, sound, image, scene, colour and text strips. |
| [`blender_sequencer_proxy`](cuts.md#blender_sequencer_proxy) | Cuts, meta strips, proxies, timing and grading | video | Sets up proxies (25, 50, 75, 100 percent) for smooth playback of heavy footage, per strip or for all, with optional project-level storage, and builds them wh… |
| [`blender_sequencer_timing`](cuts.md#blender_sequencer_timing) | Cuts, meta strips, proxies, timing and grading | video | Sets the project timing: frame rate including fractional rates (23.976, 29.97, 59.94), sync mode for playback (audio sync, frame drop), pixel aspect, resolut… |
| [`blender_sequencer_grade`](cuts.md#blender_sequencer_grade) | Cuts, meta strips, proxies, timing and grading | video | Grades a strip with modifiers: colour balance (lift/gamma/gain or offset/power/slope), curves by channel, hue correct, brightness and contrast, white balance… |
| [`blender_sequencer_meta`](cuts.md#blender_sequencer_meta) | Cuts, meta strips, proxies, timing and grading | video | Groups strips into a meta strip, or separates one, through Blender's own operators; needs the Video Sequencer editor open in Blender. |
| [`blender_sequencer_encode`](delivery.md#blender_sequencer_encode) | Encode and mixdown | video | Sets the encode without rendering: container, codec (H264, H265, AV1, ProRes with profile, DNxHD, FFV1, WebM), quality by constant rate factor or a target bi… |
| [`blender_sequencer_mixdown`](delivery.md#blender_sequencer_mixdown) | Encode and mixdown | video | Mixes every sound strip of the timeline down to one audio file: WAV, FLAC, MP3, OGG, AAC or AC3, with sample format, bitrate and channel layout. |

## Prompts

| Name | What it does |
| --- | --- |
| `photoreal_product_shot` | Set up, light, render and check a photoreal product shot of one object, with the post stack a compositor would build. |
| `farm_exr_delivery` | Prepare a scene for a multi-layer EXR render with passes, cryptomatte and light groups, check it, and run it as a background job or hand it to a farm. |
| `vse_grade_and_deliver` | Cut, grade and encode a timeline in the Video Sequence Editor to a delivery codec, with a separate audio mixdown. |

## Resources

| Name | What it does |
| --- | --- |
| `snail://renders/last` | The picture the last render, capture, bake or blender_inspect_image returned, as the image it travelled as. |
| `snail://journal` | The add-on's journal of mutating commands with their agents, newest last, as blender_journal returns it. |
