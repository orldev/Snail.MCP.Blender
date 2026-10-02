"""Engine settings in depth: Cycles sampling, light paths and denoising; EEVEE shadows, ray tracing and volumetrics; motion blur."""

import math

import bpy

from .core import _known_enum, _scene, _set_enum, command
from .modeling import _apply_settings
from .optics import _apply_curve_points
from .server import CommandError

CYCLES = {
    "samples": "samples", "viewport_samples": "preview_samples", "adaptive_sampling": "use_adaptive_sampling", "adaptive_threshold": "adaptive_threshold",
    "adaptive_min_samples": "adaptive_min_samples", "time_limit": "time_limit", "denoise": "use_denoising", "denoiser": "denoiser",
    "denoise_prefilter": "denoising_prefilter", "denoise_passes": "denoising_input_passes", "denoise_quality": "denoising_quality",
    "denoise_gpu": "denoising_use_gpu", "device": "device", "seed": "seed", "animated_seed": "use_animated_seed", "light_tree": "use_light_tree",
    "light_threshold": "light_sampling_threshold", "fast_gi": "use_fast_gi", "film_exposure": "film_exposure", "pixel_filter": "pixel_filter_type",
    "filter_width": "filter_width", "volume_step_rate": "volume_step_rate", "volume_max_steps": "volume_max_steps", "texture_limit": "texture_limit_render",
    "guiding": "use_guiding", "tile_size": "tile_size", "auto_tile": "use_auto_tile", "scrambling_distance": "scrambling_distance",
    "transparent_glass": "film_transparent_glass",
}
BOUNCES = {"total": "max_bounces", "diffuse": "diffuse_bounces", "glossy": "glossy_bounces", "transmission": "transmission_bounces",
           "volume": "volume_bounces", "transparent": "transparent_max_bounces"}
CAUSTICS = {"reflective": "caustics_reflective", "refractive": "caustics_refractive", "blur_glossy": "blur_glossy"}
CLAMP = {"direct": "sample_clamp_direct", "indirect": "sample_clamp_indirect"}
EEVEE = {
    "samples": "taa_render_samples", "viewport_samples": "taa_samples", "shadows": "use_shadows", "shadow_rays": "shadow_ray_count",
    "shadow_steps": "shadow_step_count", "shadow_resolution": "shadow_resolution_scale", "shadow_pool": "shadow_pool_size",
    "raytracing": "use_raytracing", "raytracing_method": "ray_tracing_method", "fast_gi": "use_fast_gi", "light_threshold": "light_threshold",
    "overscan": "use_overscan", "overscan_size": "overscan_size",
}
EEVEE_RAYTRACING = {"resolution": "resolution_scale", "max_roughness": "trace_max_roughness", "quality": "screen_trace_quality", "thickness": "screen_trace_thickness",
                    "denoise": "use_denoise", "denoise_spatial": "denoise_spatial", "denoise_temporal": "denoise_temporal", "denoise_bilateral": "denoise_bilateral"}
EEVEE_FAST_GI = {"method": "fast_gi_method", "quality": "fast_gi_quality", "rays": "fast_gi_ray_count", "steps": "fast_gi_step_count", "distance": "fast_gi_distance",
                 "resolution": "fast_gi_resolution", "bias": "fast_gi_bias", "thickness": "fast_gi_thickness_near"}
EEVEE_VOLUMETRICS = {"tile_size": "volumetric_tile_size", "samples": "volumetric_samples", "distribution": "volumetric_sample_distribution", "start": "volumetric_start",
                     "end": "volumetric_end", "custom_range": "use_volume_custom_range", "shadows": "use_volumetric_shadows", "shadow_samples": "volumetric_shadow_samples",
                     "ray_depth": "volumetric_ray_depth", "light_clamp": "volumetric_light_clamp"}
EEVEE_CLAMP = {"surface_direct": "clamp_surface_direct", "surface_indirect": "clamp_surface_indirect", "volume_direct": "clamp_volume_direct", "volume_indirect": "clamp_volume_indirect"}
EEVEE_BOKEH = {"max_size": "bokeh_max_size", "threshold": "bokeh_threshold", "neighbor_max": "bokeh_neighbor_max", "jittered": "use_bokeh_jittered", "overblur": "bokeh_overblur"}


