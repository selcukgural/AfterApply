#!/bin/sh
# Advisory graphify hint: fires only when a Bash command actually greps the tree
# (grep/rg/ag) and a graph exists. Never blocks; the graph can be stale between
# commits (it is rebuilt by the post-commit hook), so the source stays authoritative.
[ -f graphify-out/graph.json ] || exit 0
python3 -c '
import json, re, sys
try:
    cmd = json.load(sys.stdin).get("tool_input", {}).get("command", "")
except Exception:
    sys.exit(0)
if re.search(r"(^|[\s|;&(])(grep|rg|ag)(\s|$)", cmd):
    print("Hint: for broad \"where/how is X related\" questions, graphify query \"...\" can scope faster than grep. Optional; the graph may lag uncommitted edits.")
'
exit 0
