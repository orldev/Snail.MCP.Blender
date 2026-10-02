"""The render job worker: a headless Blender runs this on a saved copy of the file and writes a status file after every frame."""

import json
import os
import re
import sys
import time

import bpy

with open(sys.argv[sys.argv.index("--") + 1]) as handle:
    spec = json.load(handle)
status_path = spec["status_path"]
cancel_path = os.path.join(os.path.dirname(status_path), "cancel")
state = {"state": "running", "frames_done": 0, "frames_total": spec["frames_total"], "frame": None, "camera": None, "started_at": time.time(),
         "elapsed_s": 0.0, "files": [], "files_written": 0, "last_stats": "", "peak_memory_mb": 0.0, "error": None, "pid": os.getpid()}
written_path = os.path.join(os.path.dirname(status_path), "files.txt")
KEPT_FILE_PATHS = 20


def cancelled():
    """The marker blender_render_job_cancel leaves: read between frames, so the job stops where it can be resumed from rather than where a signal found it."""
    return os.path.exists(cancel_path)


def write():
    if cancelled():
        state["state"] = "cancelled"
    state["elapsed_s"] = round(time.time() - state["started_at"], 2)
    pending = f"{status_path}.{os.getpid()}.tmp"
    with open(pending, "w") as handle:
        json.dump(state, handle)
    os.replace(pending, status_path)


def on_pre(scene):
    state["frame"] = scene.frame_current
    write()


def on_write(scene):
    """Every frame is named once in files.txt and the status keeps the last few: the status is rewritten after every frame, so carrying every
    path in it made a ten thousand frame job rewrite a megabyte ten thousand times."""
    path = scene.render.frame_path(frame=scene.frame_current)
    state["frames_done"] += 1
    state["files_written"] += 1
    state["files"] = [*state["files"], path][-KEPT_FILE_PATHS:]
    with open(written_path, "a") as handle:
        handle.write(path + "\n")
    write()


def on_stats(*arguments):
    text = next((argument for argument in arguments if isinstance(argument, str)), "")
    state["last_stats"] = text
    for value, unit in re.findall(r"(?:Mem|Peak)[:\s]*([\d.]+)\s*([KMG])", text):
        state["peak_memory_mb"] = max(state["peak_memory_mb"], round(float(value) * {"K": 1 / 1024, "M": 1.0, "G": 1024.0}[unit], 1))


def use_gpu(gpu):
    """Puts the backend and devices of the Blender that queued the job into this one, which read only its own saved preferences."""
    addon = bpy.context.preferences.addons.get("cycles")
    if not gpu or gpu.get("backend") in (None, "NONE") or addon is None:
        return
    preferences = addon.preferences
    preferences.compute_device_type = gpu["backend"]
    preferences.get_devices()
    wanted = set(gpu.get("devices") or [])
    for device in preferences.devices:
        device.use = device.id in wanted if wanted else device.type == gpu["backend"]
    state["gpu"] = {"backend": gpu["backend"], "devices": [device.name for device in preferences.devices if device.use]}


bpy.app.handlers.render_pre.append(on_pre)
bpy.app.handlers.render_write.append(on_write)
bpy.app.handlers.render_stats.append(on_stats)
try:
    scene = bpy.data.scenes[spec["scene"]]
    render = scene.render
    for name, value in spec["render"].items():
        setattr(render, name, value)
    image = render.image_settings
    if spec.get("file_format"):
        if hasattr(image, "media_type"):
            image.media_type = "VIDEO" if spec["file_format"] == "FFMPEG" else ("MULTI_LAYER_IMAGE" if spec["file_format"] == "OPEN_EXR_MULTILAYER" else "IMAGE")
        image.file_format = spec["file_format"]
    for name, value in spec["image"].items():
        setattr(image, name, value)
    if spec.get("samples") is not None:
        scene.cycles.samples = spec["samples"]
        scene.eevee.taa_render_samples = spec["samples"]
    use_gpu(spec.get("gpu"))
    if spec.get("view_layers"):
        for layer in scene.view_layers:
            layer.use = layer.name in spec["view_layers"]
    template = spec["output"]
    for camera in spec["cameras"] or [None]:
        if cancelled():
            break
        if camera:
            scene.camera = bpy.data.objects[camera]
        state["camera"] = scene.camera.name if scene.camera else None
        render.filepath = template.replace("{camera}", state["camera"] or "camera").replace("{scene}", scene.name)
        write()
        if spec["frame_list"]:
            for frame in spec["frame_list"]:
                if cancelled():
                    break
                scene.frame_set(frame)
                render.filepath = scene.render.frame_path(frame=frame)
                bpy.ops.render.render(write_still=True, scene=scene.name)
                render.filepath = template.replace("{camera}", state["camera"] or "camera").replace("{scene}", scene.name)
        else:
            scene.frame_start, scene.frame_end, scene.frame_step = spec["frame_range"]
            bpy.ops.render.render(animation=True, scene=scene.name)
    state["state"] = "cancelled" if cancelled() else "finished"
except KeyboardInterrupt:
    state["state"] = "cancelled"
    write()
    raise SystemExit(0) from None
except Exception as error:
    state["state"] = "failed"
    state["error"] = f"{type(error).__name__}: {error}"
    write()
    raise
write()
