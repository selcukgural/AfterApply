#!/usr/bin/env python3
"""Per-class timing table from a TRX file, slowest first.

Shared by scripts/test-integration.sh (local runs) and the CI backend job (written to the job
summary), so the two report the same numbers the same way. "tests s" is the sum of the class's
test durations; "wall s" is first test start to last test end — the difference is its host boot
plus per-test resets, or, when classes run in parallel, time spent waiting on the others.

Usage: test-timings.py <file.trx> [--top N] [--markdown] [--prefix Namespace.To.Strip.]
"""
import argparse
import re
import xml.etree.ElementTree as ET
from collections import defaultdict
from datetime import datetime

NS = "{http://microsoft.com/schemas/VisualStudio/TeamTest/2010}"


def parse(ts):
    # TRX writes seven fractional digits; fromisoformat takes at most six.
    ts = re.sub(r"(\.\d{6})\d+", r"\1", ts.replace("Z", "+00:00"))
    return datetime.fromisoformat(ts)


def duration(d):
    h, m, s = d.split(":")
    return int(h) * 3600 + int(m) * 60 + float(s)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("trx")
    ap.add_argument("--top", type=int, default=15)
    ap.add_argument("--markdown", action="store_true")
    ap.add_argument("--prefix", default="AfterApply.IntegrationTests.")
    args = ap.parse_args()

    root = ET.parse(args.trx).getroot()
    classes = defaultdict(lambda: {"tests": 0, "sum": 0.0, "start": None, "end": None, "failed": 0})
    first = last = None
    for r in root.iter(NS + "UnitTestResult"):
        name = r.get("testName", "")
        # Theory rows carry their arguments after the method name; cut them before splitting.
        base = name.split("(", 1)[0]
        cls = base.rsplit(".", 1)[0].replace(args.prefix, "") if "." in base else base
        c = classes[cls]
        c["tests"] += 1
        c["sum"] += duration(r.get("duration", "00:00:00"))
        if r.get("outcome") != "Passed":
            c["failed"] += 1
        s, e = parse(r.get("startTime")), parse(r.get("endTime"))
        c["start"] = s if c["start"] is None or s < c["start"] else c["start"]
        c["end"] = e if c["end"] is None or e > c["end"] else c["end"]
        first = s if first is None or s < first else first
        last = e if last is None or e > last else last

    rows = []
    for cls, c in classes.items():
        wall = (c["end"] - c["start"]).total_seconds() if c["start"] else 0.0
        rows.append((wall, cls, c["tests"], c["sum"], c["failed"]))
    rows.sort(reverse=True)

    total = sum(r[2] for r in rows)
    failed = sum(r[4] for r in rows)
    span = (last - first).total_seconds() if first else 0.0
    summed = sum(r[3] for r in rows)
    headline = (f"{total} tests in {len(rows)} classes, {failed} failed; "
                f"first start to last end {span:.0f}s, test bodies summed {summed:.0f}s")

    if args.markdown:
        print(f"**{headline}**\n")
        print(f"Slowest {min(args.top, len(rows))} classes by wall time:\n")
        print("| class | tests | tests s | wall s |")
        print("|---|---:|---:|---:|")
        for wall, cls, n, s, f in rows[:args.top]:
            flag = " ❌" if f else ""
            print(f"| `{cls}`{flag} | {n} | {s:.1f} | {wall:.1f} |")
    else:
        print(f"[test-timings] {headline}")
        print(f"{'class':58} {'tests':>5} {'tests s':>8} {'wall s':>7}")
        for wall, cls, n, s, f in rows[:args.top]:
            flag = "  <- FAILURES" if f else ""
            print(f"{cls[:58]:58} {n:5d} {s:8.1f} {wall:7.1f}{flag}")


if __name__ == "__main__":
    main()
