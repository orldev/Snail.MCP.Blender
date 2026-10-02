"""Renders to disk and to the model's eye, captures, bakes, render settings and passes."""

import math
import os
import re
import time

import bpy
import mathutils
from bpy.app.handlers import persistent

from .core import _name, _object, _require, _scene, command
from .diagnostics import PROGRESS
from .images import _with_preview
from .modeling import _object_override
from .objects import _window_override
from .serialize import to_json
from .server import CommandError

MAX_RESOLUTION = 4096


ENGINES = {"EEVEE": "BLENDER_EEVEE_NEXT", "EEVEE_NEXT": "BLENDER_EEVEE_NEXT", "CYCLES": "CYCLES", "WORKBENCH": "BLENDER_WORKBENCH"}


PASSES = {
    "combined": "use_pass_combined", "z": "use_pass_z", "depth": "use_pass_z", "mist": "use_pass_mist", "normal": "use_pass_normal",
    "position": "use_pass_position", "vector": "use_pass_vector", "uv": "use_pass_uv", "object_index": "use_pass_object_index",
    "material_index": "use_pass_material_index", "diffuse_direct": "use_pass_diffuse_direct", "diffuse_indirect": "use_pass_diffuse_indirect",
    "diffuse_color": "use_pass_diffuse_color", "glossy_direct": "use_pass_glossy_direct", "glossy_indirect": "use_pass_glossy_indirect",
    "glossy_color": "use_pass_glossy_color", "transmission_direct": "use_pass_transmission_direct",
    "transmission_indirect": "use_pass_transmission_indirect", "transmission_color": "use_pass_transmission_color",
    "emit": "use_pass_emit", "environment": "use_pass_environment", "ambient_occlusion": "use_pass_ambient_occlusion",
    "shadow": "use_pass_shadow", "cryptomatte_object": "use_pass_cryptomatte_object", "cryptomatte_material": "use_pass_cryptomatte_material",
    "cryptomatte_asset": "use_pass_cryptomatte_asset",
}


def _set_output_format(render, file_format):
    """Blender 5 gates video formats behind media_type; picking FFMPEG without it fails with an enum error."""
    settings = render.image_settings
    wanted = file_format.upper()
    if hasattr(settings, "media_type"):
        settings.media_type = "VIDEO" if wanted == "FFMPEG" else ("MULTI_LAYER_IMAGE" if wanted == "OPEN_EXR_MULTILAYER" else "IMAGE")
    settings.file_format = wanted


def _memory_of(text):
    peak = 0.0
    for value, unit in re.findall(r"(?:Mem|Peak)[:\s]*([\d.]+)\s*([KMG])", text):
        peak = max(peak, float(value) * {"K": 1 / 1024, "M": 1.0, "G": 1024.0}[unit])
    return round(peak, 1)


@persistent
def _on_render_init(scene, *_):
    PROGRESS.update({"active": True, "phase": "starting", "frame": scene.frame_current, "frames_done": 0, "started_at": time.time(), "elapsed_s": 0.0, "stats": None, "peak_memory_mb": 0.0, "files": []})


@persistent
def _on_render_pre(scene, *_):
    PROGRESS.update({"phase": "rendering", "frame": scene.frame_current})


@persistent
def _on_render_stats(*arguments):
    text = next((argument for argument in arguments if isinstance(argument, str)), "")
    PROGRESS["stats"] = text
    PROGRESS["peak_memory_mb"] = max(PROGRESS["peak_memory_mb"], _memory_of(text))


@persistent
def _on_render_write(scene, *_):
    PROGRESS["frames_done"] += 1
    PROGRESS["files"] = (PROGRESS["files"] + [scene.render.frame_path(frame=scene.frame_current)])[-20:]


@persistent
def _on_render_done(*_):
    PROGRESS.update({"active": False, "phase": "done"})


@persistent
def _on_render_cancel(*_):
    PROGRESS.update({"active": False, "phase": "cancelled"})


def _watch_renders():
    for handlers, callback in ((bpy.app.handlers.render_init, _on_render_init), (bpy.app.handlers.render_pre, _on_render_pre),
                               (bpy.app.handlers.render_stats, _on_render_stats), (bpy.app.handlers.render_write, _on_render_write),
                               (bpy.app.handlers.render_complete, _on_render_done), (bpy.app.handlers.render_cancel, _on_render_cancel)):
        if callback not in handlers:
            handlers.append(callback)


_watch_renders()


def _timed_render(**arguments):
    """Runs the render operator and measures it: wall time and the peak memory Blender reports in its stats line."""
    stats = []

    def handler(*arguments):
        stats.append(next((argument for argument in arguments if isinstance(argument, str)), ""))

    bpy.app.handlers.render_stats.append(handler)
    started = time.perf_counter()
    try:
        with _window_override():
            bpy.ops.render.render(**arguments)
    finally:
        bpy.app.handlers.render_stats.remove(handler)
    return {"duration_ms": round((time.perf_counter() - started) * 1000), "peak_memory_mb": _peak_memory(stats), "stats": stats[-1] if stats else None}


