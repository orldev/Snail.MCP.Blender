"""The batch worker: a headless Blender runs this on a copy of the file, dispatches each command through the add-on and saves the result.

It takes the Python setting from the spec and chooses it before the first step. The setting belongs to the Blender the batch was asked of,
and this is another Blender: without carrying it, one batch step ran arbitrary code on a Blender started with python off, which is the one
thing that setting exists to stop.
"""

import importlib
import json
import os
import sys
import time

import bpy

with open(sys.argv[sys.argv.index("--") + 1]) as handle:
    spec = json.load(handle)
sys.path.insert(0, spec["package_parent"])
package = importlib.import_module(spec["package_name"])
core = importlib.import_module(f"{spec['package_name']}.core")
policy = importlib.import_module(f"{spec['package_name']}.policy")
policy.choose(spec.get("python") or "allow")
status = {"state": "running", "steps": [], "started_at": time.time(), "elapsed_s": 0.0, "output": None, "error": None, "pid": os.getpid()}


def write():
    status["elapsed_s"] = round(time.time() - status["started_at"], 2)
    with open(spec["status_path"] + ".tmp", "w") as handle:
        json.dump(status, handle, default=str)
    os.replace(spec["status_path"] + ".tmp", spec["status_path"])


try:
    if spec.get("input"):
        bpy.ops.wm.open_mainfile(filepath=spec["input"], load_ui=False)
    for index, step in enumerate(spec["commands"]):
        params = dict(step.get("params") or {})
        params["agent"] = spec.get("agent")
        entry = {"index": index + 1, "command": step["command"], "ok": False}
        status["steps"].append(entry)
        write()
        try:
            result = core.dispatch(step["command"], params)
            entry.update({"ok": True, "result": json.loads(json.dumps(result, default=str))})
        except Exception as error:
            entry.update({"ok": False, "error": f"{getattr(error, 'kind', type(error).__name__)}: {error}"})
            write()
            if not spec.get("continue_on_error"):
                raise
        write()
    if spec.get("output"):
        os.makedirs(os.path.dirname(spec["output"]) or ".", exist_ok=True)
        bpy.ops.wm.save_as_mainfile(filepath=spec["output"], relative_remap=True)
        status["output"] = spec["output"]
    status["state"] = "finished"
except Exception as error:
    status["state"] = "failed"
    status["error"] = f"{type(error).__name__}: {error}"
    write()
    raise
write()