def _assign(target, attribute, value, label):
    if attribute not in target.bl_rna.properties:
        raise CommandError("Unsupported", f"this Blender has no setting '{attribute}' for {label}")
    prop = target.bl_rna.properties[attribute]
    if prop.type == "ENUM":
        _set_enum(target, attribute, str(value).upper() if str(value).upper() in [item.identifier for item in prop.enum_items] else str(value), label)
    elif prop.type == "BOOLEAN":
        setattr(target, attribute, bool(value))
    elif prop.type == "INT":
        setattr(target, attribute, int(value))
    elif prop.type == "FLOAT":
        setattr(target, attribute, tuple(value) if getattr(prop, "is_array", False) else float(value))
    else:
        setattr(target, attribute, value)


def _apply_mapping(target, values, mapping, label):
    for key, value in (values or {}).items():
        if value is None:
            continue
        attribute = mapping.get(key)
        if attribute is None:
            raise CommandError("UnknownParameter", f"{label} has no setting '{key}'", {"known": sorted(mapping)})
        _assign(target, attribute, value, f"{label} {key}")


def _read(target, mapping):
    return {key: getattr(target, attribute) for key, attribute in mapping.items() if attribute in target.bl_rna.properties}


def _cycles_summary(scene):
    cycles = scene.cycles
    return {
        "scene": scene.name,
        "engine": scene.render.engine,
        **_read(cycles, CYCLES),
        "bounces": _read(cycles, BOUNCES),
        "caustics": _read(cycles, CAUSTICS),
        "clamp": _read(cycles, CLAMP),
        "persistent_data": scene.render.use_persistent_data,
        "known": {"denoisers": _known_enum(cycles, "denoiser"), "devices": _known_enum(cycles, "device"), "pixel_filters": _known_enum(cycles, "pixel_filter_type")},
    }


@command("set_cycles")
def set_cycles(params):
    scene = _scene(params)
    cycles = scene.cycles
    if params.get("activate", True):
        scene.render.engine = "CYCLES"
    if params.get("backend"):
        backend = params["backend"].upper()
        _use_backend(backend)
        if params.get("device") is None:
            cycles.device = "CPU" if backend == "NONE" else "GPU"
    _apply_mapping(cycles, {key: params.get(key) for key in CYCLES}, CYCLES, "Cycles")
    _apply_mapping(cycles, params.get("bounces"), BOUNCES, "bounces")
    _apply_mapping(cycles, params.get("caustics"), CAUSTICS, "caustics")
    _apply_mapping(cycles, params.get("clamp"), CLAMP, "clamp")
    if params.get("persistent_data") is not None:
        scene.render.use_persistent_data = bool(params["persistent_data"])
    if params.get("settings"):
        _apply_settings(cycles, params["settings"])
    return {**_cycles_summary(scene), "compute": _compute()}


def _use_backend(backend):
    """Chooses the compute backend and enables every device it offers. A render job carries the choice to its worker, which
    would otherwise read its own saved preferences and, on a fresh machine, render on the CPU."""
    addon = bpy.context.preferences.addons.get("cycles")
    if addon is None:
        raise CommandError("NotFound", "the Cycles add-on is not enabled in this Blender")
    preferences = addon.preferences
    _set_enum(preferences, "compute_device_type", backend, "compute backend")
    preferences.get_devices()
    for device in preferences.devices:
        device.use = backend != "NONE" and device.type == backend


def _compute():
    """The backend this Blender renders with and every device it can see, so a remote machine's GPU is visible from here."""
    addon = bpy.context.preferences.addons.get("cycles")
    if addon is None:
        return None
    preferences = addon.preferences
    preferences.get_devices()
    return {
        "backend": preferences.compute_device_type,
        "devices": [{"name": device.name, "type": device.type, "use": device.use} for device in preferences.devices],
        "known": _known_enum(preferences, "compute_device_type"),
    }