def _peak_memory(stats):
    peak = 0.0
    for line in stats:
        for value, unit in re.findall(r"(?:Mem|Peak)[:\s]*([\d.]+)\s*([KMG])", line):
            peak = max(peak, float(value) * {"K": 1 / 1024, "M": 1.0, "G": 1024.0}[unit])
    return round(peak, 1)


def _render_arguments(params):
    arguments = {}
    if params.get("scene"):
        arguments["scene"] = _scene(params).name
    if params.get("view_layer"):
        scene = _scene(params)
        if params["view_layer"] not in scene.view_layers:
            raise CommandError("NotFound", f"no view layer named '{params['view_layer']}'", {"known": [layer.name for layer in scene.view_layers]})
        arguments["layer"] = params["view_layer"]
    return arguments


def _render_summary(scene):
    render = scene.render
    summary = {
        "engine": render.engine,
        "resolution": [render.resolution_x, render.resolution_y],
        "percentage": render.resolution_percentage,
        "frame": {"current": scene.frame_current, "start": scene.frame_start, "end": scene.frame_end, "fps": render.fps},
        "output": {"path": render.filepath, "format": render.image_settings.file_format, "color_mode": render.image_settings.color_mode},
        "camera": _name(scene.camera),
        "film_transparent": render.film_transparent,
        "filter_size": render.filter_size,
        "use_compositing": render.use_compositing,
    }
    if render.engine == "CYCLES":
        summary["samples"] = scene.cycles.samples
        summary["device"] = scene.cycles.device
        summary["denoise"] = scene.cycles.use_denoising
    elif render.engine.startswith("BLENDER_EEVEE"):
        summary["samples"] = scene.eevee.taa_render_samples
    return summary


@command("render_settings")
def render_settings(params):
    scene = _scene(params)
    render = scene.render
    if params.get("engine"):
        wanted = params["engine"].upper()
        engine = ENGINES.get(wanted, wanted)
        known = [item.identifier for item in bpy.types.RenderSettings.bl_rna.properties["engine"].enum_items]
        if engine == "BLENDER_EEVEE_NEXT" and engine not in known and "BLENDER_EEVEE" in known:
            engine = "BLENDER_EEVEE"
        if engine not in known:
            raise CommandError("BadRequest", f"unknown render engine '{params['engine']}'", {"known": known})
        render.engine = engine
    for axis in ("x", "y"):
        key = f"resolution_{axis}"
        if params.get(key) is not None:
            value = int(params[key])
            if value < 1 or value > MAX_RESOLUTION:
                raise CommandError("BadRequest", f"{key} must be between 1 and {MAX_RESOLUTION}")
            setattr(render, key, value)
    if params.get("percentage") is not None:
        render.resolution_percentage = max(1, min(100, int(params["percentage"])))
    if params.get("samples") is not None:
        samples = max(1, int(params["samples"]))
        scene.cycles.samples = samples
        scene.eevee.taa_render_samples = samples
    if params.get("denoise") is not None:
        scene.cycles.use_denoising = bool(params["denoise"])
    if params.get("device"):
        scene.cycles.device = params["device"].upper()
    if params.get("file_format"):
        _set_output_format(render, params["file_format"])
    if params.get("color_mode"):
        render.image_settings.color_mode = params["color_mode"].upper()
    if params.get("film_transparent") is not None:
        render.film_transparent = bool(params["film_transparent"])
    if params.get("filter_size") is not None:
        render.filter_size = max(0.01, min(10.0, float(params["filter_size"])))
    if params.get("output_path"):
        render.filepath = params["output_path"]
    if params.get("camera"):
        camera = _object(params["camera"])
        if camera.type != "CAMERA":
            raise CommandError("BadRequest", f"'{camera.name}' is not a camera")
        scene.camera = camera
    return _render_summary(scene)


@command("render_image")
def render_image(params):
    scene = _scene(params)
    if scene.camera is None:
        raise CommandError("BadRequest", "the scene has no camera; blender_add_camera adds one")
    path = _require(params, "path")
    directory = os.path.dirname(path)
    if directory:
        os.makedirs(directory, exist_ok=True)
    arguments = _render_arguments(params)
    previous = scene.render.filepath
    scene.render.filepath = path
    if params.get("frame") is not None:
        scene.frame_set(int(params["frame"]))
    try:
        timing = _timed_render(write_still=True, **arguments)
    finally:
        scene.render.filepath = previous
    written = path if os.path.isfile(path) else next((candidate for candidate in _candidates(path) if os.path.isfile(candidate)), None)
    if written is None:
        raise CommandError("RenderFailed", f"Blender rendered but no file appeared at '{path}'")
    result = {"path": written, "bytes": os.path.getsize(written), "frame": scene.frame_current, "engine": scene.render.engine,
              "resolution": [scene.render.resolution_x, scene.render.resolution_y], "percentage": scene.render.resolution_percentage,
              "scene": scene.name, "view_layer": arguments.get("layer"), **timing}
    return _with_preview(result, written, params)


