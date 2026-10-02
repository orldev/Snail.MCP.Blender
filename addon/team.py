"""Several agents in one Blender: who did what (the journal) and who holds what (leases on objects and collections).

Both live in files under the state directory, so they survive a restart of Blender and are shared with the
worker Blenders that batches start; every mutating command passes through the hooks below. A render job's worker runs no
commands, so it neither holds leases nor writes the journal.
"""

import contextlib
import os
import time

import bpy

from . import catalog, core, state
from .core import command
from .server import CommandError

JOURNAL_FILE = "journal.jsonl"
LEASES_FILE = "leases.json"
JOURNAL_LIMIT = 2000
DEFAULT_LEASE_SECONDS = 900
ANONYMOUS = "anonymous"
TARGET_KEYS = catalog.TARGET_KEYS
NAMED_AFTER_SOMETHING_ELSE = catalog.NAMED_AFTER_SOMETHING_ELSE


def is_read_only(name, params):
    return catalog.is_read_only(name, params)


def _now():
    return time.time()


def _selected():
    """The objects a command that works on the selection would touch; a Blender without an interface answers some of these differently."""
    selected = core.selected_objects()
    if selected is not None:
        return [item.name for item in selected]
    with contextlib.suppress(AttributeError):
        return [item.name for item in bpy.context.view_layer.objects if item.select_get()]
    return []


def _names_at(value, path):
    """The names one declared path finds: a string is itself, a list gives its strings, a block gives its keys."""
    if value is None:
        return []
    if not path:
        if isinstance(value, str):
            return [value]
        if isinstance(value, list):
            return [item for item in value if isinstance(item, str)]
        if isinstance(value, dict):
            return [key for key in value if isinstance(key, str)]
        return []
    if not isinstance(value, dict):
        return []
    step, _, rest = path.partition(".")
    if step.endswith("[]"):
        inner = value.get(step[:-2])
        return [name for item in inner for name in _names_at(item, rest)] if isinstance(inner, list) else []
    return _names_at(value.get(step), rest)


def _acted_on(params, name):
    """The objects and collections already in the file that this command will work on."""
    names = list(_selected()) if params.get("selected") else []
    for path in catalog.targets_of(name):
        names.extend(_names_at(params, path))
    return names


def _mentioned(params, name):
    """The names a command carries in the parameters that usually hold one, whatever those names turn out to be."""
    names = []
    for key in (TARGET_KEYS[1:] if name in NAMED_AFTER_SOMETHING_ELSE else TARGET_KEYS):
        value = params.get(key)
        if isinstance(value, str):
            names.append(value)
        elif isinstance(value, list):
            names.extend(item for item in value if isinstance(item, str))
    return names


def _expanded(names):
    """A collection stands for every object in it, because a lease on the collection is a lease on its contents."""
    expanded = []
    for item in names:
        collection = bpy.data.collections.get(item)
        if collection is not None:
            expanded.extend(member.name for member in collection.all_objects)
        expanded.append(item)
    return expanded


def _addressed(params, name=None):
    """What the guard asks about: only what the command will really act on.

    It has to be exact in both directions, and a list of parameters that usually hold a name was neither. It missed a name written
    anywhere else — inside a block, in a parameter of its own, as the key of a block — so a command could move an object another
    agent held and say nothing. And it refused too much, because 'name' is the parameter that holds a material, a compositor node,
    a sequencer strip, an operator and the name of something about to be created, and only the handler knows which of those it
    looks up: a lease on an object called Steel stopped a material called Steel being edited.
    """
    return _expanded(_acted_on(params, name))


def _targets(params, name=None):
    """What the journal records: what the command acted on, and what else it named.

    Wider than the guard's reading, on purpose. An object created under a name belongs in the record even though no lease could
    have been held on a name nothing had yet, and so does the material or the strip a command was about, because the record is
    there to answer what an agent did rather than to refuse anybody.
    """
    return _expanded(_acted_on(params, name) + _mentioned(params, name))


def _leases():
    now = _now()
    return {target: lease for target, lease in state.load(LEASES_FILE, {}).items() if lease.get("until", 0) > now}


def guard(name, params, agent):
    """Refuses a mutating command on an object another agent holds; read-only commands and the holder pass."""
    if is_read_only(name, params) or name in core.AGENT_AWARE or not isinstance(params, dict):
        return
    holder = agent or ANONYMOUS
    leases = _leases()
    blocked = [(target, leases[target]) for target in _addressed(params, name) if target in leases and leases[target]["agent"] != holder]
    if blocked:
        target, lease = blocked[0]
        raise CommandError("Leased", f"'{target}' is held by agent '{lease['agent']}' until {time.strftime('%H:%M:%S', time.localtime(lease['until']))}",
                           {"agent": lease["agent"], "target": target, "note": lease.get("note"), "seconds_left": round(lease["until"] - _now())})


