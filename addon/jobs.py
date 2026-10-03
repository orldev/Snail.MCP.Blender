"""Background render jobs: a second Blender renders a saved copy of the file so the interface and the bridge stay free; status, log and cancel. Plus the pre-flight check."""

import contextlib
import functools
import json
import os
import re
import shutil
import signal
import subprocess
import threading
import time
import traceback

import bpy

from . import state
from .core import _require, _scene, command
from .rendering import ENGINES, MAX_RESOLUTION, _refuse_a_finished_name
from .server import CommandError

JOBS = {}
ROOTS_FILE = "jobs.json"
CANCEL_FILE = "cancel"
QUEUE_INTERVAL_SECONDS = 3.0
STATES = ("queued", "running", "finished", "failed", "cancelled", "interrupted")
TERMINAL = ("finished", "failed", "cancelled", "interrupted")
ACTIVE = ("queued", "running")
ENDED = ("finished", "failed", "cancelled")
KEPT_FILE_PATHS = 20


def is_active(phase):
    """Queued or running, including "running (started by another Blender session)", which is a worker writing into the folder."""
    return phase in ACTIVE or str(phase).startswith("running")
SCHEDULED = set()


def _parse_frames(text, scene):
    """Frame spec: "1-240", "1-240x2", "5", "1,5,9-12"; the scene range when empty."""
    if text is None or str(text).strip() == "":
        return (scene.frame_start, scene.frame_end, scene.frame_step), []
    text = str(text).replace(" ", "")
    match = re.fullmatch(r"(-?\d+)-(-?\d+)(?:x(\d+))?", text)
    if match:
        start, end, step = int(match.group(1)), int(match.group(2)), int(match.group(3) or 1)
        if end < start:
            raise CommandError("BadRequest", f"frame range '{text}' ends before it starts")
        return (start, end, max(1, step)), []
    frames = []
    for part in text.split(","):
        piece = re.fullmatch(r"(-?\d+)(?:-(-?\d+))?", part)
        if piece is None:
            raise CommandError("BadRequest", f"cannot read frames '{text}'; use 1-240, 1-240x2 or 1,5,9-12")
        first, last = int(piece.group(1)), int(piece.group(2) or piece.group(1))
        frames.extend(range(first, last + 1))
    return None, sorted(set(frames))


def _job_directory(params):
    root = params.get("directory") or os.path.join(state.data_directory(), "jobs")
    os.makedirs(root, exist_ok=True)
    _remember_root(root)
    return root


def _remember_root(root):
    if root in state.load(ROOTS_FILE, {}).get("roots", []):
        return
    with contextlib.suppress(CommandError):
        with state.locked(ROOTS_FILE):
            known = state.load(ROOTS_FILE, {}).get("roots", [])
            if root not in known:
                state.save(ROOTS_FILE, {"roots": sorted([*known, root])})


def resume():
    """Picks the queue back up after a restart: every root this Blender ever queued into is advanced again, so queued and crashed jobs continue."""
    advance()


def advance():
    """Advances every queue this Blender knows. The interface does it on a timer; a background Blender runs no timers, so the headless loop calls this instead."""
    for root in state.load(ROOTS_FILE, {}).get("roots", []):
        if os.path.isdir(root):
            _advance(root)


def _validate_output(template):
    if not template:
        raise CommandError("BadRequest", "output_path is required: a path template such as /renders/{scene}_{camera}_####")
    _refuse_a_finished_name(template)
    directory = os.path.dirname(template.replace("{scene}", "scene").replace("{camera}", "camera"))
    if directory:
        os.makedirs(directory, exist_ok=True)


