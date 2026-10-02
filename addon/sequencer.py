"""Video Sequence Editor commands: strips, effects, trims, fades, cuts, proxies, timing, grading, the encode and the audio mixdown."""

import contextlib
import math
import os

import bpy

from .core import _known_enum, _name, _require, _set_enum, command
from .modeling import _apply_settings
from .objects import _window_override
from .optics import _apply_curve_points
from .rendering import _set_output_format
from .serialize import to_json
from .server import CommandError

STRIP_KINDS = ("movie", "image", "sound", "scene", "color", "text")
EFFECT_TYPES = ("CROSS", "ADD", "SUBTRACT", "ALPHA_OVER", "ALPHA_UNDER", "GAMMA_CROSS", "COMPOSITOR", "MULTIPLY", "WIPE", "GLOW",
                "COLOR", "SPEED", "MULTICAM", "ADJUSTMENT", "GAUSSIAN_BLUR", "TEXT", "COLORMIX")
TWO_INPUT_EFFECTS = ("CROSS", "ADD", "SUBTRACT", "ALPHA_OVER", "ALPHA_UNDER", "GAMMA_CROSS", "MULTIPLY", "WIPE", "COLORMIX")
CONTAINERS = {"mp4": "MPEG4", "mkv": "MKV", "webm": "WEBM", "mov": "QUICKTIME", "avi": "AVI", "ogv": "OGG"}
STRIP_MODIFIERS = ("BRIGHT_CONTRAST", "COLOR_BALANCE", "CURVES", "HUE_CORRECT", "MASK", "TONEMAP", "WHITE_BALANCE", "SOUND_EQUALIZER", "PITCH", "ECHO")
PRORES_PROFILES = ("422_PROXY", "422_LT", "422_STD", "422_HQ", "4444", "4444_XQ")
AUDIO_CONTAINERS = {"wav": "WAV", "flac": "FLAC", "mp3": "MP3", "ogg": "OGG", "aac": "AAC", "ac3": "AC3", "mp2": "MP2", "mkv": "MATROSKA"}
AUDIO_CODECS = {"WAV": "PCM", "FLAC": "FLAC", "MP3": "MP3", "OGG": "VORBIS", "AAC": "AAC", "AC3": "AC3", "MP2": "MP2", "MATROSKA": "FLAC"}
FRACTIONAL_RATES = {23.976: (24, 1.001), 23.98: (24, 1.001), 29.97: (30, 1.001), 47.952: (48, 1.001), 47.95: (48, 1.001), 59.94: (60, 1.001), 119.88: (120, 1.001)}
IMAGE_EXTENSIONS = (".png", ".jpg", ".jpeg", ".exr", ".tif", ".tiff", ".bmp", ".webp", ".dpx", ".tga", ".hdr")


def _editor():
    return bpy.context.scene.sequence_editor_create()


def _strip(name):
    editor = _editor()
    strip = editor.strips_all.get(name)
    if strip is None:
        raise CommandError("NotFound", f"no strip named '{name}'", {"strips": [strip.name for strip in editor.strips_all]})
    return strip


def _strip_summary(strip):
    summary = {
        "name": strip.name,
        "type": strip.type,
        "channel": strip.channel,
        "start": strip.frame_final_start,
        "end": strip.frame_final_end,
        "duration": strip.frame_final_duration,
        "trim": [strip.frame_offset_start, strip.frame_offset_end],
        "mute": strip.mute,
        "blend_type": strip.blend_type,
        "blend_alpha": strip.blend_alpha,
    }
    if hasattr(strip, "filepath"):
        summary["path"] = strip.filepath
    if hasattr(strip, "sound") and strip.sound is not None:
        summary["path"] = strip.sound.filepath
        summary["volume"] = strip.volume
    if hasattr(strip, "directory"):
        summary["path"] = strip.directory
    if strip.type == "TEXT":
        summary["text"] = strip.text
        summary["font_size"] = strip.font_size
    if strip.type == "COLOR":
        summary["color"] = to_json(strip.color)
    if strip.type == "SCENE":
        summary["scene"] = _name(strip.scene)
    if hasattr(strip, "input_1") and strip.input_1 is not None:
        summary["inputs"] = [strip.input_1.name] + ([strip.input_2.name] if getattr(strip, "input_2", None) else [])
    if hasattr(strip, "transform"):
        transform = strip.transform
        summary["transform"] = {"offset": [transform.offset_x, transform.offset_y], "scale": [transform.scale_x, transform.scale_y], "rotation": round(math.degrees(transform.rotation), 3)}
    if hasattr(strip, "crop"):
        summary["crop"] = [strip.crop.min_x, strip.crop.max_x, strip.crop.min_y, strip.crop.max_y]
    if hasattr(strip, "elements") and strip.type == "IMAGE":
        summary["frames"] = len(strip.elements)
    if hasattr(strip, "modifiers") and len(strip.modifiers) > 0:
        summary["modifiers"] = [{"name": modifier.name, "type": modifier.type, "mute": modifier.mute} for modifier in strip.modifiers]
    if getattr(strip, "use_proxy", False) and strip.proxy is not None:
        summary["proxy"] = [size for size in (25, 50, 75, 100) if getattr(strip.proxy, f"build_{size}")]
    return summary


