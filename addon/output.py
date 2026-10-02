"""Colour management and output: view transform and look, file format with depth and codec, stamps, render region, and the File Output node."""

import os

import bpy

from .compositor import _compositor_tree, _render_layers
from .core import _known_enum, _require, _scene, _set_enum, command
from .rendering import PASSES
from .server import CommandError

COLOR_DEPTHS = ("8", "10", "12", "16", "32")
EXR_CODECS = ("NONE", "ZIP", "PIZ", "DWAA", "DWAB", "HTJ2K", "ZIPS", "RLE", "PXR24", "B44", "B44A")
RESOLUTION_PRESETS = {
    "HD": (1280, 720), "FHD": (1920, 1080), "QHD": (2560, 1440), "UHD": (3840, 2160), "8K": (7680, 4320), "DCI_2K": (2048, 1080), "DCI_4K": (4096, 2160),
    "SQUARE_1K": (1024, 1024), "SQUARE_2K": (2048, 2048), "PORTRAIT_FHD": (1080, 1920), "PORTRAIT_UHD": (2160, 3840), "CINEMASCOPE_2K": (2048, 858), "CINEMASCOPE_4K": (4096, 1716),
}
STAMP_FIELDS = ("time", "date", "frame", "frame_range", "camera", "lens", "scene", "note", "marker", "filename", "sequencer_strip", "render_time", "memory", "hostname")
SLOT_ALIASES = {"image": "Image", "combined": "Image", "beauty": "Image", "alpha": "Alpha", "z": "Depth", "depth": "Depth", "normal": "Normal", "mist": "Mist",
                "ao": "AO", "ambient_occlusion": "AO", "vector": "Vector", "position": "Position", "uv": "UV", "emit": "Emit", "emission": "Emit",
                "environment": "Env", "shadow": "Shadow", "object_index": "IndexOB", "material_index": "IndexMA"}
SOCKET_KINDS = {"RGBA": "RGBA", "VALUE": "FLOAT", "VECTOR": "VECTOR"}
COLOR_PASSES = ("Image", "Alpha", "Noisy Image", "Emit", "Env", "Shadow", "AO", "DiffCol", "GlossCol", "TransCol", "DiffDir", "DiffInd", "GlossDir", "GlossInd", "TransDir", "TransInd")


def _color_summary(scene):
    view = scene.view_settings
    return {
        "scene": scene.name,
        "display_device": scene.display_settings.display_device,
        "view_transform": view.view_transform,
        "look": view.look,
        "exposure": view.exposure,
        "gamma": view.gamma,
        "use_curves": view.use_curve_mapping,
        "white_balance": {"enabled": view.use_white_balance, "temperature": view.white_balance_temperature, "tint": view.white_balance_tint},
        "sequencer_colorspace": scene.sequencer_colorspace_settings.name,
        "known": {"view_transforms": _known_enum(view, "view_transform"), "looks": _known_enum(view, "look"), "display_devices": _known_enum(scene.display_settings, "display_device")},
    }


def _set_look(view, look):
    wanted = look or "None"
    try:
        view.look = wanted
        return
    except TypeError:
        pass
    _set_enum(view, "look", f"{view.view_transform} - {wanted}", "look")


@command("set_color_management")
def set_color_management(params):
    scene = _scene(params)
    view = scene.view_settings
    if params.get("display_device"):
        _set_enum(scene.display_settings, "display_device", params["display_device"], "display device")
    if params.get("view_transform"):
        _set_enum(view, "view_transform", params["view_transform"], "view transform")
    if params.get("look") is not None:
        _set_look(view, params["look"])
    if params.get("exposure") is not None:
        view.exposure = float(params["exposure"])
    if params.get("gamma") is not None:
        view.gamma = float(params["gamma"])
    if params.get("use_curves") is not None:
        view.use_curve_mapping = bool(params["use_curves"])
    balance = params.get("white_balance")
    if isinstance(balance, dict):
        view.use_white_balance = bool(balance.get("enabled", True))
        if balance.get("temperature") is not None:
            view.white_balance_temperature = float(balance["temperature"])
        if balance.get("tint") is not None:
            view.white_balance_tint = float(balance["tint"])
    elif balance is not None:
        view.use_white_balance = bool(balance)
    if params.get("sequencer_colorspace"):
        _set_enum(scene.sequencer_colorspace_settings, "name", params["sequencer_colorspace"], "sequencer colour space")
    return _color_summary(scene)