def _eevee_summary(scene):
    eevee = scene.eevee
    summary = {
        "scene": scene.name,
        "engine": scene.render.engine,
        **_read(eevee, EEVEE),
        "fast_gi_options": _read(eevee, EEVEE_FAST_GI),
        "volumetrics": _read(eevee, EEVEE_VOLUMETRICS),
        "clamp": _read(eevee, EEVEE_CLAMP),
        "bokeh": _read(eevee, EEVEE_BOKEH),
        "high_quality_normals": scene.render.use_high_quality_normals,
    }
    if hasattr(eevee, "ray_tracing_options"):
        summary["raytracing_options"] = _read(eevee.ray_tracing_options, EEVEE_RAYTRACING)
    return summary


@command("set_eevee")
def set_eevee(params):
    scene = _scene(params)
    eevee = scene.eevee
    if params.get("activate", True):
        known = [item.identifier for item in bpy.types.RenderSettings.bl_rna.properties["engine"].enum_items]
        scene.render.engine = "BLENDER_EEVEE_NEXT" if "BLENDER_EEVEE_NEXT" in known else "BLENDER_EEVEE"
    _apply_mapping(eevee, {key: params.get(key) for key in EEVEE}, EEVEE, "EEVEE")
    if params.get("raytracing_options"):
        if not hasattr(eevee, "ray_tracing_options"):
            raise CommandError("Unsupported", "this Blender's EEVEE has no ray tracing options")
        _apply_mapping(eevee.ray_tracing_options, params["raytracing_options"], EEVEE_RAYTRACING, "ray tracing")
    _apply_mapping(eevee, params.get("fast_gi_options"), EEVEE_FAST_GI, "fast GI")
    _apply_mapping(eevee, params.get("volumetrics"), EEVEE_VOLUMETRICS, "volumetrics")
    _apply_mapping(eevee, params.get("clamp"), EEVEE_CLAMP, "clamp")
    _apply_mapping(eevee, params.get("bokeh"), EEVEE_BOKEH, "depth of field")
    if params.get("high_quality_normals") is not None:
        scene.render.use_high_quality_normals = bool(params["high_quality_normals"])
    if params.get("settings"):
        _apply_settings(eevee, params["settings"])
    return _eevee_summary(scene)


LINESET_EDGES = ("silhouette", "border", "crease", "contour", "external_contour", "material_boundary", "edge_mark", "suggestive_contour", "ridge_valley")


def _freestyle_summary(scene, layer):
    settings = layer.freestyle_settings
    return {
        "scene": scene.name,
        "enabled": scene.render.use_freestyle and layer.use_freestyle,
        "line_thickness": scene.render.line_thickness,
        "thickness_mode": scene.render.line_thickness_mode,
        "crease_angle": round(__import__("math").degrees(settings.crease_angle), 2),
        "culling": settings.use_culling,
        "linesets": [{"name": lineset.name, "enabled": lineset.show_render, "edges": [edge for edge in LINESET_EDGES if getattr(lineset, f"select_{edge}")],
                      "visibility": lineset.visibility, "style": lineset.linestyle.name if lineset.linestyle else None,
                      "color": list(lineset.linestyle.color) if lineset.linestyle else None, "thickness": lineset.linestyle.thickness if lineset.linestyle else None}
                     for lineset in settings.linesets],
    }


