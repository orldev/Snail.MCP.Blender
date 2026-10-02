"""The post stack on the beauty pass, in a compositor's order: denoise, defocus or vector blur, glare, halation, chromatic aberration, vignette, film grain."""

import math
import os

import bpy

from .compositor import _compositor_tree, _output, _render_layers
from .core import _scene, command
from .output import _file_output_node, _new_slot, _slot_inputs
from .server import CommandError

PREFIX = "FX:"
GLARE_TYPES = {"BLOOM": "Bloom", "GHOSTS": "Ghosts", "STREAKS": "Streaks", "FOG_GLOW": "Fog Glow", "SIMPLE_STAR": "Simple Star", "SUN_BEAMS": "Sun Beams"}
QUALITIES = {"HIGH": "High", "MEDIUM": "Medium", "LOW": "Low"}
PREFILTERS = {"NONE": "None", "FAST": "Fast", "ACCURATE": "Accurate"}
DENOISE_QUALITIES = {"FOLLOW_SCENE": "Follow Scene", "HIGH": "High", "BALANCED": "Balanced", "FAST": "Fast"}
DISPLAY_SPACES = {"AgX": "AgX Base sRGB", "Filmic": "Filmic sRGB", "Khronos PBR Neutral": "Khronos PBR Neutral sRGB"}
ORDER = ("denoise", "defocus", "vector_blur", "glare", "halation", "chromatic_aberration", "vignette", "grain")


class Stack:
    """Builds nodes left to right from a source socket and remembers them, so the chain reads in the graph as it reads in the reply."""

    def __init__(self, tree, source, x, y):
        self.tree = tree
        self.source = source
        self.x = x
        self.y = y
        self.chain = []
        self.warnings = []
        self.passes = []

    def node(self, kind, label, column=0, row=0):
        node = self.tree.nodes.new(kind)
        node.name = f"{PREFIX}{label}"
        node.label = label
        node.location = (self.x + column * 220, self.y - row * 260)
        return node

    def step(self, node, input_name, output_name, label):
        self.tree.links.new(self.source, node.inputs[input_name])
        self.source = node.outputs[output_name]
        self.chain.append(label)
        self.x += 260

    def socket(self, node, name, value):
        socket = node.inputs.get(name)
        if socket is None or value is None:
            return
        if socket.type == "RGBA" and isinstance(value, list):
            socket.default_value = list(value) + [1.0] * (4 - len(value))
        elif socket.type == "VECTOR" and isinstance(value, list):
            socket.default_value = tuple(value)
        elif socket.type == "MENU":
            socket.default_value = value
        else:
            socket.default_value = value

    def enable_pass(self, layer, attribute, name):
        if not getattr(layer, attribute):
            setattr(layer, attribute, True)
            self.passes.append(name)


def _clear_stack(tree):
    for node in [node for node in tree.nodes if node.name.startswith(PREFIX)]:
        tree.nodes.remove(node)


def _mix(stack, label, blend, factor):
    node = stack.node("ShaderNodeMix", label)
    node.data_type = "RGBA"
    node.blend_type = blend
    node.inputs["Factor"].default_value = float(factor)
    return node


def _denoise(stack, spec, layers, scene, layer):
    node = stack.node("CompositorNodeDenoise", "Denoise")
    stack.socket(node, "Prefilter", PREFILTERS.get(str(spec.get("prefilter", "ACCURATE")).upper(), "Accurate"))
    stack.socket(node, "Quality", DENOISE_QUALITIES.get(str(spec.get("quality", "FOLLOW_SCENE")).upper(), "Follow Scene"))
    stack.socket(node, "HDR", bool(spec.get("hdr", True)))
    if spec.get("use_data_passes", True) and hasattr(layer, "cycles"):
        stack.enable_pass(layer.cycles, "denoising_store_passes", "denoising_data")
        scene.cycles.use_denoising = False
        for pass_name, socket_name in (("Denoising Albedo", "Albedo"), ("Denoising Normal", "Normal")):
            source = layers.outputs.get(pass_name)
            if source is not None and source.enabled:
                stack.tree.links.new(source, node.inputs[socket_name])
        if scene.render.engine != "CYCLES":
            stack.warnings.append("denoising data passes exist in Cycles only; the Denoise node runs without albedo and normal")
    stack.step(node, "Image", "Image", "denoise")


