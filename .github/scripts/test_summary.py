"""Writes the counts and the failed tests of TRX files to the job summary, and fails when a test failed.

With --require-every-test a test that did not run fails the step too: a live test skips itself when it finds no Blender,
and a run that skipped all of them must not look green.
"""

import argparse
import glob
import os
import sys
import xml.etree.ElementTree as ET

NAMESPACE = {"t": "http://microsoft.com/schemas/VisualStudio/TeamTest/2010"}
COUNTERS = ("total", "passed", "failed", "notExecuted")


def read(pattern):
    totals = dict.fromkeys(COUNTERS, 0)
    failed, skipped = [], []
    for path in glob.glob(pattern, recursive=True):
        root = ET.parse(path).getroot()
        counters = root.find("t:ResultSummary/t:Counters", NAMESPACE)
        for key in COUNTERS:
            totals[key] += int(counters.get(key, 0))
        for result in root.iterfind("t:Results/t:UnitTestResult", NAMESPACE):
            if result.get("outcome") == "Failed":
                failed.append(result.get("testName"))
            elif result.get("outcome") == "NotExecuted":
                skipped.append(result.get("testName"))
    return totals, failed, skipped


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("title")
    parser.add_argument("pattern", help="glob of the TRX files, e.g. TestResults/**/*.trx")
    parser.add_argument("--require-every-test", action="store_true")
    args = parser.parse_args()

    totals, failed, skipped = read(args.pattern)
    lines = [f"### {args.title}", "", f"{totals['passed']} passed, {totals['failed']} failed, {totals['notExecuted']} not run, of {totals['total']}."]
    lines += ["", "Failed:", *(f"- `{name}`" for name in failed)] if failed else []
    lines += ["", "Not run:", *(f"- `{name}`" for name in skipped)] if skipped and args.require_every_test else []

    summary = os.environ.get("GITHUB_STEP_SUMMARY")
    with open(summary, "a", encoding="utf-8") if summary else sys.stdout as output:
        output.write("\n".join(lines) + "\n")

    if totals["total"] == 0:
        print(f"no test results match {args.pattern}", file=sys.stderr)
        return 1
    if failed or (args.require_every_test and skipped):
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