def _refuse_or_pass(name, params, agent):
    try:
        guard(name, params, agent)
    except CommandError as refusal:
        record(name, params, agent, False, f"Leased: {refusal}")
        raise


def record(name, params, agent, ok, error=None):
    if is_read_only(name, params):
        return
    entry = {
        "time": time.strftime("%Y-%m-%d %H:%M:%S"),
        "agent": agent or ANONYMOUS,
        "command": name,
        "targets": sorted(set(_targets(params, name))) if isinstance(params, dict) else [],
        "ok": ok,
        "error": error,
        "pid": os.getpid(),
    }
    if catalog.is_unguarded(name):
        entry["unguarded"] = True
        entry["detail"] = str(params.get("name") or params.get("code") or "")[:120] if isinstance(params, dict) else ""
    with state.locked(JOURNAL_FILE):
        previous = state.last(JOURNAL_FILE)
        entry["index"] = (previous["index"] + 1) if previous and isinstance(previous.get("index"), int) else 1
        state.append(JOURNAL_FILE, entry)


core.BEFORE.append(_refuse_or_pass)
core.AFTER.append(record)
core.AGENT_AWARE.add("lease")


@command("journal")
def journal(params):
    """The record of what each agent did; the filter by agent is called author, because 'agent' is the key the link signs every request with."""
    with state.locked(JOURNAL_FILE):
        state.trim(JOURNAL_FILE, JOURNAL_LIMIT * 2)
        entries = state.lines(JOURNAL_FILE)[-JOURNAL_LIMIT:]
    since = int(params.get("since") or 0)
    latest = entries[-1]["index"] if entries else 0
    agents = sorted({entry["agent"] for entry in entries})
    entries = [entry for entry in entries if entry["index"] > since]
    if params.get("author"):
        entries = [entry for entry in entries if entry["agent"] == params["author"]]
    if params.get("target"):
        entries = [entry for entry in entries if params["target"] in entry["targets"]]
    limit = max(1, min(500, int(params.get("limit") or 100)))
    return {"entries": entries[-limit:], "latest": latest, "agents": agents, "file": state.path(JOURNAL_FILE)}


def _lease_summary(leases):
    now = _now()
    return [{"target": name, "agent": lease["agent"], "seconds_left": round(lease["until"] - now), "note": lease.get("note")} for name, lease in sorted(leases.items())]


@command("lease")
def lease(params):
    action = (params.get("action") or "list").lower()
    agent = params.get("agent") or ANONYMOUS
    if action == "list":
        return {"agent": agent, "leases": _lease_summary(_leases())}
    if action == "clear":
        with state.locked(LEASES_FILE):
            foreign = sorted(target for target, held in _leases().items() if held["agent"] != agent)
            if foreign and not params.get("force"):
                raise CommandError("Leased", f"held by another agent: {', '.join(foreign)}; pass force to clear them too", {"held": foreign})
            state.save(LEASES_FILE, {})
        return {"agent": agent, "leases": []}
    names = list(params.get("names") or ([params["name"]] if params.get("name") else []))
    if not names:
        raise CommandError("BadRequest", "pass names to claim or release")
    targets = sorted(set(_targets({"names": names})))
    with state.locked(LEASES_FILE):
        leases = _leases()
        if action == "claim":
            held = [name for name in targets if name in leases and leases[name]["agent"] != agent]
            if held:
                raise CommandError("Leased", f"already held by another agent: {', '.join(held)}", {"held": [{"target": name, "agent": leases[name]["agent"]} for name in held]})
            until = _now() + max(10, int(params.get("ttl_seconds") or DEFAULT_LEASE_SECONDS))
            for name in targets:
                leases[name] = {"agent": agent, "until": until, "note": params.get("note")}
            state.save(LEASES_FILE, leases)
            return {"agent": agent, "claimed": targets, "until": time.strftime("%H:%M:%S", time.localtime(until)), "leases": _lease_summary(leases)}
        if action == "release":
            foreign = [name for name in targets if name in leases and leases[name]["agent"] != agent and not params.get("force")]
            if foreign:
                raise CommandError("Leased", f"held by another agent: {', '.join(foreign)}; pass force to take them", {"held": foreign})
            released = [name for name in targets if leases.pop(name, None) is not None]
            state.save(LEASES_FILE, leases)
            return {"agent": agent, "released": released, "leases": _lease_summary(leases)}
    raise CommandError("BadRequest", "action must be claim, release, list or clear")