def _format_summary(settings):
    summary = {"format": settings.file_format, "color_mode": settings.color_mode, "color_depth": settings.color_depth}
    if hasattr(settings, "media_type"):
        summary["media_type"] = settings.media_type
    if settings.file_format in ("OPEN_EXR", "OPEN_EXR_MULTILAYER"):
        summary["exr_codec"] = settings.exr_codec
    if settings.file_format == "PNG":
        summary["compression"] = settings.compression
    if settings.file_format in ("JPEG", "WEBP", "AVIF", "JPEG2000"):
        summary["quality"] = settings.quality
    summary["color_management"] = settings.color_management
    return summary


def _output_summary(scene):
    render = scene.render
    return {
        "scene": scene.name,
        "path": render.filepath,
        "first_file": render.frame_path(frame=scene.frame_start),
        **_format_summary(render.image_settings),
        "overwrite": render.use_overwrite,
        "placeholder": render.use_placeholder,
        "file_extension": render.use_file_extension,
        "stamp": {"enabled": render.use_stamp, "note": render.stamp_note_text, "fields": [field for field in STAMP_FIELDS if getattr(render, f"use_stamp_{field}")]},
        "region": [render.border_min_x, render.border_min_y, render.border_max_x, render.border_max_y] if render.use_border else None,
        "crop_to_region": render.use_crop_to_border,
        "resolution": [render.resolution_x, render.resolution_y, render.resolution_percentage],
        "stereo": _stereo_summary(scene),
        "known": {"color_depths": list(COLOR_DEPTHS), "exr_codecs": list(EXR_CODECS), "presets": {name: list(size) for name, size in RESOLUTION_PRESETS.items()}},
    }


def _apply_format(settings, params):
    if params.get("color_mode"):
        _set_enum(settings, "color_mode", params["color_mode"].upper(), "colour mode")
    if params.get("color_depth") is not None:
        _set_enum(settings, "color_depth", str(params["color_depth"]), "colour depth")
    if params.get("exr_codec"):
        _set_enum(settings, "exr_codec", params["exr_codec"].upper(), "EXR codec")
    if params.get("compression") is not None:
        settings.compression = max(0, min(100, int(params["compression"])))
    if params.get("quality") is not None:
        settings.quality = max(0, min(100, int(params["quality"])))
    if params.get("color_management"):
        _set_enum(settings, "color_management", params["color_management"].upper(), "colour management mode")


def _set_format(settings, file_format):
    wanted = file_format.upper()
    if hasattr(settings, "media_type"):
        settings.media_type = "VIDEO" if wanted == "FFMPEG" else ("MULTI_LAYER_IMAGE" if wanted == "OPEN_EXR_MULTILAYER" else "IMAGE")
    _set_enum(settings, "file_format", wanted, "file format")


def _apply_stamp(render, stamp):
    if isinstance(stamp, dict):
        if stamp.get("enabled") is not None:
            render.use_stamp = bool(stamp["enabled"])
        if stamp.get("note") is not None:
            render.stamp_note_text = str(stamp["note"])
            render.use_stamp_note = bool(stamp["note"])
        if stamp.get("font_size") is not None:
            render.stamp_font_size = int(stamp["font_size"])
        for field in STAMP_FIELDS:
            if stamp.get(field) is not None:
                setattr(render, f"use_stamp_{field}", bool(stamp[field]))
        return
    if stamp is not None:
        render.use_stamp = bool(stamp)


