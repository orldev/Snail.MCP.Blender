"""The memory the render budget is built on, read without a Blender.

The rest of the add-on is tested through the server against a real Blender; this one function is pure, it was wrong inside every
container for as long as it existed, and a test of it costs a second rather than forty minutes.
"""

import importlib.util
import pathlib

SOURCE = pathlib.Path(__file__).resolve().parents[2] / "addon" / "memory.py"


def _module():
    """The add-on's memory module loaded by path, so nothing of Blender has to be importable."""
    spec = importlib.util.spec_from_file_location("snail_addon_memory", SOURCE)
    loaded = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(loaded)
    return loaded


def _written(folder, name, text):
    path = folder / name
    path.write_text(text, encoding="utf-8")
    return str(path)


def test_cgroup_v2_limit_is_read_in_megabytes(tmp_path):
    memory = _module()
    limits = ((_written(tmp_path, "memory.max", "17179869184\n"), "max"),)

    assert memory.cgroup_memory_mb(limits) == 16384


def test_cgroup_v1_limit_is_read_when_v2_is_not_there(tmp_path):
    memory = _module()
    limits = ((str(tmp_path / "absent"), "max"), (_written(tmp_path, "limit_in_bytes", "12884901888"), None))

    assert memory.cgroup_memory_mb(limits) == 12288


def test_a_cgroup_saying_max_is_no_limit(tmp_path):
    memory = _module()
    limits = ((_written(tmp_path, "memory.max", "max\n"), "max"),)

    assert memory.cgroup_memory_mb(limits) is None


def test_the_sentinel_v1_writes_for_no_limit_is_no_limit(tmp_path):
    memory = _module()
    limits = ((_written(tmp_path, "limit_in_bytes", str(9223372036854771712)), None),)

    assert memory.cgroup_memory_mb(limits) is None


def test_a_file_holding_something_else_is_no_limit(tmp_path):
    memory = _module()
    limits = ((_written(tmp_path, "memory.max", ""), "max"), (_written(tmp_path, "limit_in_bytes", "plenty"), None))

    assert memory.cgroup_memory_mb(limits) is None


def test_nowhere_to_read_is_no_limit(tmp_path):
    memory = _module()

    assert memory.cgroup_memory_mb(((str(tmp_path / "absent"), "max"),)) is None


def test_the_memory_available_is_the_lower_of_the_machine_and_the_container(monkeypatch):
    memory = _module()
    monkeypatch.setattr(memory, "physical_memory_mb", lambda: 32054)
    monkeypatch.setattr(memory, "cgroup_memory_mb", lambda: 16384)

    assert memory.available_memory_mb() == 16384


def test_a_cgroup_wider_than_the_machine_does_not_raise_the_answer(monkeypatch):
    memory = _module()
    monkeypatch.setattr(memory, "physical_memory_mb", lambda: 8192)
    monkeypatch.setattr(memory, "cgroup_memory_mb", lambda: 65536)

    assert memory.available_memory_mb() == 8192


def test_without_a_cgroup_the_machine_answers(monkeypatch):
    memory = _module()
    monkeypatch.setattr(memory, "physical_memory_mb", lambda: 32054)
    monkeypatch.setattr(memory, "cgroup_memory_mb", lambda: None)

    assert memory.available_memory_mb() == 32054


def test_a_platform_that_reports_nothing_leaves_the_cgroup_to_answer(monkeypatch):
    memory = _module()
    monkeypatch.setattr(memory, "physical_memory_mb", lambda: None)
    monkeypatch.setattr(memory, "cgroup_memory_mb", lambda: 2048)

    assert memory.available_memory_mb() == 2048


def test_this_machine_answers_with_something_sensible():
    memory = _module()
    available = memory.available_memory_mb()

    assert available is None or available > 0
