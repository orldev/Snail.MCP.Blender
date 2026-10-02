"""Snail Bridge: a TCP listener, on the loopback unless a container names another address, that runs MCP commands on Blender's main thread."""

import argparse
import time
import traceback

import bpy

from . import (
    animation,
    assets,
    batch,
    budget,
    cameras,
    collections,
    compositor,
    constraints,
    core,
    diagnostics,
    effects,
    engines,
    files,
    images,
    interchange,
    jobs,
    layers,
    lights,
    materials,
    modeling,
    moves,
    objects,
    operators,
    optics,
    output,
    parameters,
    physics,
    policy,
    rendering,
    scene,
    scenes,
    sequence,
    sequencer,
    server,
    state,
    storage,
    team,
    transfer,
    visibility,
    world,
)

_bridge = None
_cli = None
HEADLESS_TICK_SECONDS = 0.01
LOOPBACK = "127.0.0.1"


def _preferences():
    return bpy.context.preferences.addons[__package__].preferences


def is_running():
    return _bridge is not None and _bridge.is_running


def start(port=None, token=None, host=LOOPBACK):
    """Listens on the loopback, on the port and with the token of the preferences unless they are given, as the headless loop and the live tests give them.

    Another host is for a container only: there the MCP server runs in a sibling container on a private network, and the
    token is the one thing between that network and Blender.
    """
    global _bridge
    if is_running():
        return _bridge
    preferences = _preferences() if port is None or token is None else None
    _bridge = server.BridgeServer(host, port or preferences.port, core.dispatch, core.IMMEDIATE, state.token(preferences.token if token is None else token))
    _bridge.start()
    jobs.resume()
    return _bridge


def serve(port=None, token=None, blend=None, ready=None, host=LOOPBACK, backend=None, python=None):
    """Keeps a Blender without an interface answering the MCP server until the process ends.

    blender -b runs no timers, and the interface runs three things on them: the bridge's queue, the autostart and the render
    job queue. This loop runs the first and the last itself, and the listener starts here instead of on the autostart timer.
    A backend is chosen before listening, because a container starts from preferences that know no GPU.
    """
    policy.choose(python)
    if blend:
        bpy.ops.wm.open_mainfile(filepath=blend, load_ui=False)
    if backend:
        engines._use_backend(backend.upper())
    storage.remember_compute()
    bridge = start(port, token, host)
    print(f"Snail Bridge listening on {bridge.host}:{bridge.port}", flush=True)
    if ready is not None:
        ready()
    queue_due = 0.0
    while bridge.is_running:
        bridge.pump()
        if time.monotonic() >= queue_due:
            queue_due = time.monotonic() + jobs.QUEUE_INTERVAL_SECONDS
            _survive(jobs.advance)
        time.sleep(HEADLESS_TICK_SECONDS)


def _survive(step):
    """Runs one step of the headless loop; a step that fails is reported and the loop goes on, because leaving it would end Blender."""
    try:
        step()
    except Exception:
        traceback.print_exc()


def _command(arguments):
    """blender -b -c snail_bridge [--host H] [--port N] [--token-file F] [--backend B] [--file scene.blend]: a Blender with no interface that serves the MCP server until it is stopped."""
    parser = argparse.ArgumentParser(prog="blender -b -c snail_bridge", description="Serve the Snail MCP server from a Blender without an interface.")
    parser.add_argument("--host", default=LOOPBACK, help="address to listen on; the loopback unless the server runs in a sibling container")
    parser.add_argument("--port", type=int, help="port to listen on; the add-on preferences otherwise")
    parser.add_argument("--token-file", help="file holding the token the server presents; the add-on preferences or a generated token otherwise")
    parser.add_argument("--backend", help="Cycles compute backend to enable before serving: OPTIX, CUDA, HIP, ONEAPI, METAL or NONE")
    parser.add_argument("--python", help="how far Python may go here: allow, fallback or off; off refuses scripts, the operators that run or install code, and files outside the areas")
    parser.add_argument("--file", help=".blend file to open before serving")
    options = parser.parse_args(arguments)
    if not bpy.app.background:
        print("snail_bridge: start Blender with -b for this command; with the interface open the add-on listens on its own")
        return 1
    token = _read_token_file(options.token_file) if options.token_file else None
    if token == "":
        print(f"snail_bridge: the token file {options.token_file} is missing or empty")
        return 1
    try:
        serve(options.port, token, options.file, host=options.host, backend=options.backend, python=options.python)
    except (server.CommandError, OSError) as error:
        print(f"snail_bridge: {error}")
        return 1
    return 0


