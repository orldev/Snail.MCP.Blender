"""Sequences: a list of bridge commands run one after another in this Blender, so a build of hundreds of steps costs one call.

Every step goes through the same dispatcher as a direct call, which is the whole point: the lease guard refuses a step on
another agent's object, the journal records each step under its own command name, and a step that fails stops the sequence
with the steps that already ran. A script sent to blender_python would do none of that.
"""

import json

from . import core
from .core import COMMANDS, IMMEDIATE, command
from .diagnostics import PROGRESS
from .serialize import to_json
from .server import CommandError

MAX_STEPS = 500
RESULT_BUDGET = 512 * 1024

core.AGENT_AWARE.add("run")


@command("run")
def run(params):
    steps = _steps(params)
    agent = params.get("agent")
    keep_going = bool(params.get("continue_on_error", False))
    done = []
    budget = RESULT_BUDGET
    PROGRESS.update({"active": True, "phase": "run", "step": 0, "steps": len(steps), "started_at": PROGRESS.get("started_at")})
    try:
        for index, step in enumerate(steps, start=1):
            PROGRESS.update({"step": index, "phase": f"run {step['command']}"})
            entry = _step(index, step, agent)
            budget = _trim(entry, budget)
            done.append(entry)
            if not entry["ok"] and not keep_going:
                raise CommandError("StepFailed", f"step {index} ({step['command']}) failed: {entry['error']}", _summary(done, len(steps)))
    finally:
        PROGRESS.update({"active": False, "phase": None, "step": 0, "steps": 0})
    failed = [entry for entry in done if not entry["ok"]]
    if failed:
        raise CommandError("StepFailed", f"{len(failed)} of {len(steps)} steps failed", _summary(done, len(steps)))
    return _summary(done, len(steps))


def _step(index, step, agent):
    arguments = dict(step.get("params") or {})
    arguments["agent"] = agent
    entry = {"index": index, "command": step["command"], "ok": True}
    try:
        entry["result"] = to_json(core.dispatch(step["command"], arguments))
    except Exception as error:
        entry["ok"] = False
        entry["error"] = f"{getattr(error, 'kind', type(error).__name__)}: {error}"
    return entry


def _trim(entry, budget):
    """Keeps a step's result only while the reply has room: the add-on refuses a reply above four megabytes, and a sequence that
    already changed the scene must not fail on the size of its own report. What is dropped is said so, step by step."""
    if "result" not in entry:
        return budget
    size = len(json.dumps(entry["result"], default=str))
    if size > budget:
        entry["result"] = None
        entry["omitted"] = True
        return budget
    return budget - size


def _summary(done, total):
    return {
        "steps": done,
        "ran": len(done),
        "of": total,
        "failed": [entry["index"] for entry in done if not entry["ok"]],
        "omitted": sum(1 for entry in done if entry.get("omitted")),
    }


def _steps(params):
    steps = params.get("commands")
    if not isinstance(steps, list) or not steps or not all(isinstance(step, dict) and isinstance(step.get("command"), str) for step in steps):
        raise CommandError("BadRequest", "commands must be a non-empty list of {command, params}")
    if len(steps) > MAX_STEPS:
        raise CommandError("BadRequest", f"a sequence runs at most {MAX_STEPS} steps; split it or send the rest in a second call", {"steps": len(steps)})
    if any(step["command"] == "run" for step in steps):
        raise CommandError("BadRequest", "a sequence may not contain 'run': flatten the steps into one list")
    asked = {step["command"] for step in steps}
    elsewhere = sorted(asked & set(IMMEDIATE))
    if elsewhere:
        raise CommandError(
            "BadRequest",
            f"{', '.join(elsewhere)} are answered beside Blender's main thread, not on it, so they cannot be steps of a sequence; send each as its own command",
            {"commands": elsewhere})
    unknown = sorted(asked - set(COMMANDS))
    if unknown:
        raise CommandError("UnknownCommand", f"unknown commands: {', '.join(unknown)}", {"known": sorted(COMMANDS)})
    return steps