def _apply_strip_settings(strip, params):
    if params.get("channel") is not None:
        strip.channel = int(params["channel"])
    if params.get("frame_start") is not None:
        strip.frame_start = int(params["frame_start"])
    if params.get("length") is not None and hasattr(strip, "frame_final_duration"):
        strip.frame_final_duration = int(params["length"])
    if params.get("trim_start") is not None:
        strip.frame_offset_start = int(params["trim_start"])
    if params.get("trim_end") is not None:
        strip.frame_offset_end = int(params["trim_end"])
    if params.get("mute") is not None:
        strip.mute = bool(params["mute"])
    if params.get("blend_type"):
        strip.blend_type = params["blend_type"].upper()
    if params.get("opacity") is not None:
        strip.blend_alpha = float(params["opacity"])
    if params.get("volume") is not None and hasattr(strip, "volume"):
        strip.volume = float(params["volume"])
    if params.get("text") is not None and strip.type == "TEXT":
        strip.text = params["text"]
    if params.get("color") is not None and strip.type == "COLOR":
        strip.color = tuple(params["color"])[:3]
    transform = params.get("transform")
    if isinstance(transform, dict) and hasattr(strip, "transform"):
        if transform.get("offset") is not None:
            strip.transform.offset_x, strip.transform.offset_y = (float(value) for value in transform["offset"])
        if transform.get("scale") is not None:
            strip.transform.scale_x, strip.transform.scale_y = (float(value) for value in transform["scale"])
        if transform.get("rotation") is not None:
            strip.transform.rotation = math.radians(float(transform["rotation"]))
        if transform.get("crop") is not None and hasattr(strip, "crop"):
            strip.crop.min_x, strip.crop.max_x, strip.crop.min_y, strip.crop.max_y = (int(value) for value in transform["crop"])
        if transform.get("flip") is not None:
            strip.use_flip_x, strip.use_flip_y = (bool(value) for value in transform["flip"])
    if params.get("settings"):
        _apply_settings(strip, params["settings"])
    fade = params.get("fade")
    if isinstance(fade, dict):
        _fade(strip, int(fade.get("in") or 0), int(fade.get("out") or 0))


def _fade(strip, fade_in, fade_out):
    path = "volume" if strip.type == "SOUND" else "blend_alpha"
    full = strip.volume if strip.type == "SOUND" else 1.0
    start, end = strip.frame_final_start, strip.frame_final_end
    if fade_in > 0:
        setattr(strip, path, 0.0)
        strip.keyframe_insert(data_path=path, frame=start)
        setattr(strip, path, full)
        strip.keyframe_insert(data_path=path, frame=start + fade_in)
    if fade_out > 0:
        setattr(strip, path, full)
        strip.keyframe_insert(data_path=path, frame=end - fade_out)
        setattr(strip, path, 0.0)
        strip.keyframe_insert(data_path=path, frame=end)
    setattr(strip, path, full)


@command("sequencer_info")
def sequencer_info(params):
    """The edit as it stands; a scene without a sequence editor reports no strips instead of being given an editor by a read."""
    scene = bpy.context.scene
    editor = scene.sequence_editor
    render = scene.render
    strips = editor.strips_all if editor is not None else []
    return {
        "strips": [_strip_summary(strip) for strip in sorted(strips, key=lambda strip: (strip.channel, strip.frame_final_start))],
        "frame": {"start": scene.frame_start, "end": scene.frame_end, "fps": render.fps},
        "resolution": [render.resolution_x, render.resolution_y],
        "output": {"path": render.filepath, "format": render.image_settings.file_format,
                   "container": render.ffmpeg.format, "codec": render.ffmpeg.codec, "audio": render.ffmpeg.audio_codec},
        "use_sequencer": render.use_sequencer,
    }