def _defocus(stack, spec, layers, scene, layer):
    stack.enable_pass(layer, "use_pass_z", "z")
    node = stack.node("CompositorNodeDefocus", "Defocus")
    node.scene = scene
    node.use_zbuffer = True
    node.bokeh = str(spec.get("bokeh", "CIRCLE")).upper()
    if spec.get("fstop") is not None:
        node.f_stop = float(spec["fstop"])
    if spec.get("max_blur") is not None:
        node.blur_max = float(spec["max_blur"])
    if spec.get("rotation") is not None:
        node.angle = math.radians(float(spec["rotation"]))
    if spec.get("z_scale") is not None:
        node.z_scale = float(spec["z_scale"])
    stack.tree.links.new(layers.outputs["Depth"], node.inputs["Z"])
    stack.step(node, "Image", "Image", "defocus")


def _vector_blur(stack, spec, layers, scene, layer):
    stack.enable_pass(layer, "use_pass_vector", "vector")
    stack.enable_pass(layer, "use_pass_z", "z")
    if scene.render.use_motion_blur:
        stack.warnings.append("scene motion blur is on: Cycles writes no Vector pass then; switch it off with blender_set_motion_blur for post blur")
    node = stack.node("CompositorNodeVecBlur", "Vector Blur")
    stack.socket(node, "Samples", int(spec.get("samples", 32)))
    stack.socket(node, "Shutter", float(spec.get("shutter", 0.5)))
    stack.tree.links.new(layers.outputs["Vector"], node.inputs["Speed"])
    stack.tree.links.new(layers.outputs["Depth"], node.inputs["Depth"])
    stack.step(node, "Image", "Image", "vector_blur")


def _glare(stack, spec, label="Glare", default_type="BLOOM"):
    node = stack.node("CompositorNodeGlare", label)
    kind = str(spec.get("type", default_type)).upper()
    if kind not in GLARE_TYPES:
        raise CommandError("BadRequest", f"unknown glare type '{kind}'", {"known": list(GLARE_TYPES)})
    stack.socket(node, "Type", GLARE_TYPES[kind])
    stack.socket(node, "Quality", QUALITIES.get(str(spec.get("quality", "MEDIUM")).upper(), "Medium"))
    stack.socket(node, "Threshold", spec.get("threshold"))
    stack.socket(node, "Smoothness", spec.get("smoothness"))
    stack.socket(node, "Strength", spec.get("strength"))
    stack.socket(node, "Saturation", spec.get("saturation"))
    stack.socket(node, "Tint", spec.get("tint"))
    stack.socket(node, "Size", spec.get("size"))
    stack.socket(node, "Streaks", spec.get("streaks"))
    stack.socket(node, "Streaks Angle", spec.get("angle"))
    stack.socket(node, "Iterations", spec.get("iterations"))
    stack.socket(node, "Fade", spec.get("fade"))
    return node


def _halation(stack, spec):
    """Threshold, blur, tint, add: the highlights above the threshold bleed into a red halo added to the image. Additive, because the image is scene-linear: a screen blend inverts above 1 and turns warm highlights cyan."""
    highlights = stack.node("CompositorNodeGlare", "Halation Threshold", row=1)
    stack.socket(highlights, "Type", "Bloom")
    stack.socket(highlights, "Quality", "Low")
    stack.socket(highlights, "Threshold", float(spec.get("threshold", 1.0)))
    stack.socket(highlights, "Strength", 0.0)
    stack.tree.links.new(stack.source, highlights.inputs["Image"])
    blur = stack.node("CompositorNodeBlur", "Halation Blur", column=1, row=1)
    stack.socket(blur, "Type", "Gaussian")
    size = float(spec.get("size", 6))
    stack.socket(blur, "Size", [size, size])
    stack.socket(blur, "Extend Bounds", True)
    stack.tree.links.new(highlights.outputs["Highlights"], blur.inputs["Image"])
    tinted = _mix(stack, "Halation Tint", "MULTIPLY", 1.0)
    tinted.location = (stack.x + 440, stack.y - 260)
    stack.tree.links.new(blur.outputs["Image"], tinted.inputs[6])
    tint = list(spec.get("tint", [1.0, 0.25, 0.08]))
    tinted.inputs[7].default_value = tint + [1.0] * (4 - len(tint))
    halo = _mix(stack, "Halation", "ADD", float(spec.get("strength", 0.15)))
    stack.tree.links.new(tinted.outputs[2], halo.inputs[7])
    stack.step(halo, 6, 2, "halation")


