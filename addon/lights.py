"""Light objects: type, energy, colour in kelvin or RGB, shape and IES profiles."""

import math
import os

import bpy

from .core import _object, _require, command
from .objects import _degrees, _link_new_object, _object_summary, _radians, _vector
from .serialize import to_json
from .server import CommandError

LIGHTS = ("POINT", "SUN", "SPOT", "AREA")


def _blackbody(kelvin):
    """Approximate sRGB of a black body, the Tanner Helland fit, normalised so 6500 K is white."""
    temperature = max(1000.0, min(40000.0, float(kelvin))) / 100.0
    red = 255.0 if temperature <= 66 else 329.698727446 * ((temperature - 60) ** -0.1332047592)
    green = 99.4708025861 * math.log(temperature) - 161.1195681661 if temperature <= 66 else 288.1221695283 * ((temperature - 60) ** -0.0755148492)
    blue = 255.0 if temperature >= 66 else (0.0 if temperature <= 19 else 138.5177312231 * math.log(temperature - 10) - 305.0447927307)
    channels = [max(0.0, min(255.0, value)) / 255.0 for value in (red, green, blue)]
    return [round(((value + 0.055) / 1.055) ** 2.4 if value > 0.04045 else value / 12.92, 4) for value in channels]


@command("add_light")
def add_light(params):
    kind = _require(params, "type").upper()
    if kind not in LIGHTS:
        raise CommandError("BadRequest", f"unknown light type '{kind}'", {"known": list(LIGHTS)})
    name = params.get("name") or kind.capitalize()
    data = bpy.data.lights.new(name, kind)
    item = _link_new_object(bpy.data.objects.new(name, data))
    item.location = _vector(params, "location", (4.0, 1.0, 6.0))
    item.rotation_euler = _radians(_vector(params, "rotation"))
    return _apply_light(item, params)


@command("set_light")
def set_light(params):
    item = _object(_require(params, "name"))
    if item.type != "LIGHT":
        raise CommandError("BadRequest", f"'{item.name}' is a {item.type}, not a light")
    return _apply_light(item, params)


LIGHT_SHAPES = ("SQUARE", "RECTANGLE", "DISK", "ELLIPSE")


def _apply_light(item, params):
    light = item.data
    if params.get("energy") is not None:
        light.energy = float(params["energy"])
    if params.get("color") is not None:
        light.color = _vector(params, "color", (1.0, 1.0, 1.0))
        if "snail_kelvin" in light:
            del light["snail_kelvin"]
    if params.get("temperature") is not None:
        light.color = _blackbody(float(params["temperature"]))
        light["snail_kelvin"] = float(params["temperature"])
    if params.get("shadow") is not None:
        light.use_shadow = bool(params["shadow"])
    if light.type == "SPOT":
        if params.get("spot_size") is not None:
            light.spot_size = math.radians(float(params["spot_size"]))
        if params.get("spot_blend") is not None:
            light.spot_blend = float(params["spot_blend"])
    if light.type == "AREA" and params.get("size") is not None:
        light.size = float(params["size"])
    if light.type == "AREA":
        if params.get("shape"):
            shape = params["shape"].upper()
            if shape not in LIGHT_SHAPES:
                raise CommandError("BadRequest", f"unknown area shape '{shape}'", {"known": list(LIGHT_SHAPES)})
            light.shape = shape
        if params.get("size_y") is not None:
            light.size_y = float(params["size_y"])
        if params.get("spread") is not None:
            light.spread = math.radians(float(params["spread"]))
    if params.get("ies_path") is not None:
        _apply_ies(light, params["ies_path"])
    if light.type == "SUN" and params.get("angle") is not None:
        light.angle = math.radians(float(params["angle"]))
    if light.type in ("POINT", "SPOT") and params.get("radius") is not None:
        light.shadow_soft_size = float(params["radius"])
    return _light_summary(item)


def _apply_ies(light, path):
    """An IES profile through the light's node tree: Blender reads the file with a Texture IES node feeding the emission strength."""
    tree = light.node_tree if light.use_nodes else None
    if not path:
        if tree is not None:
            for node in [node for node in tree.nodes if node.bl_idname == "ShaderNodeTexIES"]:
                tree.nodes.remove(node)
            light.use_nodes = False
        return
    if not os.path.isfile(path):
        raise CommandError("NotFound", f"no IES file at '{path}'")
    light.use_nodes = True
    tree = light.node_tree
    emission = next((node for node in tree.nodes if node.bl_idname == "ShaderNodeEmission"), None) or tree.nodes.new("ShaderNodeEmission")
    output = next((node for node in tree.nodes if node.bl_idname == "ShaderNodeOutputLight"), None) or tree.nodes.new("ShaderNodeOutputLight")
    tree.links.new(emission.outputs["Emission"], output.inputs["Surface"])
    ies = next((node for node in tree.nodes if node.bl_idname == "ShaderNodeTexIES"), None) or tree.nodes.new("ShaderNodeTexIES")
    ies.mode = "EXTERNAL"
    ies.filepath = path
    ies.location = (emission.location.x - 250, emission.location.y)
    tree.links.new(ies.outputs["Fac"], emission.inputs["Strength"])


def _light_summary(item):
    light = item.data
    summary = _object_summary(item)
    summary.update({
        "light_type": light.type,
        "energy": light.energy,
        "color": to_json(light.color),
        "shadow": light.use_shadow,
        "kelvin": light.get("snail_kelvin"),
        "ies": next((node.filepath for node in light.node_tree.nodes if node.bl_idname == "ShaderNodeTexIES"), None) if light.use_nodes and light.node_tree else None,
    })
    if light.type == "SPOT":
        summary["spot_size"] = round(math.degrees(light.spot_size), 4)
        summary["spot_blend"] = light.spot_blend
    if light.type == "AREA":
        summary["size"] = light.size
        summary["shape"] = light.shape
        summary["size_y"] = light.size_y
        summary["spread"] = round(math.degrees(light.spread), 2)
    if light.type == "SUN":
        summary["angle"] = round(math.degrees(light.angle), 4)
    summary["rotation_euler"] = _degrees(item.rotation_euler)
    return summary