@command("sequencer_add_strip")
def sequencer_add_strip(params):
    kind = _require(params, "type").lower()
    if kind not in STRIP_KINDS:
        raise CommandError("BadRequest", f"unknown strip type '{kind}'", {"known": list(STRIP_KINDS)})
    strips = _editor().strips
    name = params.get("name") or kind.title()
    channel = int(params.get("channel") or _free_channel())
    frame_start = int(params.get("frame_start") if params.get("frame_start") is not None else bpy.context.scene.frame_start)
    if kind in ("movie", "sound") or (kind == "image" and not params.get("files")):
        path = _require(params, "path")
        if not os.path.exists(path):
            raise CommandError("NotFound", f"no file at '{path}'")
    if kind == "movie":
        strip = strips.new_movie(name=name, filepath=params["path"], channel=channel, frame_start=frame_start)
        if params.get("with_sound", True):
            with contextlib.suppress(RuntimeError):
                strips.new_sound(name=f"{name} Audio", filepath=params["path"], channel=channel + 1, frame_start=frame_start)
    elif kind == "image":
        files = _image_files(params)
        strip = strips.new_image(name=name, filepath=files[0], channel=channel, frame_start=frame_start)
        for path in files[1:]:
            strip.elements.append(os.path.basename(path))
        if params.get("length") is not None and len(files) == 1:
            strip.frame_final_duration = int(params["length"])
    elif kind == "sound":
        strip = strips.new_sound(name=name, filepath=params["path"], channel=channel, frame_start=frame_start)
    elif kind == "scene":
        scene_name = params.get("scene") or bpy.context.scene.name
        source = bpy.data.scenes.get(scene_name)
        if source is None:
            raise CommandError("NotFound", f"no scene named '{scene_name}'", {"scenes": [scene.name for scene in bpy.data.scenes]})
        strip = strips.new_scene(name=name, scene=source, channel=channel, frame_start=frame_start)
    else:
        length = int(params.get("length") or 48)
        strip = strips.new_effect(name=name, type=kind.upper(), channel=channel, frame_start=frame_start, length=length)
        if kind == "text":
            strip.text = params.get("text") or name
            if params.get("font_size") is not None:
                strip.font_size = float(params["font_size"])
            if params.get("color") is not None:
                color = list(params["color"])
                strip.color = [*color, 1.0] if len(color) == 3 else color
            if params.get("location") is not None:
                strip.location = tuple(params["location"])
        elif params.get("color") is not None:
            strip.color = tuple(params["color"])[:3]
    _apply_strip_settings(strip, {key: value for key, value in params.items() if key not in ("channel", "frame_start", "length", "color", "text")})
    return _strip_summary(strip)


def _image_files(params):
    """One image, a list of files from one folder, or every image in a folder in name order: an image sequence."""
    files = list(params.get("files") or [])
    if not files:
        path = params["path"]
        files = sorted(os.path.join(path, entry) for entry in os.listdir(path) if entry.lower().endswith(IMAGE_EXTENSIONS)) if os.path.isdir(path) else [path]
    if not files:
        raise CommandError("NotFound", f"no image files in '{params.get('path')}'")
    missing = [path for path in files if not os.path.isfile(path)]
    if missing:
        raise CommandError("NotFound", f"missing image files: {', '.join(missing[:5])}")
    if len({os.path.dirname(path) for path in files}) > 1:
        raise CommandError("BadRequest", "an image sequence lives in one folder")
    return files


def _free_channel():
    editor = _editor()
    return max((strip.channel for strip in editor.strips_all), default=0) + 1


@command("sequencer_add_effect")
def sequencer_add_effect(params):
    kind = _require(params, "type").upper()
    if kind not in EFFECT_TYPES:
        raise CommandError("BadRequest", f"unknown effect '{kind}'", {"known": list(EFFECT_TYPES)})
    inputs = [_strip(name) for name in params.get("inputs") or []]
    if kind in TWO_INPUT_EFFECTS and len(inputs) != 2:
        raise CommandError("BadRequest", f"{kind} needs exactly two input strips")
    channel = int(params.get("channel") or _free_channel())
    arguments = {"name": params.get("name") or kind.title(), "type": kind, "channel": channel}
    if inputs:
        overlap_start = max(strip.frame_final_start for strip in inputs)
        overlap_end = min(strip.frame_final_end for strip in inputs)
        arguments["frame_start"] = int(params.get("frame_start") if params.get("frame_start") is not None else overlap_start)
        arguments["length"] = int(params.get("length") or max(1, overlap_end - arguments["frame_start"]))
        arguments["input1"] = inputs[0]
        if len(inputs) > 1:
            arguments["input2"] = inputs[1]
    else:
        arguments["frame_start"] = int(params.get("frame_start") if params.get("frame_start") is not None else bpy.context.scene.frame_start)
        arguments["length"] = int(params.get("length") or 48)
    strip = _editor().strips.new_effect(**arguments)
    _apply_strip_settings(strip, {key: value for key, value in params.items() if key not in ("channel", "frame_start", "length")})
    return _strip_summary(strip)


@command("sequencer_update_strip")
def sequencer_update_strip(params):
    strip = _strip(_require(params, "name"))
    _apply_strip_settings(strip, params)
    if params.get("new_name"):
        strip.name = params["new_name"]
    return _strip_summary(strip)


@command("sequencer_remove_strip")
def sequencer_remove_strip(params):
    editor = _editor()
    if params.get("all"):
        removed = [strip.name for strip in editor.strips_all]
        while editor.strips_all:
            strip = next((strip for strip in editor.strips_all if getattr(strip, "input_1", None) is not None), editor.strips_all[0])
            editor.strips.remove(strip)
        return {"removed": removed, "strips": []}
    strip = _strip(_require(params, "name"))
    removed = strip.name
    editor.strips.remove(strip)
    return {"removed": [removed], "strips": [strip.name for strip in editor.strips_all]}