def _read_token_file(path):
    try:
        with open(path, encoding="utf-8") as handle:
            return handle.read().strip()
    except OSError:
        return ""


def stop():
    global _bridge
    if _bridge is not None:
        _bridge.stop()
        _bridge = None


class SnailBridgePreferences(bpy.types.AddonPreferences):
    bl_idname = __package__

    port: bpy.props.IntProperty(
        name="Port",
        description="Port the bridge listens on; the MCP server must use the same one",
        default=9876,
        min=1024,
        max=65535,
    )
    autostart: bpy.props.BoolProperty(
        name="Start with Blender",
        description="Start listening as soon as the add-on loads",
        default=True,
    )
    token: bpy.props.StringProperty(
        name="Token",
        description="Shared secret every request must carry; empty generates one into the data directory, which the server reads on its own",
        default="",
        subtype="PASSWORD",
    )

    def draw(self, context):
        layout = self.layout
        layout.prop(self, "port")
        layout.prop(self, "autostart")
        layout.prop(self, "token")


class SNAIL_OT_start_bridge(bpy.types.Operator):
    bl_idname = "snail.start_bridge"
    bl_label = "Start Snail Bridge"
    bl_description = "Listen for the MCP server on the configured port"

    def execute(self, context):
        try:
            bridge = start()
        except OSError as error:
            self.report({"ERROR"}, f"Snail Bridge could not listen: {error}")
            return {"CANCELLED"}
        self.report({"INFO"}, f"Snail Bridge listening on {bridge.host}:{bridge.port}")
        return {"FINISHED"}


class SNAIL_OT_stop_bridge(bpy.types.Operator):
    bl_idname = "snail.stop_bridge"
    bl_label = "Stop Snail Bridge"
    bl_description = "Close the listener and drop the MCP connection"

    def execute(self, context):
        stop()
        self.report({"INFO"}, "Snail Bridge stopped")
        return {"FINISHED"}


class SNAIL_PT_bridge(bpy.types.Panel):
    bl_label = "Snail Bridge"
    bl_space_type = "VIEW_3D"
    bl_region_type = "UI"
    bl_category = "Snail"

    def draw(self, context):
        layout = self.layout
        if is_running():
            state = "connected" if _bridge.has_client else "listening"
            layout.label(text=f"{state} on port {_bridge.port}", icon="LINKED")
            layout.operator(SNAIL_OT_stop_bridge.bl_idname, icon="PAUSE")
        else:
            layout.label(text="stopped", icon="UNLINKED")
            layout.operator(SNAIL_OT_start_bridge.bl_idname, icon="PLAY")
        layout.prop(_preferences(), "port")


_classes = (SnailBridgePreferences, SNAIL_OT_start_bridge, SNAIL_OT_stop_bridge, SNAIL_PT_bridge)


def _autostart():
    if _preferences().autostart:
        try:
            start()
        except OSError as error:
            print(f"Snail Bridge: could not listen: {error}")
    return


def register():
    global _cli
    for cls in _classes:
        bpy.utils.register_class(cls)
    bpy.app.timers.register(_autostart, first_interval=0.5)
    _cli = bpy.utils.register_cli_command("snail_bridge", _command)


def unregister():
    global _cli
    stop()
    if _cli is not None:
        bpy.utils.unregister_cli_command(_cli)
        _cli = None
    for cls in reversed(_classes):
        bpy.utils.unregister_class(cls)