@command("render_job")
def render_job(params):
    scene = _scene(params)
    output = _require(params, "output_path")
    _validate_output(output)
    cameras = list(params.get("cameras") or [])
    for name in cameras:
        camera = bpy.data.objects.get(name)
        if camera is None or camera.type != "CAMERA":
            raise CommandError("NotFound", f"'{name}' is not a camera in this file")
    if not cameras and scene.camera is None:
        raise CommandError("BadRequest", "the scene has no camera; pass cameras or set one with blender_render_settings")
    for name in params.get("view_layers") or []:
        if name not in scene.view_layers:
            raise CommandError("NotFound", f"no view layer named '{name}'", {"known": [layer.name for layer in scene.view_layers]})
    frame_range, frame_list = _parse_frames(params.get("frames"), scene)
    frames_total = len(frame_list) if frame_list else len(range(frame_range[0], frame_range[1] + 1, frame_range[2]))
    root = _job_directory(params)
    job_id = time.strftime("job-%Y%m%d-%H%M%S") + f"-{int(time.time() * 1000) % 1000:03d}"
    directory = os.path.join(root, job_id)
    os.makedirs(directory, exist_ok=True)
    blend = os.path.join(directory, "scene.blend")
    bpy.ops.wm.save_as_mainfile(filepath=blend, copy=True)
    chunks = max(1, int(params.get("chunks") or 1))
    if chunks > 1:
        return _split_into_chunks(params, root, job_id, directory, blend, frame_range, frame_list, chunks)
    spec = _spec(scene, params, cameras, frame_range, frame_list, frames_total, directory)
    spec.update({"blend": blend, "threads": int(params.get("threads") or 0), "max_parallel": max(1, int(params.get("max_parallel") or 1)),
                 "retries": max(0, int(params.get("retries") or 0)), "attempts": 0, "priority": int(params.get("priority") or 0), "ocio": _ocio(params)})
    _write_spec(directory, spec)
    _write_status(directory, {"state": "queued", "frames_done": 0, "frames_total": spec["frames_total"], "started_at": time.time(), "elapsed_s": 0.0, "files": []})
    _advance(root)
    return _job_status(job_id, directory, int(params.get("log_lines") or 5))


def _ocio(params):
    path = params.get("ocio_config")
    if not path:
        return None
    if not os.path.isfile(path):
        raise CommandError("NotFound", f"no OCIO config at '{path}'")
    return path


def _chunk_frames(frame_range, frame_list, index, chunks):
    if frame_list:
        share = frame_list[index::chunks]
        return ",".join(str(frame) for frame in share) if share else None
    start, end, step = frame_range
    first = start + index * step
    return f"{first}-{end}x{step * chunks}" if first <= end else None


def _split_into_chunks(params, root, parent_id, directory, blend, frame_range, frame_list, chunks):
    """Several processes over one frame range: chunk n renders every nth frame (or every nth of a list), so every chunk carries an equal share and the sequence fills in evenly."""
    children = []
    for index in range(chunks):
        frames = _chunk_frames(frame_range, frame_list, index, chunks)
        if frames is None:
            continue
        child_id = f"{parent_id}-c{index + 1}"
        child_directory = os.path.join(root, child_id)
        os.makedirs(child_directory, exist_ok=True)
        child = dict(params)
        child.update({"frames": frames, "chunks": 1, "directory": root})
        _prepare_child(child, child_id, child_directory, blend, params)
        children.append(child_id)
    _write_json(os.path.join(directory, "chunks.json"), {"children": children})
    _advance(root)
    return _job_status(parent_id, directory, 0)


def _prepare_child(params, job_id, directory, blend, parent_params):
    scene = _scene(parent_params)
    frame_range, frame_list = _parse_frames(params["frames"], scene)
    frames_total = len(frame_list) if frame_list else len(range(frame_range[0], frame_range[1] + 1, frame_range[2]))
    cameras = list(params.get("cameras") or [])
    spec = _spec(scene, params, cameras, frame_range, frame_list, frames_total, directory)
    spec.update({"blend": blend, "threads": int(params.get("threads") or 0), "max_parallel": max(1, int(params.get("max_parallel") or 1)),
                 "retries": max(0, int(params.get("retries") or 0)), "attempts": 0, "priority": int(params.get("priority") or 0), "ocio": _ocio(params)})
    _write_spec(directory, spec)
    _write_status(directory, {"state": "queued", "frames_done": 0, "frames_total": frames_total, "started_at": time.time(), "elapsed_s": 0.0, "files": []})


def _write_spec(directory, spec):
    _write_json(os.path.join(directory, "spec.json"), spec, indent=2)


def _write_status(directory, status):
    _write_json(os.path.join(directory, "status.json"), status)