@command("sequencer_render")
def sequencer_render(params):
    scene = bpy.context.scene
    editor = _editor()
    if not editor.strips_all:
        raise CommandError("BadRequest", "the sequencer has no strips; add some with sequencer_add_strip")
    path = _require(params, "path")
    stem, extension = os.path.splitext(path)
    container = CONTAINERS.get(extension.lstrip(".").lower())
    if container is None:
        raise CommandError("BadRequest", f"unsupported container '{extension}'", {"known": sorted(CONTAINERS)})
    directory = os.path.dirname(path)
    if directory:
        os.makedirs(directory, exist_ok=True)
    render = scene.render
    if params.get("fit_range", True):
        scene.frame_start = min(strip.frame_final_start for strip in editor.strips_all)
        scene.frame_end = max(strip.frame_final_end for strip in editor.strips_all) - 1
    if params.get("start") is not None:
        scene.frame_start = int(params["start"])
    if params.get("end") is not None:
        scene.frame_end = int(params["end"])
    if params.get("fps") is not None:
        render.fps = int(params["fps"])
    if params.get("resolution_x") is not None:
        render.resolution_x = int(params["resolution_x"])
    if params.get("resolution_y") is not None:
        render.resolution_y = int(params["resolution_y"])
    render.use_sequencer = True
    _set_output_format(render, "FFMPEG")
    render.ffmpeg.format = container
    if params.get("codec") or (container == "WEBM" and render.ffmpeg.codec not in ("WEBM", "AV1")):
        _set_enum(render.ffmpeg, "codec", (params.get("codec") or "WEBM").upper(), "codec")
    if render.ffmpeg.codec == "NONE":
        render.ffmpeg.codec = "H264"
    if params.get("quality"):
        _set_enum(render.ffmpeg, "constant_rate_factor", params["quality"].upper(), "quality")
    if params.get("audio_codec") or (container == "WEBM" and render.ffmpeg.audio_codec not in ("VORBIS", "OPUS", "NONE")):
        _set_enum(render.ffmpeg, "audio_codec", (params.get("audio_codec") or "VORBIS").upper(), "audio codec")
    render.filepath = path
    previous = {entry for entry in os.listdir(directory or ".")}
    with _window_override():
        bpy.ops.render.render(animation=True)
    written = path if os.path.isfile(path) else next((os.path.join(directory or ".", entry) for entry in os.listdir(directory or ".") if entry not in previous and entry.startswith(os.path.basename(stem))), None)
    if written is None:
        raise CommandError("RenderFailed", f"Blender encoded but no file appeared next to '{path}'")
    return {"path": written, "bytes": os.path.getsize(written), "frames": [scene.frame_start, scene.frame_end], "fps": render.fps,
            "container": render.ffmpeg.format, "codec": render.ffmpeg.codec, "resolution": [render.resolution_x, render.resolution_y], **_encode_summary(render)}


def _clone_source(strip, name, channel):
    strips = _editor().strips
    start = int(strip.frame_start)
    if strip.type == "MOVIE":
        return strips.new_movie(name=name, filepath=strip.filepath, channel=channel, frame_start=start)
    if strip.type == "SOUND":
        return strips.new_sound(name=name, filepath=strip.sound.filepath, channel=channel, frame_start=start)
    if strip.type == "IMAGE":
        clone = strips.new_image(name=name, filepath=os.path.join(strip.directory, strip.elements[0].filename), channel=channel, frame_start=start)
        for element in strip.elements[1:]:
            clone.elements.append(element.filename)
        if len(strip.elements) == 1:
            clone.frame_final_duration = strip.frame_final_duration
        return clone
    if strip.type == "SCENE":
        return strips.new_scene(name=name, scene=strip.scene, channel=channel, frame_start=start)
    if strip.type in ("COLOR", "TEXT"):
        clone = strips.new_effect(name=name, type=strip.type, channel=channel, frame_start=start, length=int(strip.frame_final_duration))
        for attribute in ("color", "text", "font_size", "location", "wrap_width", "use_shadow", "use_box"):
            if hasattr(strip, attribute):
                setattr(clone, attribute, getattr(strip, attribute))
        return clone
    raise CommandError("Unsupported", f"a {strip.type} strip cannot be split; split its inputs instead")


def _copy_look(source, target):
    for attribute in ("blend_type", "blend_alpha", "mute", "volume", "use_flip_x", "use_flip_y", "color_saturation", "color_multiply"):
        if hasattr(source, attribute) and hasattr(target, attribute):
            setattr(target, attribute, getattr(source, attribute))
    if hasattr(source, "transform"):
        for attribute in ("offset_x", "offset_y", "scale_x", "scale_y", "rotation"):
            setattr(target.transform, attribute, getattr(source.transform, attribute))
    if hasattr(source, "crop"):
        for attribute in ("min_x", "max_x", "min_y", "max_y"):
            setattr(target.crop, attribute, getattr(source.crop, attribute))


