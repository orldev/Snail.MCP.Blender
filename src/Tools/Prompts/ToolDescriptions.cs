namespace Snail.MCP.Blender.Tools.Prompts;

/// <summary>Tool description catalog — what the model reads before calling; a contract with the agent guarded by <c>ToolCatalogConventionTests</c>. One partial file per area of Blender.</summary>
public static partial class ToolDescriptions
{
    /// <summary>Parameter descriptions shared by several tools.</summary>
    public static class Parameters
    {
        public const string ObjectName = "Name of the object as shown in the outliner.";

        public const string ObjectType = "Blender object type: MESH, CURVE, SURFACE, META, FONT, ARMATURE, LATTICE, EMPTY, LIGHT, CAMERA, SPEAKER, GREASEPENCIL, VOLUME, POINTCLOUD.";

        public const string Location = "[x, y, z] in metres.";

        public const string Rotation = "[x, y, z] Euler rotation in degrees.";

        public const string Scale = "[x, y, z] scale factors; 1 keeps the size.";

        /// <summary>Names every entry of <c>Primitives.Kinds</c>; a guard test keeps them equal.</summary>
        public const string PrimitiveKind =
            "Which primitive: cube, plane, grid, monkey, circle, uv_sphere, ico_sphere, cylinder, cone or torus.";

        /// <summary>Names every entry of <c>Lights.Kinds</c>; a guard test keeps them equal.</summary>
        public const string LightType = "POINT, SUN, SPOT or AREA.";

        public const string LightEnergy = "Power in watts (SUN: irradiance in W/m²).";

        public const string LightTemperature = "Colour temperature in kelvin instead of a colour: 3200 tungsten, 5600 daylight, 6500 neutral, 8000 shade.";

        /// <summary>Names every entry of <c>Lights.Shapes</c>; a guard test keeps them equal.</summary>
        public const string LightShape = "AREA: SQUARE, RECTANGLE, DISK or ELLIPSE.";

        /// <summary>Names every entry of <c>Output.ResolutionPresets</c>; a guard test keeps them equal.</summary>
        public const string ResolutionPreset =
            "Resolution preset, which also sets the percentage to 100: HD, FHD, QHD, UHD, 8K, DCI_2K, DCI_4K, SQUARE_1K, SQUARE_2K, PORTRAIT_FHD, PORTRAIT_UHD, CINEMASCOPE_2K, CINEMASCOPE_4K.";

        /// <summary>Names every entry of <c>CameraMoves.All</c>; a guard test keeps them equal.</summary>
        public const string CameraMove =
            "turntable (the subject spins on a pivot under a still camera), orbit (the camera circles the subject), dolly or push_in (the camera moves towards the subject), crane (the camera rises).";

        public const string LightColor = "[r, g, b] with components from 0 to 1.";

        public const string OperatorName = "Operator as bpy.ops names it, without the prefix: mesh.primitive_cube_add, object.shade_smooth, transform.resize.";

        /// <summary>States <c>ToolLimits.MaxTimeoutSeconds</c>; a guard test keeps the number equal.</summary>
        public const string TimeoutSeconds = "Seconds to wait for Blender, 1 to 600.";

        public const string SkillName = "Skill name as blender_skills lists it.";

        /// <summary>States <c>JavaScriptPrograms.MostCalls</c>; a guard test keeps the number equal.</summary>
        public const string ProgramCode =
            "JavaScript. Call commands as blender.<command>({...}) — the names blender_find_tool reports, the tool name without blender_ — return a " +
            "value, log(...) to add a line. Up to 200 commands, no files, no network, no bpy.";

        public const string MeshName = "Name of the mesh object.";

        public const string ModifierName = "Modifier name as shown in the stack; blender_object_info lists them.";

        public const string ModifierType =
            "Modifier type as Blender names it: ARRAY, BEVEL, BOOLEAN, BUILD, DECIMATE, EDGE_SPLIT, MIRROR, REMESH, SCREW, SKIN, " +
            "SOLIDIFY, SUBSURF, TRIANGULATE, WELD, WIREFRAME, CAST, CURVE, DISPLACE, LATTICE, MESH_DEFORM, SHRINKWRAP, " +
            "SIMPLE_DEFORM, SMOOTH, WARP, WAVE, NODES and the rest; blender_describe_modifier lists them all.";