@command("set_freestyle")
def set_freestyle(params):
    scene = _scene(params)
    layer = bpy.context.view_layer if scene == bpy.context.scene else scene.view_layers[0]
    if params.get("view_layer"):
        layer = scene.view_layers.get(params["view_layer"])
        if layer is None:
            raise CommandError("NotFound", f"no view layer named '{params['view_layer']}'")
    if params.get("enabled") is not None:
        scene.render.use_freestyle = bool(params["enabled"])
        layer.use_freestyle = bool(params["enabled"])
    if params.get("line_thickness") is not None:
        scene.render.line_thickness = float(params["line_thickness"])
    if params.get("thickness_mode"):
        _set_enum(scene.render, "line_thickness_mode", params["thickness_mode"].upper(), "thickness mode")
    settings = layer.freestyle_settings
    if params.get("crease_angle") is not None:
        settings.crease_angle = math.radians(float(params["crease_angle"]))
    if params.get("culling") is not None:
        settings.use_culling = bool(params["culling"])
    for spec in params.get("linesets") or []:
        name = spec.get("name") or "LineSet"
        lineset = settings.linesets.get(name)
        if lineset is None:
            lineset = settings.linesets.new(name)
        if spec.get("enabled") is not None:
            lineset.show_render = bool(spec["enabled"])
        if spec.get("edges") is not None:
            unknown = [edge for edge in spec["edges"] if edge not in LINESET_EDGES]
            if unknown:
                raise CommandError("BadRequest", f"unknown edge types {unknown}", {"known": list(LINESET_EDGES)})
            for edge in LINESET_EDGES:
                setattr(lineset, f"select_{edge}", edge in spec["edges"])
        if spec.get("visibility"):
            _set_enum(lineset, "visibility", spec["visibility"].upper(), "visibility")
        if lineset.linestyle is not None:
            if spec.get("color") is not None:
                lineset.linestyle.color = tuple(spec["color"])[:3]
            if spec.get("thickness") is not None:
                lineset.linestyle.thickness = float(spec["thickness"])
            if spec.get("alpha") is not None:
                lineset.linestyle.alpha = float(spec["alpha"])
    for name in params.get("remove_linesets") or []:
        lineset = settings.linesets.get(name)
        if lineset is not None:
            settings.linesets.remove(lineset)
    return _freestyle_summary(scene, layer)


def _motion_blur_summary(scene):
    render = scene.render
    return {
        "scene": scene.name,
        "enabled": render.use_motion_blur,
        "shutter": render.motion_blur_shutter,
        "position": render.motion_blur_position,
        "rolling_shutter": scene.cycles.rolling_shutter_type,
        "rolling_shutter_duration": scene.cycles.rolling_shutter_duration,
        "steps": scene.eevee.motion_blur_steps,
        "max_blur": scene.eevee.motion_blur_max,
        "depth_scale": scene.eevee.motion_blur_depth_scale,
        "view_layers": {layer.name: layer.use_motion_blur for layer in scene.view_layers},
        "shutter_curve": [[round(point.location[0], 4), round(point.location[1], 4)] for point in render.motion_blur_shutter_curve.curves[0].points],
    }


@command("set_motion_blur")
def set_motion_blur(params):
    scene = _scene(params)
    render = scene.render
    if params.get("enabled") is not None:
        render.use_motion_blur = bool(params["enabled"])
    if params.get("shutter") is not None:
        render.motion_blur_shutter = max(0.0, float(params["shutter"]))
    if params.get("position"):
        _set_enum(render, "motion_blur_position", params["position"].upper(), "shutter position")
    if params.get("rolling_shutter") is not None:
        _set_enum(scene.cycles, "rolling_shutter_type", "TOP" if params["rolling_shutter"] is True else ("NONE" if params["rolling_shutter"] is False else str(params["rolling_shutter"]).upper()), "rolling shutter")
    if params.get("rolling_shutter_duration") is not None:
        scene.cycles.rolling_shutter_duration = float(params["rolling_shutter_duration"])
    if params.get("steps") is not None:
        scene.eevee.motion_blur_steps = max(1, int(params["steps"]))
    if params.get("max_blur") is not None:
        scene.eevee.motion_blur_max = int(params["max_blur"])
    if params.get("depth_scale") is not None:
        scene.eevee.motion_blur_depth_scale = float(params["depth_scale"])
    if params.get("view_layer_enabled") is not None:
        for layer in scene.view_layers:
            layer.use_motion_blur = bool(params["view_layer_enabled"])
    if params.get("shutter_curve") is not None:
        mapping = render.motion_blur_shutter_curve
        _apply_curve_points(mapping.curves[0], params["shutter_curve"])
        mapping.update()
    return _motion_blur_summary(scene)