@command("sequencer_split")
def sequencer_split(params):
    strip = _strip(_require(params, "name"))
    frame = int(_require(params, "frame", int))
    if not strip.frame_final_start < frame < strip.frame_final_end:
        raise CommandError("BadRequest", f"frame {frame} is not inside '{strip.name}' ({strip.frame_final_start}..{strip.frame_final_end})")
    keep = (params.get("keep") or "both").lower()
    if keep not in ("both", "left", "right"):
        raise CommandError("BadRequest", "keep must be both, left or right")
    end = strip.frame_final_end
    right = _clone_source(strip, params.get("right_name") or f"{strip.name}.R", _free_channel())
    _copy_look(strip, right)
    right.frame_final_end = end
    right.frame_final_start = frame
    strip.frame_final_end = frame
    right.channel = strip.channel
    if right.frame_final_start != frame or strip.frame_final_end != frame:
        raise CommandError("RenderFailed", f"Blender did not accept the cut at frame {frame}", {"left": [strip.frame_final_start, strip.frame_final_end], "right": [right.frame_final_start, right.frame_final_end]})
    editor = _editor()
    if keep == "left":
        editor.strips.remove(right)
        return {"left": _strip_summary(strip), "right": None}
    if keep == "right":
        left_name = strip.name
        editor.strips.remove(strip)
        right.name = left_name
        return {"left": None, "right": _strip_summary(right)}
    return {"left": _strip_summary(strip), "right": _strip_summary(right)}


def _sequencer_override():
    for window in bpy.context.window_manager.windows:
        for area in window.screen.areas:
            if area.type == "SEQUENCE_EDITOR":
                region = next((region for region in area.regions if region.type == "WINDOW"), None)
                return bpy.context.temp_override(window=window, screen=window.screen, area=area, region=region)
    return None


@command("sequencer_proxy")
def sequencer_proxy(params):
    editor = _editor()
    strips = list(editor.strips_all) if params.get("all") else [_strip(name) for name in params.get("names") or [_require(params, "name")]]
    sizes = [int(size) for size in params.get("sizes") or [25]]
    unknown = [size for size in sizes if size not in (25, 50, 75, 100)]
    if unknown:
        raise CommandError("BadRequest", f"proxy sizes are 25, 50, 75 or 100 percent, not {unknown}")
    if params.get("directory"):
        editor.proxy_storage = "PROJECT"
        editor.proxy_dir = params["directory"]
    configured = []
    for strip in strips:
        if not hasattr(strip, "use_proxy") or strip.type not in ("MOVIE", "IMAGE", "META", "SCENE", "MOVIECLIP"):
            continue
        strip.use_proxy = params.get("enabled", True)
        if strip.proxy is None:
            continue
        for size in (25, 50, 75, 100):
            setattr(strip.proxy, f"build_{size}", size in sizes)
        if params.get("quality") is not None:
            strip.proxy.quality = max(1, min(100, int(params["quality"])))
        strip.proxy.use_overwrite = bool(params.get("overwrite", False))
        configured.append(strip.name)
    built = False
    if params.get("build", True) and configured:
        override = _sequencer_override()
        if override is None:
            raise CommandError("NoArea", "proxy settings are stored, but building them needs the Video Sequencer editor open in Blender (Strip → Rebuild Proxy and Timecode Indices)", {"configured": configured})
        with override:
            for strip in editor.strips_all:
                strip.select = strip.name in configured
            bpy.ops.sequencer.rebuild_proxy()
        built = True
    return {"configured": configured, "sizes": sizes, "built": built, "storage": editor.proxy_storage, "directory": editor.proxy_dir}


@command("sequencer_meta")
def sequencer_meta(params):
    """Meta strips through Blender's own operators, which need the sequencer editor on screen: make groups the named strips, separate unpacks one."""
    editor = _editor()
    action = (params.get("action") or "make").lower()
    override = _sequencer_override()
    if override is None:
        raise CommandError("NoArea", "meta strips need the Video Sequencer editor open in Blender; open one and call again")
    names = list(params.get("names") or ([params["name"]] if params.get("name") else []))
    if not names:
        raise CommandError("BadRequest", "pass names (make) or name (separate)")
    strips = [_strip(name) for name in names]
    with override:
        for strip in editor.strips_all:
            strip.select = strip in strips
        editor.active_strip = strips[0]
        if action == "make":
            before = {strip.name for strip in editor.strips_all}
            bpy.ops.sequencer.meta_make()
            meta = next((strip for strip in editor.strips_all if strip.name not in before and strip.type == "META"), None)
            if meta is None:
                raise CommandError("RenderFailed", "Blender made no meta strip")
            if params.get("new_name"):
                meta.name = params["new_name"]
            return _strip_summary(meta)
        if action == "separate":
            if strips[0].type != "META":
                raise CommandError("BadRequest", f"'{strips[0].name}' is not a meta strip")
            bpy.ops.sequencer.meta_separate()
            return {"separated": names[0], "strips": [strip.name for strip in editor.strips_all]}
    raise CommandError("BadRequest", "action must be make or separate")