def _chromatic_aberration(stack, spec):
    node = stack.node("CompositorNodeLensdist", "Chromatic Aberration")
    stack.socket(node, "Type", "Radial")
    stack.socket(node, "Distortion", float(spec.get("distortion", 0.0)))
    stack.socket(node, "Dispersion", float(spec.get("dispersion", 0.02)))
    stack.socket(node, "Jitter", bool(spec.get("jitter", False)))
    stack.socket(node, "Fit", bool(spec.get("fit", True)))
    stack.step(node, "Image", "Image", "chromatic_aberration")


def _vignette(stack, spec):
    mask = stack.node("CompositorNodeEllipseMask", "Vignette Mask", row=1)
    roundness = float(spec.get("roundness", 1.0))
    stack.socket(mask, "Size", [0.9 * roundness, 0.9])
    blur = stack.node("CompositorNodeBlur", "Vignette Softness", column=1, row=1)
    stack.socket(blur, "Type", "Gaussian")
    softness = float(spec.get("softness", 0.5))
    stack.socket(blur, "Size", [softness, softness])
    stack.socket(blur, "Extend Bounds", True)
    stack.tree.links.new(mask.outputs["Mask"], blur.inputs["Image"])
    mix = _mix(stack, "Vignette", "MULTIPLY", spec.get("amount", 0.4))
    stack.tree.links.new(blur.outputs["Image"], mix.inputs[7])
    stack.step(mix, 6, 2, "vignette")


def _grain(stack, spec, scene):
    space = str(spec.get("space", "DISPLAY")).upper()
    display = DISPLAY_SPACES.get(scene.view_settings.view_transform, "sRGB")
    if space == "DISPLAY":
        forward = stack.node("CompositorNodeConvertColorSpace", "To Display")
        forward.from_color_space = "scene_linear"
        forward.to_color_space = display
        stack.step(forward, "Image", "Image", "grain:to_display")
    noise = stack.node("ShaderNodeTexWhiteNoise", "Grain Noise", row=1)
    noise.noise_dimensions = "4D"
    if spec.get("animated", True):
        curve = noise.inputs["W"].driver_add("default_value")
        curve.driver.expression = "frame"
    chroma = float(spec.get("chroma", 0.3))
    tone = _mix(stack, "Grain Chroma", "MIX", chroma)
    tone.location = (stack.x, stack.y - 260)
    stack.tree.links.new(noise.outputs["Value"], tone.inputs[6])
    stack.tree.links.new(noise.outputs["Color"], tone.inputs[7])
    size = float(spec.get("size", 1.0))
    grain_source = tone.outputs[2]
    if size > 1.0:
        soften = stack.node("CompositorNodeBlur", "Grain Size", column=1, row=1)
        stack.socket(soften, "Type", "Gaussian")
        stack.socket(soften, "Size", [size - 1.0, size - 1.0])
        stack.tree.links.new(grain_source, soften.inputs["Image"])
        grain_source = soften.outputs["Image"]
    centred = _mix(stack, "Grain Centre", "SUBTRACT", 1.0)
    centred.location = (stack.x + 220, stack.y - 260)
    stack.tree.links.new(grain_source, centred.inputs[6])
    centred.inputs[7].default_value = (0.5, 0.5, 0.5, 1.0)
    luminance = stack.node("CompositorNodeRGBToBW", "Grain Luminance", row=2)
    stack.tree.links.new(stack.source, luminance.inputs["Image"])
    response = spec.get("response") or {}
    highlights = float(response.get("highlights", 0.2))
    shadows = float(response.get("shadows", 0.6))
    midtones = float(response.get("midtones", 1.0))
    weight = _grain_weight(stack, luminance.outputs["Val"], shadows, midtones, highlights, float(spec.get("strength", 0.08)))
    scaled = _mix(stack, "Grain Amount", "MULTIPLY", 1.0)
    scaled.location = (stack.x + 440, stack.y - 260)
    stack.tree.links.new(centred.outputs[2], scaled.inputs[6])
    stack.tree.links.new(weight, scaled.inputs[7])
    add = _mix(stack, "Grain", "ADD", 1.0)
    stack.tree.links.new(scaled.outputs[2], add.inputs[7])
    stack.step(add, 6, 2, "grain")
    if space == "DISPLAY":
        back = stack.node("CompositorNodeConvertColorSpace", "To Linear")
        back.from_color_space = display
        back.to_color_space = "scene_linear"
        stack.step(back, "Image", "Image", "grain:to_linear")


