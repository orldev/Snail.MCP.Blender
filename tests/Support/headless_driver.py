"""Runs the add-on from a folder in a background Blender for the live tests, through the same loop blender -b -c snail_bridge runs.

With SNAIL_TEST_COMMAND_LINE set it goes through the command line parser instead, one argument per line, as the container starts it.
"""

import importlib
import os
import sys

root, package_name, port = sys.argv[-3], sys.argv[-2], int(sys.argv[-1])
sys.path.insert(0, root)
package = importlib.import_module(package_name)
command_line = os.environ.get("SNAIL_TEST_COMMAND_LINE")
if command_line is not None:
    sys.exit(package._command(["--port", str(port), *command_line.splitlines()]))
package.serve(port=port, token=os.environ.get("SNAIL_TEST_TOKEN", ""), ready=lambda: print("SNAIL_READY", flush=True))