        public const string ModifierSettings =
            "Settings as a JSON object keyed by the modifier's Python property names, e.g. ARRAY {\"count\": 3}, " +
            "BOOLEAN {\"operation\": \"DIFFERENCE\", \"object\": \"Cutter\"}, MIRROR {\"use_axis\": [true, false, false], " +
            "\"use_clip\": true}, SUBSURF {\"levels\": 2}, SOLIDIFY {\"thickness\": 0.05}. Objects, collections and " +
            "node groups are named by their name. Unknown keys are rejected with the list of known ones; " +
            "blender_describe_modifier documents them.";

        public const string BevelDepth = "Thickness of the curve as a round tube, in metres; 0 keeps it a line.";

        public const string CurveExtrude = "Extrude the curve into a flat ribbon of this half-height, in metres.";

        public const string ResolutionU = "Segments between control points; more is smoother.";

        public const string MaterialName = "Material name as blender_list_materials shows it.";

        public const string Color = "[r, g, b] or [r, g, b, a] with components 0 to 1.";

        public const string EmissionColor = "[r, g, b] emission colour; pair with emissionStrength above 0.";

        public const string BlendMethod = "How alpha renders: OPAQUE, CLIP, HASHED, BLEND.";

        public const string NodeType =
            "Shader node bl_idname or its suffix: TexNoise, TexVoronoi, TexImage, ValToRGB (colour ramp), Bump, NormalMap, Math, " +
            "Mix, MixRGB, RGBCurve, Mapping, TexCoord, SeparateXYZ, CombineXYZ, BsdfPrincipled, Emission, BsdfGlass, MixShader.";

        public const string NodeName = "Node name as blender_material_info lists it, e.g. Principled BSDF.";

        public const string NodeInputs = "Unlinked input sockets by name, e.g. {\"Scale\": 5, \"Base Color\": [1, 0, 0]}.";

        public const string NodeProperties =
            "Non-socket settings by Python name, e.g. {\"noise_dimensions\": \"4D\"}, {\"blend_type\": \"MULTIPLY\"}, {\"operation\": \"ADD\"}.";

        public const string ConnectTo =
            "Principled BSDF input to wire the texture into: Base Color, Roughness, Metallic, Alpha, Normal (adds a bump node) " +
            "or Emission Color; empty leaves it unconnected.";

        public const string Channels =
            "Animated properties: location, rotation_euler, scale, or any data path such as data.energy for a light, " +
            "data.lens for a camera, hide_render.";

        public const string Interpolation =
            "CONSTANT, LINEAR, BEZIER (the default) or an eased kind: SINE, QUAD, CUBIC, QUART, QUINT, EXPO, CIRC, BACK, BOUNCE, ELASTIC.";

        /// <summary>States <c>ToolLimits.MaxRenderResolution</c>; a guard test keeps the number equal.</summary>
        public const string RenderResolution = "Pixels along this edge, 1 to 4096.";

        /// <summary>Names every entry of <c>FileFormats.Import</c>; a guard test keeps them equal.</summary>
        public const string ImportFormat =
            "Format when the extension does not say: fbx, obj, stl, ply, gltf, glb, dae, usd, usda, usdc, usdz, abc, x3d, svg, dxf, 3ds.";

        /// <summary>Names every entry of <c>FileFormats.Export</c>; a guard test keeps them equal.</summary>
        public const string ExportFormat =
            "Format when the extension does not say: fbx, obj, stl, ply, gltf, glb, dae, usd, usda, usdc, usdz, abc, x3d, dxf, 3ds.";

        /// <summary>Names every entry of <c>EmptyTypes.All</c>; a guard test keeps them equal.</summary>
        public const string EmptyType = "Display shape: PLAIN_AXES, ARROWS, SINGLE_ARROW, CIRCLE, CUBE, SPHERE, CONE or IMAGE.";