def _write_json(path, data, indent=None):
    """Written beside and moved into place: a crash between the two leaves the file as it was, not half of it, and a job whose spec reads
    as half a file is a job nothing can start or retry.

    The name written beside it carries this process's pid. A job's status is written by this Blender and by the worker it started, and both
    used to stage it through the same name: one truncated the file while the other was filling it, and whoever renamed first published the
    result. A status that reads as half a file comes back as the state 'unknown', which is neither running nor ended — so the job is never
    retried, never cleaned up and never deletable.
    """
    pending = f"{path}.{os.getpid()}.tmp"
    with open(pending, "w") as handle:
        json.dump(data, handle, indent=indent)
    os.replace(pending, path)


def _read_spec(directory):
    try:
        with open(os.path.join(directory, "spec.json")) as handle:
            return json.load(handle)
    except (OSError, ValueError):
        return None


def _launched(job_id, directory, spec):
    """Starts a job under a lock of its own, and only while it is still the queued job this pass read.

    Several Blender sessions may share a jobs root — that is what resuming and 'started by another Blender session' are for — and each of
    them advances the queue on its own timer. Between reading a status as queued and writing the pid of the worker lies a whole Blender
    being started, and both sessions used to fit inside it: two workers rendered the same frames to the same files, and cancelling the job
    killed only the second.
    """
    try:
        with state.locked(f"job-{job_id}"):
            if _read_status(directory).get("state") != "queued" or _read_status(directory).get("pid"):
                return False
            _launch(job_id, directory, spec)

            return True
    except CommandError:
        return False


def _launch(job_id, directory, spec):
    log = open(os.path.join(directory, "log.txt"), "a")
    arguments = [bpy.app.binary_path, "-b", spec["blend"], "--python", os.path.join(os.path.dirname(os.path.abspath(__file__)), "job_worker.py")]
    if spec.get("threads"):
        arguments += ["-t", str(spec["threads"])]
    arguments += ["--", os.path.join(directory, "spec.json")]
    environment = state.inherited()
    if spec.get("ocio"):
        environment["OCIO"] = spec["ocio"]
    process = subprocess.Popen(arguments, stdout=log, stderr=subprocess.STDOUT, cwd=directory, env=environment)
    JOBS[job_id] = {"process": process, "directory": directory, "log": log}
    spec["attempts"] = spec.get("attempts", 0) + 1
    _write_spec(directory, spec)
    status = _read_status(directory)
    status.update({"state": "running", "pid": process.pid, "started_at": time.time()})
    _write_status(directory, status)


def _is_running(job_id, directory):
    job = JOBS.get(job_id)
    if job is not None:
        return job["process"].poll() is None
    return _worker_alive(directory, _read_status(directory))


def _worker_alive(directory, status):
    """A job this Blender holds no handle for is running only while it says so and its pid is still that job's worker: a finished job keeps its pid, and the pid may name another program by now."""
    return status.get("state") in ACTIVE and state.runs(status.get("pid"), os.path.join(directory, "spec.json"))


def _advance(root):
    """Starts queued jobs while fewer processes run than the smallest max_parallel of the queue allows; retries failed ones that still have attempts left."""
    entries = sorted(entry for entry in os.listdir(root) if entry.startswith("job-") and os.path.isfile(os.path.join(root, entry, "spec.json")))
    _forget_the_scenes_of_parents(root)
    running = [entry for entry in entries if _is_running(entry, os.path.join(root, entry))]
    queued = []
    for entry in entries:
        directory = os.path.join(root, entry)
        status = _read_status(directory)
        spec = _read_spec(directory)
        if spec is None or entry in running:
            continue
        if not _retriable(spec, status):
            _forget_the_scene(entry, directory, status)
        if status.get("state") == "queued" and not status.get("pid"):
            queued.append((-(spec.get("priority") or 0), entry, directory, spec))
        elif _retriable(spec, status) and (status.get("state") == "failed" or _crashed(entry, directory, status)):
            status["state"] = "queued"
            status.pop("pid", None)
            _write_status(directory, status)
            queued.append((-(spec.get("priority") or 0), entry, directory, spec))
    queued.sort()
    for _, entry, directory, spec in queued:
        limit = spec.get("max_parallel", 1)
        if len(running) >= limit:
            break
        if _launched(entry, directory, spec):
            running.append(entry)
    if any(_read_status(os.path.join(root, entry)).get("state") == "queued" for entry in entries) and not bpy.app.background:
        _schedule(root)


