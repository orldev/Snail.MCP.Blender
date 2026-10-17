"""How much memory this Blender may actually use: what the machine has, held down to what a cgroup allows it.

Touches nothing of Blender, so it is read by a test of its own without a Blender to run in. It exists apart from budget.py for
that reason: the number the budget is built on was wrong for a year inside containers, and a wrong number there is a render
the verdict calls "fits" and the OOM reaper ends.
"""

import os

CGROUP_LIMITS = (("/sys/fs/cgroup/memory.max", "max"), ("/sys/fs/cgroup/memory/memory.limit_in_bytes", None))
NO_LIMIT_BYTES = 1 << 62


def cgroup_memory_mb(limits=CGROUP_LIMITS):
    """The memory a cgroup allows this process, or None when none caps it.

    Both layouts are read, v2 first. "max" of v2 and the huge sentinel v1 writes both mean "no limit" rather than a limit of that
    size, and a value neither layout wrote — an empty file, a word — is no limit either.
    """
    for path, unlimited in limits:
        try:
            with open(path, encoding="utf-8") as limit:
                written = limit.read().strip()
        except OSError:
            continue

        if written == unlimited or not written.isdigit():
            continue

        allowed = int(written)

        if 0 < allowed < NO_LIMIT_BYTES:
            return allowed // 1024 ** 2

    return None


def physical_memory_mb():
    """What the machine reports it has, or None where the platform does not say."""
    try:
        return os.sysconf("SC_PHYS_PAGES") * os.sysconf("SC_PAGE_SIZE") // 1024 ** 2
    except (ValueError, OSError, AttributeError):
        return None


def available_memory_mb():
    """The lower of the two: the machine's memory is not this Blender's when a container holds it to less.

    sysconf reports the memory of the machine, and inside a container that is the host's or the virtual machine's. On the
    deployment this was found on it answered 32054 MB against a cgroup limit of 16 GiB — 1.6 times what Blender could have.
    """
    physical = physical_memory_mb()
    allowed = cgroup_memory_mb()

    if physical is None:
        return allowed

    return physical if allowed is None else min(physical, allowed)