        /// <summary>Names every entry of <c>PhysicsKinds.All</c>; a guard test keeps them equal.</summary>
        public const string PhysicsKind =
            "rigid_body (falls and collides), rigid_body_passive (static obstacle), cloth, soft_body, collision (obstacle for cloth, " +
            "soft bodies and particles), fluid_domain, fluid_flow, fluid_effector, dynamic_paint.";

        public const string ConstraintSettings =
            "Settings by Python name, e.g. {\"target\": \"Camera\"}, TRACK_TO {\"target\": \"Cube\", \"track_axis\": \"TRACK_NEGATIVE_Z\", " +
            "\"up_axis\": \"UP_Y\"}, IK {\"target\": \"Rig\", \"subtarget\": \"Hand.IK\", \"chain_count\": 2}; objects are named by their " +
            "name, bones by subtarget. blender_describe_constraint documents them.";

        /// <summary>Names every entry of <c>Sequencer.StripKinds</c>; a guard test keeps them equal.</summary>
        public const string StripKind = "movie, image, sound, scene (renders a 3D scene into the timeline), color or text.";

        /// <summary>Names every entry of <c>Sequencer.EffectTypes</c>; a guard test keeps them equal.</summary>
        public const string EffectType =
            "Transitions between two strips: CROSS, GAMMA_CROSS, WIPE; mixes of two: ADD, SUBTRACT, MULTIPLY, ALPHA_OVER, ALPHA_UNDER, " +
            "COLORMIX; on one strip: GLOW, GAUSSIAN_BLUR, SPEED, ADJUSTMENT; generators: COLOR, TEXT; special: MULTICAM, COMPOSITOR.";

        public const string StripBlend = "How the strip mixes with the channels below: REPLACE, ALPHA_OVER (the default for effects), CROSS, ADD, MULTIPLY, SCREEN, OVERLAY, DIFFERENCE and the other blend modes.";

        public const string StripOpacity = "Opacity 0 to 1.";

        public const string StripTransform = "Picture placement: offset, scale, rotation, crop and flip.";

        public const string Scene = "Scene to act on; the current scene otherwise.";

        public const string Preview = "Return a small JPEG of the result as image content, with exposure statistics; false for the path only.";

        /// <summary>Names every entry of <c>Output.ColorDepths</c>; a guard test keeps them equal.</summary>
        public const string ColorDepth = "Bits per channel: 8 or 16 for PNG and TIFF, 16 (half) or 32 (full float) for EXR, 8, 10 or 12 for video and DPX.";

        /// <summary>Names every entry of <c>Output.ExrCodecs</c>; a guard test keeps them equal.</summary>
        public const string ExrCodec =
            "EXR compression: ZIP (lossless, the default), PIZ, ZIPS, RLE, PXR24 (lossy for 32-bit), DWAA and DWAB (lossy, small), B44, B44A, HTJ2K or NONE.";

        /// <summary>Names every entry of <c>Sequencer.ModifierTypes</c>; a guard test keeps them equal.</summary>
        public const string StripModifier =
            "COLOR_BALANCE (lift/gamma/gain or offset/power/slope), CURVES, HUE_CORRECT, BRIGHT_CONTRAST, WHITE_BALANCE, TONEMAP, MASK; " +
            "for sound strips SOUND_EQUALIZER, PITCH, ECHO.";

        /// <summary>Names every entry of <c>Sequencer.ProresProfiles</c>; a guard test keeps them equal.</summary>
        public const string ProresProfile = "ProRes profile, which also selects the PRORES codec: 422_PROXY, 422_LT, 422_STD, 422_HQ, 4444 or 4444_XQ.";

        public const string Frames = "Frames to render: 1-240, every second frame 1-240x2, a list 1,5,9-12; the scene range otherwise.";

        public const string RenderJobId = "Job id as blender_render_job returned it.";

        public const string StripFade = "Fades in frames; opacity for pictures, volume for sound.";

        public const string StripSettings =
            "Further strip properties by Python name, e.g. text {\"use_box\": true, \"wrap_width\": 0.8}, SPEED {\"speed_factor\": 2}, " +
            "WIPE {\"transition_type\": \"CLOCK\"}, GAUSSIAN_BLUR {\"size_x\": 20, \"size_y\": 20}, GLOW {\"threshold\": 0.5}.";
    }
}