def _rate(value):
    value = float(value)
    for fractional, pair in FRACTIONAL_RATES.items():
        if abs(value - fractional) < 0.005:
            return pair
    if abs(value - round(value)) < 0.005:
        return round(value), 1.0
    return round(value), round(value) / value


@command("sequencer_timing")
def sequencer_timing(params):
    scene = bpy.context.scene
    render = scene.render
    if params.get("fps") is not None:
        render.fps, render.fps_base = _rate(params["fps"])
    if params.get("fps_base") is not None:
        render.fps_base = float(params["fps_base"])
    if params.get("sync_mode"):
        _set_enum(scene, "sync_mode", params["sync_mode"].upper(), "sync mode")
    if params.get("pixel_aspect") is not None:
        render.pixel_aspect_x, render.pixel_aspect_y = (float(value) for value in params["pixel_aspect"])
    for axis in ("x", "y"):
        if params.get(f"resolution_{axis}") is not None:
            setattr(render, f"resolution_{axis}", int(params[f"resolution_{axis}"]))
    if params.get("percentage") is not None:
        render.resolution_percentage = max(1, min(100, int(params["percentage"])))
    if params.get("start") is not None:
        scene.frame_start = int(params["start"])
    if params.get("end") is not None:
        scene.frame_end = int(params["end"])
    if params.get("audio") is not None:
        scene.use_audio = bool(params["audio"])
    return {
        "fps": render.fps, "fps_base": render.fps_base, "effective_fps": round(render.fps / render.fps_base, 3),
        "sync_mode": scene.sync_mode, "pixel_aspect": [render.pixel_aspect_x, render.pixel_aspect_y],
        "resolution": [render.resolution_x, render.resolution_y], "percentage": render.resolution_percentage,
        "frames": [scene.frame_start, scene.frame_end], "audio": scene.use_audio,
    }


CURVE_CHANNELS = {"r": 0, "g": 1, "b": 2, "c": 3, "red": 0, "green": 1, "blue": 2, "combined": 3, "h": 0, "s": 1, "v": 2, "hue": 0, "saturation": 1, "value": 2}


def _apply_modifier(modifier, params):
    balance = params.get("color_balance")
    if isinstance(balance, dict) and hasattr(modifier, "color_balance"):
        target = modifier.color_balance
        if balance.get("method"):
            _set_enum(target, "correction_method", balance["method"].upper(), "correction method")
        for key in ("lift", "gamma", "gain", "slope", "offset", "power"):
            if balance.get(key) is not None:
                setattr(target, key, tuple(balance[key]))
            if balance.get(f"invert_{key}") is not None:
                setattr(target, f"invert_{key}", bool(balance[f"invert_{key}"]))
    curves = params.get("curves")
    if isinstance(curves, dict) and hasattr(modifier, "curve_mapping"):
        for channel, points in curves.items():
            index = CURVE_CHANNELS.get(str(channel).lower())
            if index is None or index >= len(modifier.curve_mapping.curves):
                raise CommandError("BadRequest", f"unknown curve channel '{channel}'", {"known": ["R", "G", "B", "C"] if len(modifier.curve_mapping.curves) > 3 else ["H", "S", "V"]})
            _apply_curve_points(modifier.curve_mapping.curves[index], points)
        modifier.curve_mapping.update()
    if params.get("mask_strip"):
        modifier.input_mask_type = "STRIP"
        modifier.input_mask_strip = _strip(params["mask_strip"])
    if params.get("mute") is not None:
        modifier.mute = bool(params["mute"])
    if params.get("settings"):
        _apply_settings(modifier, params["settings"])


def _strip_modifier_summary(modifier):
    summary = {"name": modifier.name, "type": modifier.type, "mute": modifier.mute}
    if hasattr(modifier, "color_balance"):
        target = modifier.color_balance
        summary["color_balance"] = {"method": target.correction_method, **{key: to_json(getattr(target, key)) for key in ("lift", "gamma", "gain", "slope", "offset", "power")}}
    for key in ("bright", "contrast", "white_value", "tonemap_type", "key", "offset", "gamma", "intensity", "contrast", "adaptation", "correction"):
        if hasattr(modifier, key) and key not in summary:
            summary[key] = to_json(getattr(modifier, key))
    if hasattr(modifier, "curve_mapping"):
        summary["curves"] = [[list(point.location) for point in curve.points] for curve in modifier.curve_mapping.curves]
    if modifier.input_mask_strip is not None:
        summary["mask_strip"] = modifier.input_mask_strip.name
    return summary