def _forget_the_scenes_of_parents(root):
    """The copy a chunked job's children render from, released once the last of them has ended.

    A parent holds the scene and no spec of its own, so the pass that releases an ended job's copy never looked at it: every chunked job
    left a whole scene behind for good, and those are the large ones. The children are asked directly rather than through their status
    alone, because a child that never started has no status yet.
    """
    for entry in os.listdir(root):
        directory = os.path.join(root, entry)
        blend = os.path.join(directory, "scene.blend")
        if not os.path.isfile(os.path.join(directory, "chunks.json")) or not os.path.isfile(blend):
            continue
        children = [name for name in os.listdir(root) if name.startswith(f"{entry}-c") and os.path.isdir(os.path.join(root, name))]
        if not children or any(_read_status(os.path.join(root, name)).get("state") not in ENDED for name in children):
            continue
        with contextlib.suppress(OSError):
            os.remove(blend)


def _retriable(spec, status):
    """A job may be started again while it has attempts left, whether its worker died without a word or wrote down that it failed.

    The retry used to ask for the state 'running', which only a worker that vanished leaves behind. A worker that caught its own failure
    writes 'failed' — the ordinary case, a missing texture or an unwritable output — and that job was never retried, though the reply went
    on saying it had attempts left.
    """
    return status.get("state") in ("running", "failed") and spec.get("attempts", 0) <= spec.get("retries", 0)


def _forget_the_scene(entry, directory, status):
    """A job that has ended keeps its status and its log, and loses the copy of the file it rendered: that copy is the whole scene, tens of
    megabytes a job, and nothing reads it again — a job that may still be retried is not one of these, and is asked about first."""
    if status.get("state") not in ENDED:
        return
    blend = os.path.join(directory, "scene.blend")
    if not os.path.isfile(blend):
        return
    if any(_read_status(os.path.join(os.path.dirname(directory), other)).get("state") in ACTIVE
           for other in os.listdir(os.path.dirname(directory))
           if other.startswith(f"{entry}-c") and os.path.isdir(os.path.join(os.path.dirname(directory), other))):
        return
    with contextlib.suppress(OSError):
        os.remove(blend)


def _crashed(job_id, directory, status):
    """A job whose process ended without finishing: seen through its handle in this session, through its pid after a restart."""
    job = JOBS.get(job_id)
    if job is not None:
        return job["process"].poll() not in (None, 0)
    return not _worker_alive(directory, status)


def _schedule(root):
    """One queue timer per root, however often the queue is advanced; a closure made per call was never found registered, so every tick added one."""
    if root in SCHEDULED:
        return
    SCHEDULED.add(root)
    bpy.app.timers.register(functools.partial(_tick, root), first_interval=QUEUE_INTERVAL_SECONDS)


def _tick(root):
    try:
        _advance(root)
        if _has_queued(root):
            return QUEUE_INTERVAL_SECONDS
    except Exception:
        traceback.print_exc()
        return QUEUE_INTERVAL_SECONDS
    SCHEDULED.discard(root)
    return None


def _has_queued(root):
    return any(_read_status(os.path.join(root, entry)).get("state") == "queued" for entry in os.listdir(root) if entry.startswith("job-"))


def _gpu():
    """The compute backend and devices this Blender renders with, written into the spec. A worker reads only its own saved
    preferences, which on a fresh machine say NONE, and would render a GPU job on the CPU without a word."""
    addon = bpy.context.preferences.addons.get("cycles")
    if addon is None:
        return None
    preferences = addon.preferences
    preferences.get_devices()
    return {"backend": preferences.compute_device_type, "devices": [device.id for device in preferences.devices if device.use]}