def _candidates(path):
    root, _extension = os.path.splitext(path)
    for candidate in (".png", ".jpg", ".jpeg", ".exr", ".tif", ".tiff", ".bmp", ".webp"):
        yield root + candidate
        yield path + candidate


@command("render_animation")
def render_animation(params):
    scene = _scene(params)
    if scene.camera is None:
        raise CommandError("BadRequest", "the scene has no camera; blender_add_camera adds one")
    output = _require(params, "output_path")
    directory = os.path.dirname(output)
    if directory:
        os.makedirs(directory, exist_ok=True)
    arguments = _render_arguments(params)
    scene.render.filepath = output
    if params.get("start") is not None:
        scene.frame_start = int(params["start"])
    if params.get("end") is not None:
        scene.frame_end = int(params["end"])
    if params.get("step") is not None:
        scene.frame_step = max(1, int(params["step"]))
    if params.get("file_format"):
        _set_output_format(scene.render, params["file_format"])
    timing = _timed_render(animation=True, **arguments)
    first_file = scene.render.frame_path(frame=scene.frame_start)
    result = {"output_path": output, "frames": [scene.frame_start, scene.frame_end], "step": scene.frame_step, "format": scene.render.image_settings.file_format,
              "engine": scene.render.engine, "first_file": first_file, "scene": scene.name, **timing}
    return _with_preview(result, first_file, params) if os.path.isfile(first_file) and scene.render.image_settings.file_format != "FFMPEG" else result


@command("render_passes")
def render_passes(params):
    view_layer = bpy.context.view_layer
    for key, enabled in (params.get("passes") or {}).items():
        attribute = PASSES.get(key)
        if attribute is None or not hasattr(view_layer, attribute):
            raise CommandError("BadRequest", f"unknown render pass '{key}'", {"known": sorted(name for name, attribute in PASSES.items() if hasattr(view_layer, attribute))})
        setattr(view_layer, attribute, bool(enabled))
    return {
        "view_layer": view_layer.name,
        "passes": {name: getattr(view_layer, attribute) for name, attribute in PASSES.items() if hasattr(view_layer, attribute)},
    }


@command("render_object")
def render_object(params):
    scene = bpy.context.scene
    names = params.get("names") or [_require(params, "name")]
    subjects = [_object(name) for name in names]
    path = _require(params, "path")
    corners = [item.matrix_world @ mathutils.Vector(corner) for item in subjects for corner in item.bound_box]
    if not corners:
        raise CommandError("BadRequest", "the objects have no geometry to frame")
    center = sum(corners, mathutils.Vector()) / len(corners)
    radius = max((corner - center).length for corner in corners) or 1.0
    azimuth = math.radians(float(params.get("azimuth") if params.get("azimuth") is not None else 40.0))
    elevation = math.radians(float(params.get("elevation") if params.get("elevation") is not None else 25.0))
    direction = mathutils.Vector((math.cos(elevation) * math.cos(azimuth), math.cos(elevation) * math.sin(azimuth), math.sin(elevation)))
    lens = float(params.get("lens") or 50.0)
    camera_data = bpy.data.cameras.new("SnailShot")
    camera_data.lens = lens
    fov = 2 * math.atan(camera_data.sensor_width / (2 * lens))
    aspect = scene.render.resolution_x / max(1, scene.render.resolution_y)
    fov = min(fov, fov * aspect) if aspect < 1 else fov
    distance = radius / math.sin(fov / 2) * float(params.get("margin") or 1.15)
    camera = bpy.data.objects.new("SnailShot", camera_data)
    scene.collection.objects.link(camera)
    camera.location = center + direction * distance
    camera.rotation_euler = (-direction).to_track_quat("-Z", "Y").to_euler()
    hidden = []
    previous_camera, previous_transparent = scene.camera, scene.render.film_transparent
    previous_resolution = (scene.render.resolution_x, scene.render.resolution_y)
    try:
        for item in scene.objects:
            if item.type in ("MESH", "CURVE", "SURFACE", "META", "FONT", "GREASEPENCIL", "VOLUME") and item not in subjects and not item.hide_render:
                item.hide_render = True
                hidden.append(item)
        scene.camera = camera
        if params.get("transparent", True):
            scene.render.film_transparent = True
        if params.get("resolution_x") is not None:
            scene.render.resolution_x = int(params["resolution_x"])
        if params.get("resolution_y") is not None:
            scene.render.resolution_y = int(params["resolution_y"])
        result = render_image({"path": path, "preview": params.get("preview", True), "preview_size": params.get("preview_size")})
    finally:
        for item in hidden:
            item.hide_render = False
        scene.camera = previous_camera
        scene.render.film_transparent = previous_transparent
        scene.render.resolution_x, scene.render.resolution_y = previous_resolution
        if not params.get("keep_camera"):
            bpy.data.objects.remove(camera, do_unlink=True)
            bpy.data.cameras.remove(camera_data)
    result.update({"objects": names, "center": to_json(center), "radius": radius, "camera_kept": bool(params.get("keep_camera"))})
    return result