MATTE_LAYERS = {"object": "CryptoObject", "material": "CryptoMaterial", "asset": "CryptoAsset"}


@command("cryptomatte_matte")
def cryptomatte_matte(params):
    """A Cryptomatte node picking the named objects, materials or assets, its matte optionally written by a File Output slot."""
    scene = _scene(params)
    layer = bpy.context.view_layer if scene == bpy.context.scene else scene.view_layers[0]
    kind = (params.get("layer") or "object").lower()
    if kind not in MATTE_LAYERS:
        raise CommandError("BadRequest", "layer must be object, material or asset")
    names = list(params.get("names") or [])
    if not names:
        raise CommandError("BadRequest", "names is required: the objects, materials or assets to matte")
    setattr(layer, f"use_pass_cryptomatte_{kind}", True)
    tree = _compositor_tree()
    node_name = params.get("name") or f"Matte {', '.join(names)}"[:60]
    node = tree.nodes.get(node_name)
    if node is None or node.bl_idname != "CompositorNodeCryptomatteV2":
        node = tree.nodes.new("CompositorNodeCryptomatteV2")
        node.name = node_name
        node.label = node_name
        node.location = (0, -600)
    node.source = "RENDER"
    node.scene = scene
    node.layer_name = f"{layer.name}.{MATTE_LAYERS[kind]}"
    node.matte_id = ",".join(names)
    layers = _render_layers(tree)
    tree.links.new(layers.outputs["Image"], node.inputs["Image"])
    written = None
    if params.get("output"):
        output = _file_output_node(tree, params["output"], True)
        if params.get("directory"):
            os.makedirs(params["directory"], exist_ok=True)
            if hasattr(output, "directory"):
                output.directory = params["directory"] if params["directory"].endswith(os.sep) else params["directory"] + os.sep
            else:
                output.base_path = params["directory"]
        slot_name = params.get("slot") or "Matte"
        target = next((socket for socket in _slot_inputs(output) if socket.name == slot_name), None) or _new_slot(output, slot_name, node.outputs["Matte"])
        tree.links.new(node.outputs["Matte"], target)
        written = {"node": output.name, "slot": slot_name}
    scene.render.use_compositing = True
    return {"node": node.name, "layer": node.layer_name, "matte_id": node.matte_id, "outputs": [socket.name for socket in node.outputs], "file_output": written}


def _math(stack, label, operation, column, row, first=None, second=None, third=None):
    node = stack.node("ShaderNodeMath", label, column=column, row=row)
    node.operation = operation
    for index, value in enumerate((first, second, third)):
        if value is None:
            continue
        if hasattr(value, "is_output"):
            stack.tree.links.new(value, node.inputs[index])
        else:
            node.inputs[index].default_value = float(value)
    return node


