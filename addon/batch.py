"""Batches: a list of bridge commands run by a separate headless Blender on a copy of the file, so several agents' work proceeds in parallel and lands in files to link."""

import json
import os
import subprocess
import time

import bpy

from . import core, policy, state
from .core import COMMANDS, command
from .server import CommandError

BATCHES = {}
core.AGENT_AWARE.add("batch_job")


def _root(params):
    root = params.get("directory") or os.path.join(state.data_directory(), "batches")
    os.makedirs(root, exist_ok=True)
    return root


def _package():
    package_dir = os.path.dirname(os.path.abspath(__file__))
    return os.path.dirname(package_dir), os.path.basename(package_dir)


@command("batch_job")
def batch_job(params):
    steps = params.get("commands")
    if not isinstance(steps, list) or not steps or not all(isinstance(step, dict) and isinstance(step.get("command"), str) for step in steps):
        raise CommandError("BadRequest", "commands must be a list of {command, params}")
    unknown = sorted({step["command"] for step in steps} - set(COMMANDS))
    if unknown:
        raise CommandError("UnknownCommand", f"unknown commands: {', '.join(unknown)}")
    root = _root(params)
    batch_id = time.strftime("batch-%Y%m%d-%H%M%S") + f"-{int(time.time() * 1000) % 1000:03d}"
    directory = os.path.join(root, batch_id)
    os.makedirs(directory, exist_ok=True)
    source = params.get("input")
    if source is None:
        source = os.path.join(directory, "input.blend")
        bpy.ops.wm.save_as_mainfile(filepath=source, copy=True, relative_remap=True)
    elif not os.path.isfile(source):
        raise CommandError("NotFound", f"no .blend file at '{source}'")
    package_parent, package_name = _package()
    spec = {
        "commands": steps, "input": source, "output": params.get("output") or os.path.join(directory, "output.blend"), "agent": params.get("agent"),
        "continue_on_error": bool(params.get("continue_on_error", False)), "package_parent": package_parent, "package_name": package_name,
        "status_path": os.path.join(directory, "status.json"), "name": params.get("name") or batch_id,
        "python": policy.ACCESS["python"],
    }
    with open(os.path.join(directory, "spec.json"), "w") as handle:
        json.dump(spec, handle, indent=2)
    with open(spec["status_path"], "w") as handle:
        json.dump({"state": "queued", "steps": [], "started_at": time.time(), "elapsed_s": 0.0}, handle)
    log = open(os.path.join(directory, "log.txt"), "w")
    process = subprocess.Popen([bpy.app.binary_path, "-b", "--factory-startup", "--python", os.path.join(package_parent, package_name, "batch_worker.py"), "--", os.path.join(directory, "spec.json")],
                               stdout=log, stderr=subprocess.STDOUT, cwd=directory, env=state.inherited())
    BATCHES[batch_id] = {"process": process, "directory": directory, "log": log}
    return _batch_status(batch_id, directory, 0)


def _batch_status(batch_id, directory, log_lines):
    try:
        with open(os.path.join(directory, "status.json")) as handle:
            status = json.load(handle)
    except (OSError, ValueError):
        status = {"state": "unknown", "steps": []}
    batch = BATCHES.get(batch_id)
    exit_code = batch["process"].poll() if batch else None
    current = status.get("state", "unknown")
    if exit_code not in (None, 0) and current != "finished":
        current = "failed"
    elif current == "running" and batch is None and not state.alive(status.get("pid")):
        current = "interrupted"
    steps = status.get("steps", [])
    log = []
    if log_lines > 0 and os.path.isfile(os.path.join(directory, "log.txt")):
        with open(os.path.join(directory, "log.txt"), errors="replace") as handle:
            log = [line.rstrip() for line in handle.readlines()[-log_lines:]]
    return {
        "id": batch_id, "state": current, "steps_done": sum(1 for step in steps if step.get("ok")), "steps_total": len(steps), "steps": steps[-20:],
        "elapsed_s": status.get("elapsed_s", 0.0), "output": status.get("output"), "error": status.get("error"), "exit_code": exit_code, "directory": directory, "log": log,
    }


@command("batch_status")
def batch_status(params):
    root = _root(params)
    lines = int(params.get("log_lines") if params.get("log_lines") is not None else 10)
    if params.get("id"):
        directory = os.path.join(root, params["id"])
        if not os.path.isdir(directory):
            raise CommandError("NotFound", f"no batch '{params['id']}'", {"known": sorted(os.listdir(root))[-10:]})
        return _batch_status(params["id"], directory, lines)
    batches = sorted((entry for entry in os.listdir(root) if entry.startswith("batch-")), reverse=True)[:20]
    return {"batches": [_batch_status(batch_id, os.path.join(root, batch_id), 0) for batch_id in batches]}