def _spec(scene, params, cameras, frame_range, frame_list, frames_total, directory):
    render = {}
    for key, attribute in (("resolution_x", "resolution_x"), ("resolution_y", "resolution_y"), ("percentage", "resolution_percentage")):
        if params.get(key) is not None:
            value = int(params[key])
            if attribute != "resolution_percentage" and not 1 <= value <= MAX_RESOLUTION:
                raise CommandError("BadRequest", f"{key} must be between 1 and {MAX_RESOLUTION}")
            render[attribute] = value
    if params.get("engine"):
        render["engine"] = ENGINES.get(params["engine"].upper(), params["engine"].upper())
    render["use_overwrite"] = bool(params.get("overwrite", True))
    render["use_placeholder"] = bool(params.get("placeholder", False))
    image = {}
    if params.get("color_depth") is not None:
        image["color_depth"] = str(params["color_depth"])
    if params.get("exr_codec"):
        image["exr_codec"] = params["exr_codec"].upper()
    if params.get("color_mode"):
        image["color_mode"] = params["color_mode"].upper()
    return {
        "scene": scene.name, "output": params["output_path"], "cameras": cameras, "view_layers": list(params.get("view_layers") or []),
        "frame_range": list(frame_range) if frame_range else None, "frame_list": frame_list, "frames_total": frames_total * max(1, len(cameras)),
        "render": render, "file_format": (params.get("file_format") or "").upper() or None, "image": image, "samples": params.get("samples"), "gpu": _gpu(),
        "status_path": os.path.join(directory, "status.json"),
    }


def _read_status(directory):
    path = os.path.join(directory, "status.json")
    if not os.path.isfile(path):
        return {"state": "unknown"}
    try:
        with open(path) as handle:
            return json.load(handle)
    except (OSError, ValueError):
        return {"state": "unknown"}


def _log_tail(directory, lines):
    path = os.path.join(directory, "log.txt")
    if lines <= 0 or not os.path.isfile(path):
        return []
    with open(path, errors="replace") as handle:
        return [line.rstrip() for line in handle.readlines()[-lines:]]


def _job_status(job_id, directory, log_lines):
    chunks_path = os.path.join(directory, "chunks.json")
    if os.path.isfile(chunks_path):
        return _chunked_status(job_id, directory, chunks_path, log_lines)
    status = _read_status(directory)
    job = JOBS.get(job_id)
    process = job["process"] if job else None
    exit_code = process.poll() if process else None
    if process is not None and exit_code is None and status.get("state") in ACTIVE:
        phase = status.get("state")
    elif status.get("state") == "cancelled":
        phase = "cancelled"
    elif exit_code not in (None, 0) and status.get("state") != "finished":
        phase = "failed"
    elif status.get("state") == "queued" and not status.get("pid"):
        phase = "queued"
    elif status.get("state") in ACTIVE and process is None:
        phase = "running (started by another Blender session)" if _worker_alive(directory, status) else "interrupted"
    else:
        phase = status.get("state", "unknown")
    spec = _read_spec(directory) or {}
    done, total = status.get("frames_done", 0), status.get("frames_total", 0)
    elapsed = status.get("elapsed_s", 0.0)
    if phase in TERMINAL and job and job["log"] and not job["log"].closed:
        job["log"].close()
    return {
        "id": job_id,
        "state": phase,
        "progress": round(done / total, 3) if total else 0.0,
        "frames_done": done,
        "frames_total": total,
        "frame": status.get("frame"),
        "camera": status.get("camera"),
        "elapsed_s": elapsed,
        "eta_s": round(elapsed / done * (total - done), 1) if done and phase == "running" else None,
        "peak_memory_mb": status.get("peak_memory_mb"),
        "last_stats": status.get("last_stats"),
        "files": status.get("files", [])[-10:],
        "files_written": status.get("files_written", len(status.get("files", []))),
        "error": status.get("error"),
        "exit_code": exit_code,
        "attempts": spec.get("attempts", 0),
        "retries": spec.get("retries", 0),
        "directory": directory,
        "log": _log_tail(directory, log_lines),
    }


