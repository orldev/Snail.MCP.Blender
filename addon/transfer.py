"""Files between the machine the MCP server runs on and the one Blender runs on, in chunks small enough for one line each way.

Answered from the socket thread, like ping, because none of it touches bpy: a transfer never waits for a render and never
holds one up. A relative path lands under the add-on's own data directory, in files/, and cannot climb out of it; an
absolute path is taken as given, on the machine Blender runs on. A request naming an area is held inside that area of the
data directory and refuses absolute paths: that is how the HTTP server's pages and links reach the volume.
"""

import base64
import binascii
import hashlib
import os

from . import state
from .core import immediate_command
from .server import CommandError

MAX_CHUNK_BYTES = 2 * 1024 * 1024
LIST_LIMIT = 5000
PARTIAL_SUFFIX = ".part"


AREAS = ("files", "jobs", "batches", "snapshots")


def files_root():
    return area_root("files")


def area_root(area):
    if area not in AREAS:
        raise CommandError("BadRequest", f"'area' must be one of {', '.join(AREAS)}", {"known": list(AREAS)})
    return os.path.join(state.data_directory(), area)


def inside(area, relative):
    """A path held inside one area: relative, in either slash, never climbing out; an empty path is the area itself.

    A segment that is "." or ".." once trimmed is refused outright rather than resolved: the path is trimmed as a whole first, so
    "x/.. " would otherwise name the area itself, and the server, which applies the same rule, never sends one.
    """
    relative = (relative or "").strip()
    if os.path.isabs(relative) or relative.startswith(("~", "/", "\\")) or ":" in relative or "\0" in relative:
        raise CommandError("BadRequest", f"'{relative}' must be relative to the {area} area")
    if any(segment.strip() in (".", "..") for segment in relative.replace("\\", "/").split("/")):
        raise CommandError("BadRequest", f"'{relative}' names a folder by '.' or '..'; name it by its path inside the {area} area")
    root = area_root(area)
    resolved = os.path.normpath(os.path.join(root, relative.replace("\\", "/")))
    if os.path.commonpath([root, resolved]) != root:
        raise CommandError("BadRequest", f"'{relative}' climbs out of the {area} area")
    return resolved


def _resolve(path, area=None):
    if not isinstance(path, str) or not path.strip():
        raise CommandError("BadRequest", "'path' is required")
    if area is not None:
        return inside(area, path)
    path = os.path.expanduser(path.strip())
    if os.path.isabs(path):
        return os.path.normpath(path)
    root = files_root()
    resolved = os.path.normpath(os.path.join(root, path))
    if os.path.commonpath([root, resolved]) != root:
        raise CommandError("BadRequest", f"'{path}' climbs out of the add-on's files directory; pass an absolute path instead")
    return resolved


def _digest(path):
    digest = hashlib.sha256()
    with open(path, "rb") as handle:
        while block := handle.read(1024 * 1024):
            digest.update(block)
    return digest.hexdigest()


@immediate_command("file_put")
def file_put(bridge, params):
    """One chunk of a file on its way in: the first creates the partial file, each next one must start where the last ended,
    and the one marked done is checked against the sender's digest before it takes the file's name."""
    target = _resolve(params.get("path"), params.get("area"))
    offset = int(params.get("offset") or 0)
    try:
        data = base64.b64decode(params.get("data") or "", validate=True)
    except binascii.Error as error:
        raise CommandError("BadRequest", f"'data' is not base64: {error}") from error
    partial = target + PARTIAL_SUFFIX
    if offset == 0:
        if os.path.exists(target) and not params.get("overwrite"):
            raise CommandError("Exists", f"'{target}' already exists; pass overwrite to replace it", {"path": target})
        os.makedirs(os.path.dirname(target) or ".", exist_ok=True)
        mode = "wb"
    else:
        arrived = os.path.getsize(partial) if os.path.exists(partial) else 0
        if arrived != offset:
            raise CommandError("BadRequest", f"a chunk at {offset} does not follow the {arrived} bytes that arrived", {"received": arrived})
        mode = "ab"
    with open(partial, mode) as handle:
        handle.write(data)
    received = offset + len(data)
    if not params.get("done"):
        return {"path": target, "received": received}
    digest = _digest(partial)
    expected = params.get("sha256")
    if expected and expected != digest:
        os.remove(partial)
        raise CommandError("Corrupt", "the file that arrived is not the file that was sent", {"expected": expected, "received": digest})
    os.replace(partial, target)
    return {"path": target, "size": received, "sha256": digest}


@immediate_command("file_get")
def file_get(bridge, params):
    """One chunk of a file on its way out; the last one carries the digest of the whole file, so the receiver can check what it assembled."""
    source = _resolve(params.get("path"), params.get("area"))
    if not os.path.isfile(source):
        raise CommandError("NotFound", f"no file at '{source}'")
    size = os.path.getsize(source)
    offset = max(0, int(params.get("offset") or 0))
    length = max(1, min(MAX_CHUNK_BYTES, int(params.get("length") or MAX_CHUNK_BYTES)))
    with open(source, "rb") as handle:
        handle.seek(offset)
        data = handle.read(length)
    reply = {"path": source, "offset": offset, "size": size, "data": base64.b64encode(data).decode("ascii"), "eof": offset + len(data) >= size}
    if reply["eof"]:
        reply["sha256"] = _digest(source)
    return reply


@immediate_command("file_list")
def file_list(bridge, params):
    """A page of the files under a directory, with paths relative to it in forward slashes; a file answers for itself.

    A folder of a hundred thousand frames does not fit in one reply, and a reply that simply stopped at five thousand made the rest
    unreachable: a download refused rather than fetched them. 'offset' and 'limit' ask for the next page, 'total' says how many there are
    in all and 'truncated' whether this page is the last.
    """
    root = _resolve(params.get("path"), params.get("area"))
    if os.path.isfile(root):
        return {"root": os.path.dirname(root), "kind": "file", "files": [_entry(os.path.dirname(root), root)], "total_bytes": os.path.getsize(root), "total": 1, "offset": 0, "truncated": False}
    if not os.path.isdir(root):
        raise CommandError("NotFound", f"no file or directory at '{root}'")
    offset = max(0, int(params.get("offset") or 0))
    limit = max(1, min(LIST_LIMIT, int(params.get("limit") or LIST_LIMIT)))
    files = sorted(
        (_entry(root, os.path.join(directory, name)) for directory, _, names in os.walk(root) for name in names if not name.endswith(PARTIAL_SUFFIX)),
        key=lambda entry: entry["path"],
    )
    page = files[offset : offset + limit]
    return {
        "root": root,
        "kind": "directory",
        "files": page,
        "total_bytes": sum(entry["size"] for entry in page),
        "total": len(files),
        "offset": offset,
        "truncated": offset + len(page) < len(files),
    }


def _entry(root, path):
    return {"path": os.path.relpath(path, root).replace(os.sep, "/"), "size": os.path.getsize(path), "modified": round(os.path.getmtime(path))}
