"""What a frame will cost before it is rendered: textures, geometry, particles, devices and system memory, a timed probe; limits and farm packing."""

import os
import tempfile
import time

import bpy

from .core import _scene, _set_enum, command
from .rendering import _timed_render
from .server import CommandError

PROBE_PERCENTAGE = 10
BYTES_PER_POLYGON = 120
VERDICT_TIGHT = 0.6
VERDICT_EXCEEDS = 0.9


def _texture_budget():
    entries = []
    for image in bpy.data.images:
        if not image.users or image.size[0] == 0:
            continue
        bytes_per_channel = 4 if image.is_float else 1
        megabytes = image.size[0] * image.size[1] * max(1, image.channels) * bytes_per_channel / 1024 ** 2
        entries.append({"name": image.name, "size": [image.size[0], image.size[1]], "float": image.is_float, "mb": round(megabytes, 1), "packed": image.packed_file is not None})
    entries.sort(key=lambda entry: entry["mb"], reverse=True)
    return {"count": len(entries), "mb": round(sum(entry["mb"] for entry in entries), 1), "largest": entries[:8]}


def _geometry_budget(scene):
    depsgraph = bpy.context.evaluated_depsgraph_get()
    polygons = 0
    instances = 0
    objects = 0
    volumes = 0
    for instance in depsgraph.object_instances:
        item = instance.object
        if item.type == "MESH":
            polygons += len(item.data.polygons)
            instances += 1 if instance.is_instance else 0
            objects += 0 if instance.is_instance else 1
        elif item.type == "VOLUME":
            volumes += 1
    particles = sum(system.settings.count for item in scene.objects for system in item.particle_systems)
    hair = sum(system.settings.count * (system.settings.rendered_child_count + 1) for item in scene.objects for system in item.particle_systems if system.settings.type == "HAIR")
    return {"polygons_render": polygons, "objects": objects, "instances": instances, "particles": particles, "hair_strands": hair, "volumes": volumes,
            "mb": round(polygons * BYTES_PER_POLYGON / 1024 ** 2, 1)}


def _estimate(scene, textures, geometry, probe):
    """What the frame will need, from the measurement when there is one and from the parts when there is not.

    The peak a probe reports is Blender's own measurement of that render: the textures are in it, the geometry is in it, and so is a frame
    buffer of the probe's size. Adding the textures and the geometry to it counted them twice and called scenes too large for a machine
    they fit on. What the probe cannot know is the buffer of the full frame, so that difference is added — four channels of float, which is
    the combined pass and a floor rather than a promise. The parts are still the answer when they come to more than the measurement, and
    the whole of it when no probe ran — or when the render reported no memory at all, which some builds and engines do not.
    """
    parts = round(textures["mb"] + geometry["mb"], 1)
    if probe is None or probe["peak_memory_mb"] <= 0:
        return parts, "textures and geometry"
    measured = round(probe["peak_memory_mb"] + _buffer_growth_mb(scene, probe["percentage"]), 1)
    return (measured, "the probe, plus the frame buffer it did not fill") if measured >= parts else (parts, "textures and geometry")


def _buffer_growth_mb(scene, percentage):
    """How much more frame buffer the whole frame needs than the probe's did."""
    width = scene.render.resolution_x
    height = scene.render.resolution_y
    pixels = width * height
    probed = int(width * percentage / 100.0) * int(height * percentage / 100.0)
    return max(0, pixels - probed) * 4 * 4 / 1024 ** 2


def _devices():
    preferences = bpy.context.preferences.addons.get("cycles")
    if preferences is None:
        return {"backend": "NONE", "devices": []}
    cycles = preferences.preferences
    cycles.get_devices()
    return {"backend": cycles.compute_device_type, "devices": [{"name": device.name, "type": device.type, "use": device.use} for device in cycles.devices]}


def _system_memory_mb():
    try:
        return os.sysconf("SC_PHYS_PAGES") * os.sysconf("SC_PAGE_SIZE") // 1024 ** 2
    except (ValueError, OSError, AttributeError):
        return None


def _remove_probes(probes):
    """Deletes what the probe wrote, under the name Blender gave it: a still render adds the frame number and extension to its path, so the
    path alone names nothing, and the scene's own output path, restored by then, must never be what is deleted."""
    for probe in probes:
        if os.path.isfile(probe):
            os.remove(probe)