def _grain_weight(stack, luminance, shadows, midtones, highlights, strength):
    """A quadratic through three points: the weight is shadows at black, midtones at mid grey, highlights at white, times the strength, never below zero."""
    inverse = _math(stack, "Grain 1-L", "SUBTRACT", 1, 2, 1.0, luminance)
    bell = _math(stack, "Grain Bell", "MULTIPLY", 2, 2, luminance, inverse.outputs[0])
    mid = _math(stack, "Grain Mid", "MULTIPLY", 3, 2, bell.outputs[0], 4.0 * midtones)
    twice = _math(stack, "Grain 2L", "MULTIPLY", 1, 3, luminance, 2.0)
    below_half = _math(stack, "Grain 1-2L", "SUBTRACT", 2, 3, 1.0, twice.outputs[0])
    dark_shape = _math(stack, "Grain Dark Shape", "MULTIPLY", 3, 3, inverse.outputs[0], below_half.outputs[0])
    dark = _math(stack, "Grain Dark", "MULTIPLY", 4, 3, dark_shape.outputs[0], shadows)
    above_half = _math(stack, "Grain 2L-1", "SUBTRACT", 2, 4, twice.outputs[0], 1.0)
    bright_shape = _math(stack, "Grain Bright Shape", "MULTIPLY", 3, 4, luminance, above_half.outputs[0])
    bright = _math(stack, "Grain Bright", "MULTIPLY", 4, 4, bright_shape.outputs[0], highlights)
    total = _math(stack, "Grain Weight", "ADD", 5, 3, mid.outputs[0], dark.outputs[0])
    weighted = _math(stack, "Grain Weight Sum", "ADD", 5, 4, total.outputs[0], bright.outputs[0])
    positive = _math(stack, "Grain Clamp", "MAXIMUM", 6, 4, weighted.outputs[0], 0.0)
    scaled = _math(stack, "Grain Strength", "MULTIPLY", 6, 3, positive.outputs[0], strength)
    return scaled.outputs[0]


def _enabled(spec):
    return isinstance(spec, dict) and spec.get("enabled", True)


@command("lens_effects")
def lens_effects(params):
    """Builds the post stack, or takes it down when asked to in so many words.

    Asked for nothing it used to take the stack down and answer cleared: true, so a call made to see what the compositor held
    emptied it instead, and what had been wired by hand could not be put back. Nothing is touched before this refusal.
    """
    blocks = {name: params.get(name) for name in ORDER}
    if not any(_enabled(spec) for spec in blocks.values()) and not params.get("clear"):
        raise CommandError(
            "BadRequest",
            "lens_effects builds the compositor stack: name at least one effect, or pass clear to take the stack down. To see the chain that is "
            "there, read it with the compositor command and action info",
            {"effects": list(ORDER)})
    scene = _scene(params)
    layer = bpy.context.view_layer if scene == bpy.context.scene else scene.view_layers[0]
    tree = _compositor_tree()
    _clear_stack(tree)
    layers = _render_layers(tree)
    layers.location = (-400, 0)
    output = _output(tree)
    stack = Stack(tree, layers.outputs["Image"], 0, 0)
    if params.get("clear"):
        for link in [link for link in tree.links if link.to_socket == output.inputs[0]]:
            tree.links.remove(link)
        tree.links.new(layers.outputs["Image"], output.inputs[0])
        return {"chain": [], "warnings": [], "passes_enabled": [], "cleared": True}
    if _enabled(blocks["denoise"]):
        _denoise(stack, blocks["denoise"], layers, scene, layer)
    if _enabled(blocks["defocus"]):
        _defocus(stack, blocks["defocus"], layers, scene, layer)
    if _enabled(blocks["vector_blur"]):
        _vector_blur(stack, blocks["vector_blur"], layers, scene, layer)
    if _enabled(blocks["glare"]):
        stack.step(_glare(stack, blocks["glare"]), "Image", "Image", "glare")
    if _enabled(blocks["halation"]):
        _halation(stack, blocks["halation"])
    if _enabled(blocks["chromatic_aberration"]):
        _chromatic_aberration(stack, blocks["chromatic_aberration"])
    if _enabled(blocks["vignette"]):
        _vignette(stack, blocks["vignette"])
    if _enabled(blocks["grain"]):
        _grain(stack, blocks["grain"], scene)
        if not _enabled(blocks["denoise"]) and not scene.cycles.use_denoising and scene.render.engine == "CYCLES":
            stack.warnings.append("grain on top of render noise: enable denoise in this stack or in blender_set_cycles first")
    for link in [link for link in tree.links if link.to_socket == output.inputs[0]]:
        tree.links.remove(link)
    tree.links.new(stack.source, output.inputs[0])
    output.location = (stack.x + 200, 0)
    scene.render.use_compositing = True
    return {
        "chain": stack.chain,
        "warnings": stack.warnings,
        "passes_enabled": stack.passes,
        "nodes": [node.name for node in tree.nodes if node.name.startswith(PREFIX)],
        "view_transform": scene.view_settings.view_transform,
    }
