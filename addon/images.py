"""Pictures the model can look at: a small preview of an image file with its exposure statistics."""

import base64
import os
import tempfile

import bpy

from .core import _require, command
from .server import CommandError

PREVIEW_EDGE = 512


HISTOGRAM_BINS = 8


def _luminance_stats(image):
    pixels = image.pixels[:]
    channels = image.channels
    count = len(pixels) // channels
    if count == 0:
        return None
    histogram = [0] * HISTOGRAM_BINS
    total = 0.0
    clipped_high = 0
    clipped_low = 0
    for index in range(0, len(pixels), channels):
        red, green, blue = pixels[index], pixels[index + 1] if channels > 1 else pixels[index], pixels[index + 2] if channels > 2 else pixels[index]
        luminance = 0.2126 * red + 0.7152 * green + 0.0722 * blue
        total += luminance
        if luminance >= 0.99:
            clipped_high += 1
        elif luminance <= 0.004:
            clipped_low += 1
        histogram[min(HISTOGRAM_BINS - 1, int(max(0.0, luminance) * HISTOGRAM_BINS))] += 1
    return {
        "mean_luminance": round(total / count, 4),
        "clipped_high": round(clipped_high / count, 4),
        "clipped_low": round(clipped_low / count, 4),
        "histogram": [round(bin_count / count, 4) for bin_count in histogram],
        "sampled_pixels": count,
    }


def _preview(path, max_edge=PREVIEW_EDGE, with_stats=True):
    """A JPEG small enough to travel in a reply, plus exposure statistics from the same pixels; the file on disk stays untouched."""
    image = bpy.data.images.load(path, check_existing=False)
    try:
        width, height = image.size
        if width == 0 or height == 0:
            raise CommandError("RenderFailed", f"'{path}' holds no pixels")
        scale = min(1.0, max_edge / max(width, height))
        if scale < 1.0:
            image.scale(max(1, int(width * scale)), max(1, int(height * scale)))
        stats = _luminance_stats(image) if with_stats else None
        target = os.path.join(tempfile.gettempdir(), f"snail-preview-{os.getpid()}.jpg")
        image.filepath_raw = target
        image.file_format = "JPEG"
        image.save()
        with open(target, "rb") as handle:
            encoded = base64.b64encode(handle.read()).decode("ascii")
        os.remove(target)
        return {"mime": "image/jpeg", "base64": encoded, "width": image.size[0], "height": image.size[1], "source": [width, height]}, stats
    finally:
        bpy.data.images.remove(image)


def _with_preview(result, path, params):
    if not params.get("preview", True):
        return result
    try:
        preview, stats = _preview(path, int(params.get("preview_size") or PREVIEW_EDGE))
    except (RuntimeError, CommandError) as error:
        result["preview_error"] = str(error)
        return result
    result["preview"] = preview
    result["stats"] = stats
    return result


@command("inspect_image")
def inspect_image(params):
    path = _require(params, "path")
    if not os.path.isfile(path):
        raise CommandError("NotFound", f"no image at '{path}'")
    preview, stats = _preview(path, int(params.get("preview_size") or PREVIEW_EDGE))
    result = {"path": path, "bytes": os.path.getsize(path), "size": preview["source"], "stats": stats}
    if params.get("preview", True):
        result["preview"] = preview
    return result