def _chunked_status(job_id, directory, chunks_path, log_lines):
    with open(chunks_path) as handle:
        children = json.load(handle).get("children", [])
    root = os.path.dirname(directory)
    parts = [_job_status(child, os.path.join(root, child), 0) for child in children if os.path.isdir(os.path.join(root, child))]
    states = [part["state"] for part in parts]
    if any(state.startswith("running") for state in states):
        state = "running"
    elif any(state == "queued" for state in states):
        state = "queued" if not any(state == "finished" for state in states) else "running"
    elif any(state == "failed" for state in states):
        state = "failed"
    elif any(state == "cancelled" for state in states):
        state = "cancelled"
    elif parts and all(state == "finished" for state in states):
        state = "finished"
    else:
        state = "unknown"
    done = sum(part["frames_done"] for part in parts)
    total = sum(part["frames_total"] for part in parts)
    elapsed = max((part["elapsed_s"] for part in parts), default=0.0)
    return {
        "id": job_id, "state": state, "progress": round(done / total, 3) if total else 0.0, "frames_done": done, "frames_total": total,
        "elapsed_s": elapsed, "eta_s": round(elapsed / done * (total - done), 1) if done and state == "running" else None,
        "peak_memory_mb": max((part.get("peak_memory_mb") or 0 for part in parts), default=0), "files_written": sum(part["files_written"] for part in parts),
        "error": next((part["error"] for part in parts if part.get("error")), None), "chunks": [{"id": part["id"], "state": part["state"], "frames_done": part["frames_done"], "frames_total": part["frames_total"]} for part in parts],
        "directory": directory, "log": _log_tail(os.path.join(root, children[0]), log_lines) if children else [],
    }


@command("render_job_status")
def render_job_status(params):
    root = _job_directory(params)
    lines = int(params.get("log_lines") if params.get("log_lines") is not None else 20)
    _advance(root)
    if params.get("id"):
        directory = os.path.join(root, params["id"])
        if not os.path.isdir(directory):
            raise CommandError("NotFound", f"no render job '{params['id']}'", {"known": sorted(os.listdir(root))[-10:]})
        return _job_status(params["id"], directory, lines)
    jobs = sorted((entry for entry in os.listdir(root) if entry.startswith("job-") and "-c" not in entry[13:]), reverse=True)[:20]
    return {"jobs": [_job_status(job_id, os.path.join(root, job_id), 0) for job_id in jobs], "running": [job_id for job_id in jobs if _is_running(job_id, os.path.join(root, job_id))]}


@command("render_job_cancel")
def render_job_cancel(params):
    job_id = _require(params, "id")
    root = _job_directory(params)
    directory = os.path.join(root, job_id)
    if not os.path.isdir(directory):
        raise CommandError("NotFound", f"no render job '{job_id}'")
    chunks_path = os.path.join(directory, "chunks.json")
    if os.path.isfile(chunks_path):
        with open(chunks_path) as handle:
            children = json.load(handle).get("children", [])
        for child in children:
            _cancel(child, os.path.join(root, child))
        _advance(root)
        return _job_status(job_id, directory, 0)
    _cancel(job_id, directory)
    _advance(root)
    return _job_status(job_id, directory, 5)


def _cancel(job_id, directory):
    """Stops one job and keeps it from being started or retried; the queue advances once, after every job of a cancel is settled, or it would
    start the next queued chunk only to cancel it. A job that has ended stays as it ended, and its old pid is never signalled."""
    status = _read_status(directory)
    if status.get("state") in ENDED:
        return
    job = JOBS.get(job_id)
    _ask_to_stop(directory)
    if job and job["process"].poll() is None:
        _stop(job["process"])
    elif job is None and _worker_alive(directory, status):
        _stop_by_pid(int(status["pid"]))
    status["state"] = "cancelled"
    _write_status(directory, status)
    spec = _read_spec(directory)
    if spec is not None:
        spec["retries"] = 0
        _write_spec(directory, spec)


def _ask_to_stop(directory):
    """Leaves the marker the worker reads between frames, so a cancelled job stops on its own and writes its own last status instead of being killed mid-write."""
    with open(os.path.join(directory, CANCEL_FILE), "w") as handle:
        handle.write(str(time.time()))


