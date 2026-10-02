"""The world: background colour or HDRI, strength and mist."""

import math
import os

import bpy

from .core import _name, command
from .server import CommandError


def _world():
    scene = bpy.context.scene
    world = scene.world
    if world is None:
        world = bpy.data.worlds.new("World")
        scene.world = world
    world.use_nodes = True
    return world


def _background(world):
    tree = world.node_tree
    node = next((node for node in tree.nodes if node.bl_idname == "ShaderNodeBackground"), None)
    if node is None:
        node = tree.nodes.new("ShaderNodeBackground")
        output = next((node for node in tree.nodes if node.bl_idname == "ShaderNodeOutputWorld"), None) or tree.nodes.new("ShaderNodeOutputWorld")
        tree.links.new(node.outputs["Background"], output.inputs["Surface"])
    return node


@command("set_world")
def set_world(params):
    world = _world()
    tree = world.node_tree
    background = _background(world)
    if params.get("color") is not None:
        color = list(params["color"])
        background.inputs["Color"].default_value = [*color, 1.0] if len(color) == 3 else color
    if params.get("strength") is not None:
        background.inputs["Strength"].default_value = float(params["strength"])
    if params.get("hdri_path"):
        path = params["hdri_path"]
        if not os.path.isfile(path):
            raise CommandError("NotFound", f"no image file at '{path}'")
        environment = next((node for node in tree.nodes if node.bl_idname == "ShaderNodeTexEnvironment"), None) or tree.nodes.new("ShaderNodeTexEnvironment")
        environment.image = bpy.data.images.load(path, check_existing=True)
        environment.location = (-400, 0)
        tree.links.new(environment.outputs["Color"], background.inputs["Color"])
        if params.get("rotation") is not None:
            mapping = next((node for node in tree.nodes if node.bl_idname == "ShaderNodeMapping"), None) or tree.nodes.new("ShaderNodeMapping")
            coordinates = next((node for node in tree.nodes if node.bl_idname == "ShaderNodeTexCoord"), None) or tree.nodes.new("ShaderNodeTexCoord")
            mapping.inputs["Rotation"].default_value = (0.0, 0.0, math.radians(float(params["rotation"])))
            tree.links.new(coordinates.outputs["Generated"], mapping.inputs["Vector"])
            tree.links.new(mapping.outputs["Vector"], environment.inputs["Vector"])
    elif params.get("hdri_path") == "":
        for node in [node for node in tree.nodes if node.bl_idname == "ShaderNodeTexEnvironment"]:
            tree.nodes.remove(node)
    mist = world.mist_settings
    if params.get("mist") is not None:
        mist.use_mist = bool(params["mist"])
    if params.get("mist_start") is not None:
        mist.start = float(params["mist_start"])
    if params.get("mist_depth") is not None:
        mist.depth = float(params["mist_depth"])
    environment_node = next((node for node in tree.nodes if node.bl_idname == "ShaderNodeTexEnvironment"), None)
    return {
        "world": world.name,
        "color": list(background.inputs["Color"].default_value),
        "strength": background.inputs["Strength"].default_value,
        "hdri": _name(environment_node.image) if environment_node else None,
        "mist": {"enabled": mist.use_mist, "start": mist.start, "depth": mist.depth},
    }