@command("sequencer_grade")
def sequencer_grade(params):
    strip = _strip(_require(params, "name"))
    if not hasattr(strip, "modifiers"):
        raise CommandError("BadRequest", f"'{strip.name}' takes no modifiers")
    if params.get("remove"):
        modifier = strip.modifiers.get(params["remove"])
        if modifier is None:
            raise CommandError("NotFound", f"'{strip.name}' has no modifier '{params['remove']}'", {"known": [modifier.name for modifier in strip.modifiers]})
        strip.modifiers.remove(modifier)
    elif params.get("clear"):
        strip.modifiers.clear()
    elif params.get("type") or params.get("modifier"):
        modifier = strip.modifiers.get(params.get("modifier") or "")
        if modifier is None:
            kind = (params.get("type") or "").upper()
            if kind not in STRIP_MODIFIERS:
                raise CommandError("BadRequest", f"unknown strip modifier '{kind}'", {"known": list(STRIP_MODIFIERS)})
            modifier = strip.modifiers.new(params.get("modifier") or kind.replace("_", " ").title(), kind)
        _apply_modifier(modifier, params)
    return {"strip": strip.name, "modifiers": [_strip_modifier_summary(modifier) for modifier in strip.modifiers]}


def _encode_summary(render):
    ffmpeg = render.ffmpeg
    summary = {
        "container": ffmpeg.format, "codec": ffmpeg.codec, "quality": ffmpeg.constant_rate_factor, "preset": ffmpeg.ffmpeg_preset,
        "bitrate_kbps": ffmpeg.video_bitrate, "max_bitrate_kbps": ffmpeg.maxrate, "min_bitrate_kbps": ffmpeg.minrate, "buffer_kb": ffmpeg.buffersize,
        "gop": ffmpeg.gopsize, "max_b_frames": ffmpeg.max_b_frames if ffmpeg.use_max_b_frames else 0, "lossless": ffmpeg.use_lossless_output,
        "autosplit": ffmpeg.use_autosplit, "color_depth": render.image_settings.color_depth,
        "audio": {"codec": ffmpeg.audio_codec, "bitrate_kbps": ffmpeg.audio_bitrate, "sample_rate": ffmpeg.audio_mixrate, "channels": ffmpeg.audio_channels, "volume": ffmpeg.audio_volume},
    }
    if ffmpeg.constant_rate_factor == "CUSTOM":
        summary["crf"] = ffmpeg.custom_constant_rate_factor
    if hasattr(ffmpeg, "ffmpeg_prores_profile"):
        summary["prores_profile"] = ffmpeg.ffmpeg_prores_profile
    return summary


@command("sequencer_encode")
def sequencer_encode(params):
    render = bpy.context.scene.render
    ffmpeg = render.ffmpeg
    _set_output_format(render, "FFMPEG")
    if params.get("container"):
        container = CONTAINERS.get(params["container"].lower(), params["container"].upper())
        _set_enum(ffmpeg, "format", container, "container")
    if params.get("codec"):
        _set_enum(ffmpeg, "codec", params["codec"].upper(), "codec")
    if params.get("prores_profile"):
        if not hasattr(ffmpeg, "ffmpeg_prores_profile"):
            raise CommandError("Unsupported", "this Blender has no ProRes profile setting")
        profile = str(params["prores_profile"]).upper()
        if profile not in PRORES_PROFILES:
            raise CommandError("BadRequest", f"unknown ProRes profile '{profile}'", {"known": list(PRORES_PROFILES)})
        ffmpeg.codec = "PRORES"
        ffmpeg.ffmpeg_prores_profile = profile
    if params.get("quality"):
        _set_enum(ffmpeg, "constant_rate_factor", params["quality"].upper(), "quality")
    if params.get("crf") is not None:
        ffmpeg.constant_rate_factor = "CUSTOM"
        ffmpeg.custom_constant_rate_factor = max(0, min(51, int(params["crf"])))
    if params.get("bitrate") is not None:
        ffmpeg.constant_rate_factor = "NONE"
        ffmpeg.video_bitrate = int(params["bitrate"])
    if params.get("max_bitrate") is not None:
        ffmpeg.maxrate = int(params["max_bitrate"])
    if params.get("min_bitrate") is not None:
        ffmpeg.minrate = int(params["min_bitrate"])
    if params.get("buffer_size") is not None:
        ffmpeg.buffersize = int(params["buffer_size"])
    if params.get("gop") is not None:
        ffmpeg.gopsize = int(params["gop"])
    if params.get("max_b_frames") is not None:
        ffmpeg.use_max_b_frames = int(params["max_b_frames"]) > 0
        ffmpeg.max_b_frames = int(params["max_b_frames"])
    if params.get("preset"):
        _set_enum(ffmpeg, "ffmpeg_preset", params["preset"].upper(), "encoding preset")
    if params.get("lossless") is not None:
        ffmpeg.use_lossless_output = bool(params["lossless"])
    if params.get("autosplit") is not None:
        ffmpeg.use_autosplit = bool(params["autosplit"])
    if params.get("color_depth") is not None:
        _set_enum(render.image_settings, "color_depth", str(params["color_depth"]), "colour depth")
    audio = params.get("audio")
    if isinstance(audio, dict):
        if audio.get("codec"):
            _set_enum(ffmpeg, "audio_codec", audio["codec"].upper(), "audio codec")
        if audio.get("bitrate") is not None:
            ffmpeg.audio_bitrate = int(audio["bitrate"])
        if audio.get("sample_rate") is not None:
            ffmpeg.audio_mixrate = int(audio["sample_rate"])
        if audio.get("channels"):
            _set_enum(ffmpeg, "audio_channels", audio["channels"].upper(), "audio channels")
        if audio.get("volume") is not None:
            ffmpeg.audio_volume = float(audio["volume"])
    summary = _encode_summary(render)
    summary["known"] = {"codecs": _known_enum(ffmpeg, "codec"), "containers": sorted(CONTAINERS), "audio_codecs": _known_enum(ffmpeg, "audio_codec"), "prores_profiles": list(PRORES_PROFILES)}
    return summary