def _escalation():
    """How a cancel escalates. SIGINT is the break a background Blender honours; the rest is for a worker that ignores it.
    Windows can send neither SIGINT to another process nor SIGKILL: there the marker is the polite request, a few seconds are
    its chance to be read, and SIGTERM — TerminateProcess on Windows — is the rest."""
    if os.name == "nt":
        return ((None, 3), (signal.SIGTERM, 0))
    return ((signal.SIGINT, 3), (signal.SIGTERM, 3), (signal.SIGKILL, 0))


def _stop(process):
    """Sends the break and waits for it off the main thread, because waiting is what the escalation is made of.

    Each step of it waits whole seconds for a worker deep in a frame, and a chunked job cancels one worker per chunk: the interface froze
    and the bridge answered nothing for the better part of a minute, while requests queued behind it ran out of their deadlines. Nothing in
    the escalation touches Blender, so it belongs on a thread of its own; the marker and the first signal are what the caller waits for.
    """
    threading.Thread(target=_escalate, args=(process,), name="snail-job-cancel", daemon=True).start()


def _escalate(process):
    for number, patience in _escalation():
        if number is not None:
            try:
                process.send_signal(number)
            except OSError:
                return
        if patience == 0:
            return
        try:
            process.wait(timeout=patience)
            return
        except subprocess.TimeoutExpired:
            continue


def _stop_by_pid(pid):
    """The same for a worker this Blender did not start: after a restart of the server the job is known only by its pid."""
    threading.Thread(target=_escalate_by_pid, args=(pid,), name="snail-job-cancel", daemon=True).start()


def _escalate_by_pid(pid):
    for number, patience in _escalation():
        if number is not None:
            try:
                os.kill(pid, number)
            except OSError:
                return
        deadline = time.monotonic() + patience
        while time.monotonic() < deadline:
            if not state.alive(pid):
                return
            time.sleep(0.25)


def _first_file(render, template, frame):
    """The name Blender would give the first frame of a template that is not the scene's own output path."""
    previous = render.filepath
    render.filepath = template
    try:
        return render.frame_path(frame=frame)
    finally:
        render.filepath = previous


def _issue(issues, level, code, message):
    issues.append({"level": level, "code": code, "message": message})