@command("viewport_capture")
def viewport_capture(params):
    scene = bpy.context.scene
    path = _require(params, "path")
    for window in bpy.context.window_manager.windows:
        for area in window.screen.areas:
            if area.type != "VIEW_3D":
                continue
            region = next((region for region in area.regions if region.type == "WINDOW"), None)
            directory = os.path.dirname(path)
            if directory:
                os.makedirs(directory, exist_ok=True)
            previous = scene.render.filepath
            scene.render.filepath = path
            try:
                with bpy.context.temp_override(window=window, screen=window.screen, area=area, region=region):
                    bpy.ops.render.opengl(write_still=True, view_context=True)
            finally:
                scene.render.filepath = previous
            written = path if os.path.isfile(path) else next((candidate for candidate in _candidates(path) if os.path.isfile(candidate)), None)
            if written is None:
                raise CommandError("RenderFailed", f"the viewport was captured but no file appeared at '{path}'")
            return _with_preview({"path": written, "bytes": os.path.getsize(written), "shading": area.spaces.active.shading.type}, written, params)
    raise CommandError("NoArea", "no 3D viewport is open in Blender; viewport capture needs the interface")


BAKE_TYPES = ("COMBINED", "AO", "SHADOW", "POSITION", "NORMAL", "UV", "ROUGHNESS", "EMIT", "ENVIRONMENT", "DIFFUSE", "GLOSSY", "TRANSMISSION")


@command("bake_texture")
def bake_texture(params):
    item = _object(_require(params, "name"))
    if item.type != "MESH":
        raise CommandError("BadRequest", f"'{item.name}' is a {item.type}, not a mesh")
    if not item.data.uv_layers:
        raise CommandError("BadRequest", f"'{item.name}' has no UV map; unwrap it first")
    kind = (params.get("type") or "DIFFUSE").upper()
    if kind not in BAKE_TYPES:
        raise CommandError("BadRequest", f"unknown bake type '{kind}'", {"known": list(BAKE_TYPES)})
    path = _require(params, "path")
    width = int(params.get("width") or 1024)
    height = int(params.get("height") or width)
    if width > MAX_RESOLUTION or height > MAX_RESOLUTION:
        raise CommandError("BadRequest", f"bake size must be at most {MAX_RESOLUTION} per edge")
    materials = [slot.material for slot in item.material_slots if slot.material]
    if not materials:
        raise CommandError("BadRequest", f"'{item.name}' has no material to bake")
    image = bpy.data.images.new(params.get("image_name") or f"{item.name}_{kind.lower()}", width, height, alpha=True)
    if kind in ("NORMAL", "ROUGHNESS", "AO", "POSITION", "UV"):
        image.colorspace_settings.name = "Non-Color"
    nodes = []
    for material in materials:
        material.use_nodes = True
        node = material.node_tree.nodes.new("ShaderNodeTexImage")
        node.image = image
        node.name = "Snail Bake Target"
        material.node_tree.nodes.active = node
        nodes.append((material, node))
    scene = bpy.context.scene
    previous_engine, previous_samples = scene.render.engine, scene.cycles.samples
    scene.render.engine = "CYCLES"
    scene.cycles.samples = int(params.get("samples") or 16)
    try:
        with _object_override([item], item):
            bpy.ops.object.bake(type=kind, margin=int(params.get("margin") or 16), use_clear=True, use_selected_to_active=False)
        directory = os.path.dirname(path)
        if directory:
            os.makedirs(directory, exist_ok=True)
        image.filepath_raw = path
        image.file_format = "PNG"
        image.save()
    finally:
        scene.render.engine = previous_engine
        scene.cycles.samples = previous_samples
        if not params.get("keep_node"):
            for material, node in nodes:
                material.node_tree.nodes.remove(node)
    result = {"name": item.name, "type": kind, "image": image.name, "path": path, "size": [width, height], "bytes": os.path.getsize(path) if os.path.isfile(path) else 0}
    return _with_preview(result, path, params) if os.path.isfile(path) else result