@command("sequencer_mixdown")
def sequencer_mixdown(params):
    scene = bpy.context.scene
    editor = _editor()
    if not any(strip.type in ("SOUND", "SCENE", "META") for strip in editor.strips_all):
        raise CommandError("BadRequest", "the sequencer has no sound strips to mix down")
    path = _require(params, "path")
    extension = os.path.splitext(path)[1].lstrip(".").lower()
    container = AUDIO_CONTAINERS.get(extension)
    if container is None:
        raise CommandError("BadRequest", f"unsupported audio container '{extension}'", {"known": sorted(AUDIO_CONTAINERS)})
    directory = os.path.dirname(path)
    if directory:
        os.makedirs(directory, exist_ok=True)
    arguments = {"filepath": path, "container": container, "codec": (params.get("codec") or AUDIO_CODECS[container]).upper(),
                 "format": (params.get("format") or ("F32" if container == "WAV" else "S16")).upper(), "bitrate": int(params.get("bitrate") or 192),
                 "accuracy": int(params.get("accuracy") or 1024), "split_channels": bool(params.get("split_channels", False))}
    if params.get("channels"):
        arguments["channels"] = params["channels"].upper()
    if bpy.app.background:
        _mixdown_with_aud(scene, editor, arguments)
    else:
        with bpy.context.temp_override(scene=scene):
            bpy.ops.sound.mixdown(**arguments)
    if not os.path.isfile(path):
        raise CommandError("RenderFailed", f"the mixdown ran but no file appeared at '{path}'")
    return {"path": path, "bytes": os.path.getsize(path), "container": container, "codec": arguments["codec"], "format": arguments["format"],
            "frames": [scene.frame_start, scene.frame_end], "sample_rate": scene.render.ffmpeg.audio_mixrate, "mixer": "aud" if bpy.app.background else "sequencer"}


AUD_FORMATS = {"U8": "FORMAT_U8", "S16": "FORMAT_S16", "S24": "FORMAT_S24", "S32": "FORMAT_S32", "F32": "FORMAT_FLOAT32", "F64": "FORMAT_FLOAT64"}


def _mixdown_with_aud(scene, editor, arguments):
    """Background Blender runs no audio scene, so the mixdown operator writes nothing; the audaspace library mixes the sound strips instead."""
    import aud
    fps = scene.render.fps / scene.render.fps_base
    origin = scene.frame_start
    mix = None
    for strip in editor.strips_all:
        if strip.type != "SOUND" or strip.mute:
            continue
        sound = aud.Sound.file(bpy.path.abspath(strip.sound.filepath))
        start = strip.frame_offset_start / fps
        sound = sound.limit(start, start + strip.frame_final_duration / fps).volume(strip.volume)
        offset = (strip.frame_final_start - origin) / fps
        if offset > 0:
            sound = sound.delay(offset)
        mix = sound if mix is None else mix.mix(sound)
    if mix is None:
        raise CommandError("BadRequest", "no unmuted sound strip to mix down")
    mix = mix.limit(0, (scene.frame_end - origin + 1) / fps)
    channels = getattr(aud, f"CHANNELS_{arguments.get('channels', 'STEREO')}")
    rate = scene.render.ffmpeg.audio_mixrate
    mix.write(arguments["filepath"], rate, channels, getattr(aud, AUD_FORMATS[arguments["format"]]), getattr(aud, f"CONTAINER_{arguments['container']}"),
              getattr(aud, f"CODEC_{arguments['codec']}"), arguments["bitrate"] * 1000, arguments["accuracy"])