def _apply_stereo(scene, stereo):
    render = scene.render
    if stereo.get("enabled") is not None:
        render.use_multiview = bool(stereo["enabled"])
    if stereo.get("mode"):
        _set_enum(render, "views_format", stereo["mode"].upper(), "multiview mode")
    if stereo.get("format"):
        _set_enum(render.image_settings, "views_format", stereo["format"].upper(), "stereo file format")
    camera = scene.camera.data if scene.camera and scene.camera.type == "CAMERA" else None
    if camera is not None:
        if stereo.get("convergence_mode"):
            _set_enum(camera.stereo, "convergence_mode", stereo["convergence_mode"].upper(), "convergence mode")
        if stereo.get("convergence_distance") is not None:
            camera.stereo.convergence_distance = float(stereo["convergence_distance"])
        if stereo.get("interocular_distance") is not None:
            camera.stereo.interocular_distance = float(stereo["interocular_distance"])
        if stereo.get("pivot"):
            _set_enum(camera.stereo, "pivot", stereo["pivot"].upper(), "stereo pivot")


def _stereo_summary(scene):
    render = scene.render
    summary = {"enabled": render.use_multiview, "mode": render.views_format, "format": render.image_settings.views_format}
    camera = scene.camera.data if scene.camera and scene.camera.type == "CAMERA" else None
    if camera is not None:
        summary.update({"convergence_mode": camera.stereo.convergence_mode, "convergence_distance": camera.stereo.convergence_distance,
                        "interocular_distance": camera.stereo.interocular_distance, "pivot": camera.stereo.pivot})
    return summary


@command("set_output")
def set_output(params):
    scene = _scene(params)
    render = scene.render
    if params.get("preset"):
        preset = str(params["preset"]).upper().replace("-", "_")
        if preset not in RESOLUTION_PRESETS:
            raise CommandError("BadRequest", f"unknown resolution preset '{params['preset']}'", {"known": list(RESOLUTION_PRESETS)})
        render.resolution_x, render.resolution_y = RESOLUTION_PRESETS[preset]
        render.resolution_percentage = 100
    if isinstance(params.get("stereo"), dict):
        _apply_stereo(scene, params["stereo"])
    if params.get("path"):
        render.filepath = params["path"]
    if params.get("file_format"):
        _set_format(render.image_settings, params["file_format"])
    _apply_format(render.image_settings, {key: value for key, value in params.items() if key != "file_format"})
    if params.get("overwrite") is not None:
        render.use_overwrite = bool(params["overwrite"])
    if params.get("placeholder") is not None:
        render.use_placeholder = bool(params["placeholder"])
    if params.get("file_extension") is not None:
        render.use_file_extension = bool(params["file_extension"])
    _apply_stamp(render, params.get("stamp"))
    if "region" in params:
        region = params["region"]
        render.use_border = region is not None
        if region is not None:
            if len(region) != 4:
                raise CommandError("BadRequest", "region must be [minX, minY, maxX, maxY] as fractions of the frame, 0 to 1")
            render.border_min_x, render.border_min_y, render.border_max_x, render.border_max_y = (max(0.0, min(1.0, float(value))) for value in region)
    if params.get("crop_to_region") is not None:
        render.use_crop_to_border = bool(params["crop_to_region"])
    return _output_summary(scene)


def _file_output_node(tree, name, create):
    node = tree.nodes.get(name)
    if node is not None and node.bl_idname != "CompositorNodeOutputFile":
        raise CommandError("BadRequest", f"'{name}' is a {node.bl_idname}, not a File Output node")
    if node is None and create:
        node = tree.nodes.new("CompositorNodeOutputFile")
        node.name = name
        node.label = name
        node.location = (600, -300)
        if hasattr(node, "file_name"):
            node.file_name = ""
    if node is None:
        raise CommandError("NotFound", f"the compositor has no File Output node '{name}'", {"nodes": [node.name for node in tree.nodes if node.bl_idname == "CompositorNodeOutputFile"]})
    return node


def _slot_inputs(node):
    return [socket for socket in node.inputs if socket.name]


def _clear_slots(node):
    if hasattr(node, "file_output_items"):
        node.file_output_items.clear()
        return
    while len(node.file_slots) > 0:
        node.file_slots.remove(node.inputs[0])


def _new_slot(node, name, socket):
    kind = SOCKET_KINDS.get(socket.type, "RGBA")
    if hasattr(node, "file_output_items"):
        node.file_output_items.new(kind, name)
        return node.inputs[len(_slot_inputs(node)) - 1]
    node.file_slots.new(name)
    return node.inputs[len(node.inputs) - 1]