def _probe(scene, spec):
    """Renders the frame small, twice, and reads the two costs of a frame out of the pair.

    A frame costs a fixed part and a part that grows with the pixels: synchronising the scene, building the acceleration structure and
    loading textures happen once whatever the resolution, while the tracing itself scales with area. One small render scaled by the square
    of the ratio treats all of it as the second kind, which under-reports every frame and most of all the ones whose scene is heavy and
    whose image is small. Two renders at different sizes separate the two: the slope between them is the cost per pixel, and what is left
    at zero pixels is the fixed cost.

    The second render is the cheaper of the pair, so the pair costs about a quarter more than the single one did. At one percent there is
    nothing smaller to measure, and the answer falls back to scaling alone, which the reply says.
    """
    render = scene.render
    percentage = max(1, min(50, int(spec.get("percentage") or PROBE_PERCENTAGE)))
    smaller = max(1, percentage // 2)
    previous = (render.resolution_percentage, render.filepath, scene.cycles.samples, scene.eevee.taa_render_samples, render.use_compositing)
    if spec.get("samples") is not None:
        scene.cycles.samples = int(spec["samples"])
        scene.eevee.taa_render_samples = int(spec["samples"])
    render.filepath = os.path.join(tempfile.gettempdir(), f"snail-probe-{int(time.time())}.png")
    probes = {render.filepath, render.frame_path(frame=scene.frame_current)}
    runs = []
    try:
        for at in ([smaller, percentage] if smaller < percentage else [percentage]):
            render.resolution_percentage = at
            timing = _timed_render(write_still=True, scene=scene.name)
            runs.append({"percentage": at, "seconds": round(timing["duration_ms"] / 1000.0, 2), "peak_memory_mb": timing["peak_memory_mb"], "stats": timing["stats"]})
    finally:
        render.resolution_percentage, render.filepath, scene.cycles.samples, scene.eevee.taa_render_samples, render.use_compositing = previous
        _remove_probes(probes)
    return _read_the_runs(runs)


def _read_the_runs(runs):
    """The two costs of a frame, from one measurement or from two."""
    last = runs[-1]
    fixed, per_frame = _split(runs)
    return {
        "percentage": last["percentage"],
        "seconds": last["seconds"],
        "peak_memory_mb": max(run["peak_memory_mb"] for run in runs),
        "fixed_seconds": round(fixed, 2),
        "seconds_per_frame_estimate": round(per_frame, 1),
        "method": "two renders" if len(runs) > 1 else "one render scaled by area",
        "runs": [{"percentage": run["percentage"], "seconds": run["seconds"], "peak_memory_mb": run["peak_memory_mb"]} for run in runs],
        "stats": last["stats"],
    }


def _split(runs):
    """Fixed seconds and the estimate for a whole frame: the slope between two renders, or the square of the ratio when there is only one.

    A pair too close to tell apart — a scene so light that noise is larger than the difference — gives a slope of zero or less, and the
    honest answer there is the one a single render gives, with no fixed part claimed.
    """
    last = runs[-1]
    whole = _area(100)
    if len(runs) > 1:
        first = runs[0]
        rate = (last["seconds"] - first["seconds"]) / (_area(last["percentage"]) - _area(first["percentage"]))
        if rate > 0:
            fixed = max(0.0, last["seconds"] - rate * _area(last["percentage"]))
            return fixed, fixed + rate * whole
    return 0.0, last["seconds"] * (whole / _area(last["percentage"]))


def _area(percentage):
    return (percentage / 100.0) ** 2


def _apply(scene, spec):
    applied = {}
    render = scene.render
    cycles = scene.cycles
    if spec.get("texture_limit"):
        _set_enum(cycles, "texture_limit_render", str(spec["texture_limit"]).upper(), "texture limit")
        applied["texture_limit"] = cycles.texture_limit_render
    simplify = spec.get("simplify")
    if isinstance(simplify, dict):
        render.use_simplify = bool(simplify.get("enabled", True))
        if simplify.get("subdivision") is not None:
            render.simplify_subdivision_render = int(simplify["subdivision"])
        if simplify.get("child_particles") is not None:
            render.simplify_child_particles_render = float(simplify["child_particles"])
        if simplify.get("volumes") is not None:
            render.simplify_volumes = float(simplify["volumes"])
        applied["simplify"] = {"enabled": render.use_simplify, "subdivision": render.simplify_subdivision_render, "child_particles": render.simplify_child_particles_render, "volumes": render.simplify_volumes}
    if spec.get("tile_size") is not None:
        cycles.use_auto_tile = True
        cycles.tile_size = int(spec["tile_size"])
        applied["tile_size"] = cycles.tile_size
    if spec.get("device"):
        wanted = str(spec["device"])
        if wanted.upper() == "CPU":
            cycles.device = "CPU"
        else:
            preferences = bpy.context.preferences.addons.get("cycles")
            if preferences:
                preferences.preferences.get_devices()
            devices = list(preferences.preferences.devices) if preferences else []
            match = [device for device in devices if device.name == wanted or device.type == wanted.upper()]
            if not match:
                raise CommandError("NotFound", f"no Cycles device '{wanted}'", {"known": [device.name for device in devices]})
            for device in devices:
                device.use = device in match or device.type == "CPU"
            cycles.device = "GPU"
        applied["device"] = cycles.device
    return applied


def _pack(spec):
    result = {}
    if spec.get("pack_external"):
        bpy.ops.file.pack_all()
        result["packed"] = True
    if spec.get("relative_paths"):
        if not bpy.data.filepath:
            raise CommandError("BadRequest", "relative paths need a saved file; blender_save_file first")
        bpy.ops.file.make_paths_relative()
        result["relative_paths"] = True
    if spec.get("copy_to"):
        path = spec["copy_to"]
        directory = os.path.dirname(path)
        if directory:
            os.makedirs(directory, exist_ok=True)
        bpy.ops.wm.save_as_mainfile(filepath=path, copy=True, relative_remap=True)
        result["copy"] = path
        result["bytes"] = os.path.getsize(path)
    return result


@command("render_budget")
def render_budget(params):
    scene = _scene(params)
    applied = _apply(scene, params["apply"]) if isinstance(params.get("apply"), dict) else {}
    textures = _texture_budget()
    geometry = _geometry_budget(scene)
    devices = _devices()
    system_mb = _system_memory_mb()
    probe = _probe(scene, params["probe"]) if isinstance(params.get("probe"), dict) and params["probe"].get("enabled", True) else None
    estimate_mb, estimate_from = _estimate(scene, textures, geometry, probe)
    limit_mb = params.get("memory_limit_mb") or (int(system_mb * 0.8) if system_mb else None)
    ratio = estimate_mb / limit_mb if limit_mb else None
    verdict = "unknown" if ratio is None else ("exceeds" if ratio > VERDICT_EXCEEDS else "tight" if ratio > VERDICT_TIGHT else "fits")
    recommendations = []
    if textures["mb"] > 2048:
        recommendations.append({"apply": {"texture_limit": "2048"}, "why": f"textures take {textures['mb']} MB; capping them at 2048 px keeps most of the detail"})
    if geometry["polygons_render"] > 20_000_000:
        recommendations.append({"apply": {"simplify": {"enabled": True, "subdivision": 2}}, "why": f"{geometry['polygons_render']} polygons after modifiers; simplify caps subdivision"})
    if geometry["hair_strands"] > 2_000_000:
        recommendations.append({"apply": {"simplify": {"enabled": True, "child_particles": 0.5}}, "why": f"{geometry['hair_strands']} hair strands; halving children is rarely visible"})
    if scene.render.engine == "CYCLES" and scene.cycles.device == "GPU" and devices["backend"] == "NONE":
        recommendations.append({"apply": {"device": "CPU"}, "why": "no GPU backend is enabled; Cycles will fall back to CPU anyway"})
    if verdict == "exceeds":
        recommendations.append({"apply": {"tile_size": 1024}, "why": "smaller tiles lower peak memory of the final render"})
    packed = _pack(params["pack_for_farm"]) if isinstance(params.get("pack_for_farm"), dict) and params["pack_for_farm"].get("enabled", True) else None
    return {
        "scene": scene.name,
        "engine": scene.render.engine,
        "textures": textures,
        "geometry": geometry,
        "devices": devices,
        "system_memory_mb": system_mb,
        "probe": probe,
        "estimate_mb": round(estimate_mb, 1),
        "estimate_from": estimate_from,
        "memory_limit_mb": limit_mb,
        "verdict": verdict,
        "recommendations": recommendations,
        "applied": applied,
        "packed": packed,
    }
