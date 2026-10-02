"""The physical camera: sensor, focal length, lens shift, aperture geometry, focus; per-object motion blur and the shutter curve."""

import math

from .cameras import _camera_of
from .core import _object, _scene, _set_enum, command
from .server import CommandError

CIRCLE_OF_CONFUSION_FRACTION = 1500.0


def _apply_curve_points(curve, points):
    while len(curve.points) > 2:
        curve.points.remove(curve.points[1])
    if len(points) < 2:
        raise CommandError("BadRequest", "a curve needs at least two [x, y] points")
    curve.points[0].location = tuple(points[0])
    curve.points[-1].location = tuple(points[-1])
    for point in points[1:-1]:
        curve.points.new(float(point[0]), float(point[1]))


def _hyperfocal_m(camera):
    circle = camera.sensor_width / CIRCLE_OF_CONFUSION_FRACTION
    fstop = max(0.1, camera.dof.aperture_fstop)
    return round((camera.lens ** 2 / (fstop * circle) + camera.lens) / 1000.0, 2)


def _optics_summary(item, scene):
    camera = item.data
    dof = camera.dof
    return {
        "camera": item.name,
        "type": camera.type,
        "panorama": camera.panorama_type if camera.type == "PANO" else None,
        "lens_mm": camera.lens,
        "fov_deg": round(math.degrees(camera.angle), 2),
        "sensor": {"width": camera.sensor_width, "height": camera.sensor_height, "fit": camera.sensor_fit},
        "shift": [camera.shift_x, camera.shift_y],
        "clip": [camera.clip_start, camera.clip_end],
        "ortho_scale": camera.ortho_scale,
        "aperture": {"enabled": dof.use_dof, "fstop": dof.aperture_fstop, "blades": dof.aperture_blades, "rotation": round(math.degrees(dof.aperture_rotation), 2), "ratio": dof.aperture_ratio},
        "focus": {"distance": dof.focus_distance, "object": dof.focus_object.name if dof.focus_object else None, "bone": dof.focus_subtarget or None},
        "hyperfocal_m": _hyperfocal_m(camera),
        "shutter_curve": [[round(point.location[0], 4), round(point.location[1], 4)] for point in scene.render.motion_blur_shutter_curve.curves[0].points],
        "object_motion_blur": {item.name: {"enabled": item.cycles.use_motion_blur, "steps": item.cycles.motion_steps, "deform": item.cycles.use_deform_motion}
                               for item in scene.objects if (hasattr(item, "cycles") and not item.cycles.use_motion_blur) or (hasattr(item, "cycles") and item.cycles.motion_steps > 1)},
    }


@command("set_camera_optics")
def set_camera_optics(params):
    scene = _scene(params)
    item = _camera_of(params)
    camera = item.data
    sensor = params.get("sensor")
    if isinstance(sensor, dict):
        if sensor.get("width") is not None:
            camera.sensor_width = float(sensor["width"])
        if sensor.get("height") is not None:
            camera.sensor_height = float(sensor["height"])
        if sensor.get("fit"):
            _set_enum(camera, "sensor_fit", sensor["fit"].upper(), "sensor fit")
    if params.get("type"):
        _set_enum(camera, "type", params["type"].upper(), "camera type")
    if params.get("panorama"):
        camera.type = "PANO"
        _set_enum(camera, "panorama_type", params["panorama"].upper(), "panorama type")
    if params.get("lens") is not None:
        camera.lens_unit = "MILLIMETERS"
        camera.lens = float(params["lens"])
    elif params.get("fov") is not None:
        camera.angle = math.radians(float(params["fov"]))
    if params.get("ortho_scale") is not None:
        camera.ortho_scale = float(params["ortho_scale"])
    if params.get("shift") is not None:
        camera.shift_x, camera.shift_y = (float(value) for value in params["shift"])
    if params.get("clip") is not None:
        camera.clip_start, camera.clip_end = (float(value) for value in params["clip"])
    aperture = params.get("aperture")
    if isinstance(aperture, dict):
        dof = camera.dof
        dof.use_dof = bool(aperture.get("enabled", True))
        if aperture.get("fstop") is not None:
            dof.aperture_fstop = max(0.1, float(aperture["fstop"]))
        if aperture.get("blades") is not None:
            blades = int(aperture["blades"])
            if blades not in (0,) and not 3 <= blades <= 16:
                raise CommandError("BadRequest", "aperture blades are 0 (round) or 3 to 16")
            dof.aperture_blades = blades
        if aperture.get("rotation") is not None:
            dof.aperture_rotation = math.radians(float(aperture["rotation"]))
        if aperture.get("ratio") is not None:
            dof.aperture_ratio = max(0.01, float(aperture["ratio"]))
    focus = params.get("focus")
    if isinstance(focus, dict):
        dof = camera.dof
        if focus.get("distance") is not None:
            dof.focus_object = None
            dof.focus_distance = float(focus["distance"])
        if focus.get("object"):
            dof.focus_object = _object(focus["object"])
        if focus.get("bone") is not None:
            dof.focus_subtarget = focus["bone"] or ""
    blur = params.get("object_motion_blur")
    if isinstance(blur, dict):
        for name, spec in blur.items():
            target = _object(name)
            if not hasattr(target, "cycles"):
                raise CommandError("Unsupported", "per-object motion blur needs the Cycles add-on")
            if spec.get("enabled") is not None:
                target.cycles.use_motion_blur = bool(spec["enabled"])
            if spec.get("steps") is not None:
                target.cycles.motion_steps = max(1, min(7, int(spec["steps"])))
            if spec.get("deform") is not None:
                target.cycles.use_deform_motion = bool(spec["deform"])
    if params.get("shutter_curve") is not None:
        mapping = scene.render.motion_blur_shutter_curve
        _apply_curve_points(mapping.curves[0], params["shutter_curve"])
        mapping.update()
    return _optics_summary(item, scene)