def _slot_format(node, index, spec, source_name):
    item = node.file_output_items[index] if hasattr(node, "file_output_items") else node.file_slots[index]
    if hasattr(item, "save_as_render"):
        wanted = spec.get("save_as_render")
        item.save_as_render = bool(wanted) if wanted is not None else (source_name in COLOR_PASSES and node.format.file_format not in ("OPEN_EXR", "OPEN_EXR_MULTILAYER"))
    if not any(spec.get(key) is not None for key in ("file_format", "color_depth", "exr_codec", "compression", "quality")):
        return
    if hasattr(item, "override_node_format"):
        item.override_node_format = True
    else:
        item.use_node_format = False
    if spec.get("file_format"):
        _set_format(item.format, spec["file_format"])
    _apply_format(item.format, {key: value for key, value in spec.items() if key != "file_format"})


def _pass_socket(layers, wanted):
    name = SLOT_ALIASES.get(str(wanted).lower(), wanted)
    socket = layers.outputs.get(name)
    if socket is None:
        attribute = PASSES.get(str(wanted).lower())
        if attribute is not None and hasattr(bpy.context.view_layer, attribute):
            setattr(bpy.context.view_layer, attribute, True)
            socket = layers.outputs.get(name)
    if socket is None or not socket.enabled:
        raise CommandError("NotFound", f"the Render Layers node has no enabled pass '{wanted}'", {"enabled": [socket.name for socket in layers.outputs if socket.enabled], "hint": "blender_render_passes or blender_view_layer enables passes"})
    return socket


def _file_output_summary(tree, node):
    directory = node.directory if hasattr(node, "directory") else node.base_path
    slots = []
    items = node.file_output_items if hasattr(node, "file_output_items") else node.file_slots
    for index, socket in enumerate(_slot_inputs(node)):
        source = next((link.from_socket for link in tree.links if link.to_socket == socket), None)
        slot = {"name": socket.name, "from": f"{source.node.name}.{source.name}" if source else None}
        if index < len(items) and hasattr(items[index], "save_as_render"):
            slot["save_as_render"] = items[index].save_as_render
        slots.append(slot)
    summary = {"node": node.name, "directory": directory, **_format_summary(node.format), "slots": slots}
    if hasattr(node, "file_name"):
        summary["file_name"] = node.file_name
    return summary


@command("file_output")
def file_output(params):
    tree = _compositor_tree()
    action = params.get("action") or "configure"
    name = params.get("name") or "File Output"
    if action == "info":
        return {"nodes": [_file_output_summary(tree, node) for node in tree.nodes if node.bl_idname == "CompositorNodeOutputFile"]}
    if action == "remove":
        tree.nodes.remove(_file_output_node(tree, name, False))
        return {"removed": name, "nodes": [node.name for node in tree.nodes if node.bl_idname == "CompositorNodeOutputFile"]}
    if action != "configure":
        raise CommandError("BadRequest", "action must be configure, info or remove")
    node = _file_output_node(tree, name, True)
    if params.get("directory"):
        directory = params["directory"]
        os.makedirs(directory, exist_ok=True)
        if hasattr(node, "directory"):
            node.directory = directory if directory.endswith(os.sep) else directory + os.sep
        else:
            node.base_path = directory
    if params.get("file_name") is not None and hasattr(node, "file_name"):
        node.file_name = params["file_name"]
    if params.get("file_format"):
        _set_format(node.format, params["file_format"])
    _apply_format(node.format, {key: value for key, value in params.items() if key != "file_format"})
    layers = _render_layers(tree)
    slots = list(params.get("slots") or [])
    if params.get("all_passes"):
        slots = [socket.name for socket in layers.outputs if socket.enabled]
    if slots:
        _clear_slots(node)
        for index, spec in enumerate(slots):
            spec = {"pass": spec} if isinstance(spec, str) else dict(spec)
            source = _pass_socket(layers, _require(spec, "pass"))
            target = _new_slot(node, spec.get("name") or source.name, source)
            tree.links.new(source, target)
            _slot_format(node, index, spec, source.name)
    return _file_output_summary(tree, node)