@command("render_check")
def render_check(params):
    scene = _scene(params)
    render = scene.render
    issues = []
    if scene.camera is None:
        _issue(issues, "error", "no_camera", "the scene has no active camera")
    elif scene.camera.type != "CAMERA":
        _issue(issues, "error", "no_camera", f"the scene camera '{scene.camera.name}' is not a camera object")
    output = params.get("output_path") or render.filepath
    directory = os.path.dirname(bpy.path.abspath(output)) or os.getcwd()
    probe = directory
    while probe and not os.path.isdir(probe):
        probe = os.path.dirname(probe)
    if not probe or not os.access(probe, os.W_OK):
        _issue(issues, "error", "output_unwritable", f"cannot write under '{directory}'")
    free_gb = round(shutil.disk_usage(probe or os.getcwd()).free / 1024 ** 3, 1) if probe else None
    if free_gb is not None and free_gb < 2:
        _issue(issues, "warning", "disk_low", f"only {free_gb} GB free next to the output")
    if not bpy.data.filepath:
        _issue(issues, "warning", "unsaved_file", "the file was never saved; relative paths and background jobs work best from a saved file")
    elif bpy.data.is_dirty:
        _issue(issues, "warning", "unsaved_changes", "the file has unsaved changes; blender_save_file keeps them")
    for image in bpy.data.images:
        if image.source == "FILE" and image.users and not image.packed_file and not os.path.isfile(bpy.path.abspath(image.filepath)):
            _issue(issues, "error", "missing_texture", f"image '{image.name}' points at a missing file '{image.filepath}'")
    for library in bpy.data.libraries:
        if not os.path.isfile(bpy.path.abspath(library.filepath)):
            _issue(issues, "error", "missing_library", f"linked library '{library.filepath}' is missing")
    if not any(layer.use for layer in scene.view_layers):
        _issue(issues, "error", "no_view_layer", "every view layer is disabled for rendering")
    if scene.frame_end < scene.frame_start:
        _issue(issues, "error", "frame_range", f"frame range {scene.frame_start}..{scene.frame_end} is empty")
    if render.resolution_x > MAX_RESOLUTION or render.resolution_y > MAX_RESOLUTION:
        _issue(issues, "warning", "resolution", f"{render.resolution_x}×{render.resolution_y} is above the {MAX_RESOLUTION} limit the tools enforce")
    if render.resolution_percentage < 100:
        _issue(issues, "info", "percentage", f"resolution percentage is {render.resolution_percentage}%, the output is smaller than the resolution says")
    if render.engine == "CYCLES" and scene.cycles.device == "GPU":
        preferences = bpy.context.preferences.addons.get("cycles")
        backend = preferences.preferences.compute_device_type if preferences else "NONE"
        if backend == "NONE":
            _issue(issues, "warning", "gpu_unavailable", "Cycles is set to GPU but no compute backend is enabled in Preferences; it will fall back to CPU")
    if scene.world is None:
        _issue(issues, "warning", "no_world", "the scene has no world; renders get a black background and no ambient light")
    if render.engine != "BLENDER_WORKBENCH" and not any(item.type == "LIGHT" and not item.hide_render for item in scene.objects):
        _issue(issues, "warning", "no_lights", "no light object renders; only the world lights the scene")
    if render.image_settings.file_format == "FFMPEG" and render.ffmpeg.codec == "NONE":
        _issue(issues, "error", "no_codec", "video output has no codec")
    if render.use_border and not render.use_crop_to_border:
        _issue(issues, "info", "region", "a render region is set; the frame outside it stays empty")
    if render.image_settings.file_format in ("OPEN_EXR", "OPEN_EXR_MULTILAYER") and render.image_settings.color_depth == "16":
        _issue(issues, "info", "half_float", "EXR at 16 bit (half float); 32 keeps full precision for compositing")
    layer = bpy.context.view_layer if scene == bpy.context.scene else scene.view_layers[0]
    if render.image_settings.file_format in ("OPEN_EXR", "OPEN_EXR_MULTILAYER") and render.image_settings.exr_codec in ("DWAA", "DWAB", "B44", "B44A", "PXR24"):
        data_passes = [name for name, attribute in (("z", "use_pass_z"), ("vector", "use_pass_vector"), ("normal", "use_pass_normal"), ("position", "use_pass_position"), ("cryptomatte", "use_pass_cryptomatte_object")) if getattr(layer, attribute)]
        if data_passes or render.image_settings.color_depth == "32":
            _issue(issues, "warning", "lossy_exr", f"EXR codec {render.image_settings.exr_codec} is lossy; depth, vector, normal and cryptomatte passes need ZIP, ZIPS or PIZ")
    if layer.use_pass_vector and render.use_motion_blur and render.engine == "CYCLES":
        _issue(issues, "warning", "vector_with_motion_blur", "the Vector pass is empty while scene motion blur is on; switch one of them off")
    tree = getattr(scene, "compositing_node_group", None) if "compositing_node_group" in bpy.types.Scene.bl_rna.properties else getattr(scene, "node_tree", None)
    if tree is not None and render.use_compositing:
        has_grain = any(node.name.endswith("Grain") or node.name == "Grain" for node in tree.nodes)
        has_denoise = any(node.bl_idname == "CompositorNodeDenoise" for node in tree.nodes)
        if has_grain and not has_denoise and render.engine == "CYCLES" and not scene.cycles.use_denoising:
            _issue(issues, "warning", "grain_without_denoise", "film grain is added to a noisy render; denoise first, in Cycles or with a Denoise node")
    frames = len(range(scene.frame_start, scene.frame_end + 1, scene.frame_step))
    first_file = _first_file(render, output, scene.frame_start)
    return {
        "scene": scene.name,
        "ready": not any(issue["level"] == "error" for issue in issues),
        "issues": issues,
        "engine": render.engine,
        "camera": scene.camera.name if scene.camera else None,
        "resolution": [render.resolution_x, render.resolution_y, render.resolution_percentage],
        "frames": {"start": scene.frame_start, "end": scene.frame_end, "step": scene.frame_step, "count": frames},
        "output": {"path": output, "first_file": first_file, "format": render.image_settings.file_format, "free_gb": free_gb},
        "view_layers": [layer.name for layer in scene.view_layers if layer.use],
        "file": bpy.data.filepath or None,
    }
