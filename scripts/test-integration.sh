#!/usr/bin/env bash
# The one way to run the integration suite locally.
#
# What it adds over a bare `dotnet test`, and why each piece exists (DECISIONS.md 2026-09-15):
#   - a per-test hang limit (--blame-hang): a test that stalls ends the run in 2 minutes *with its
#     name*, instead of burning 40 silent minutes the way PostgresPoolCapTests did;
#   - a hard wall-clock limit on the whole run, in case the harness itself wedges;
#   - container cleanup on every exit path — a --blame-hang kill skips the fixtures' DisposeAsync,
#     so the Postgres container would otherwise stay up until the next run prunes it;
#   - a per-class timing table from the TRX, so a class that regresses is visible at a glance;
#   - the podman wiring (socket + Ryuk off) that README used to ask everyone to type by hand.
#
# Usage: scripts/test-integration.sh [extra dotnet test args, e.g. --filter "FullyQualifiedName~Payments"]
set -euo pipefail

cd "$(dirname "$0")/.."

PROJECT=tests/AfterApply.IntegrationTests
RESULTS=$PROJECT/TestResults
HANG_TIMEOUT=${HANG_TIMEOUT:-2m}
RUN_TIMEOUT_SECONDS=${RUN_TIMEOUT_SECONDS:-900}

mkdir -p "$RESULTS"
rm -f "$RESULTS"/integration.trx "$RESULTS"/crash.log

# Testcontainers needs a Docker-compatible socket. On this machine that is podman's, where Ryuk
# (the reaper sidecar) cannot start under rootless podman — TestContainerCleanup prunes instead.
if [[ -z "${DOCKER_HOST:-}" ]] && command -v podman >/dev/null 2>&1; then
  socket=$(podman machine inspect --format '{{.ConnectionInfo.PodmanSocket.Path}}' 2>/dev/null || true)
  if [[ -n "$socket" ]]; then
    export DOCKER_HOST="unix://$socket"
    export TESTCONTAINERS_RYUK_DISABLED=true
  fi
fi
export AFTERAPPLY_CRASH_LOG="$PWD/$RESULTS/crash.log"

cleanup() {
  # Anything Testcontainers started and did not get to stop. Only containers carrying its own
  # label, so nothing else on a shared VM is touched.
  local cli
  for cli in podman docker; do
    if command -v "$cli" >/dev/null 2>&1; then
      ids=$("$cli" ps -aq --filter label=org.testcontainers=true 2>/dev/null || true)
      if [[ -n "$ids" ]]; then
        echo "[test-integration] removing leftover test containers: $(echo "$ids" | tr '\n' ' ')"
        # shellcheck disable=SC2086
        "$cli" rm -f $ids >/dev/null 2>&1 || true
      fi
      break
    fi
  done
}
trap cleanup EXIT

start=$(date +%s)

# The run itself, under a wall-clock watchdog. `timeout` is not on stock macOS, hence by hand.
dotnet test "$PROJECT" -nologo -v normal \
  --blame-hang --blame-hang-timeout "$HANG_TIMEOUT" --blame-hang-dump-type mini \
  --blame-crash \
  --logger "trx;LogFileName=integration.trx" --results-directory "$RESULTS" \
  "$@" &
test_pid=$!
(
  # Polls rather than sleeping the whole timeout, so it ends on its own a second after the run
  # does and leaves no stray process behind.
  for ((i = 0; i < RUN_TIMEOUT_SECONDS; i++)); do
    sleep 1
    kill -0 "$test_pid" 2>/dev/null || exit 0
  done
  echo "[test-integration] run exceeded ${RUN_TIMEOUT_SECONDS}s — killing it" >&2
  kill "$test_pid" 2>/dev/null || true
  sleep 5
  kill -9 "$test_pid" 2>/dev/null || true
) >/dev/null &
set +e
wait "$test_pid"
status=$?
set -e

elapsed=$(( $(date +%s) - start ))
echo
echo "[test-integration] wall clock: ${elapsed}s, exit status: $status"

# The crash log always ends with a "PROCESS-EXIT clean" line on a normal exit; anything else in
# it is an unhandled background exception the runner could not attribute to a test.
if [[ -s "$AFTERAPPLY_CRASH_LOG" ]] && grep -qv "PROCESS-EXIT\|^clean$" "$AFTERAPPLY_CRASH_LOG"; then
  echo "[test-integration] crash log ($AFTERAPPLY_CRASH_LOG):"
  cat "$AFTERAPPLY_CRASH_LOG"
elif [[ ! -s "$AFTERAPPLY_CRASH_LOG" ]]; then
  echo "[test-integration] no PROCESS-EXIT line in the crash log: the test host did not shut down cleanly (killed, or a native crash)"
fi

# Per-class table from the TRX: how long each class's tests took, and how long the class took on
# the wall (the difference is its host boot plus the per-test resets). Sorted slowest first.
trx=$RESULTS/integration.trx
if [[ -f "$trx" ]]; then
  python3 - "$trx" <<'PY'
import re, sys, xml.etree.ElementTree as ET
from collections import defaultdict
from datetime import datetime

ns = {"t": "http://microsoft.com/schemas/VisualStudio/TeamTest/2010"}
root = ET.parse(sys.argv[1]).getroot()

def parse(ts):
    # TRX writes seven fractional digits; fromisoformat takes at most six.
    ts = re.sub(r"(\.\d{6})\d+", r"\1", ts.replace("Z", "+00:00"))
    return datetime.fromisoformat(ts)

def duration(d):
    h, m, s = d.split(":")
    return int(h) * 3600 + int(m) * 60 + float(s)

classes = defaultdict(lambda: {"tests": 0, "sum": 0.0, "start": None, "end": None, "failed": 0})
for r in root.iter("{%s}UnitTestResult" % ns["t"]):
    name = r.get("testName", "")
    cls = name.rsplit(".", 1)[0].replace("AfterApply.IntegrationTests.", "") if "." in name else name
    c = classes[cls]
    c["tests"] += 1
    c["sum"] += duration(r.get("duration", "00:00:00"))
    if r.get("outcome") != "Passed":
        c["failed"] += 1
    s, e = parse(r.get("startTime")), parse(r.get("endTime"))
    c["start"] = s if c["start"] is None or s < c["start"] else c["start"]
    c["end"] = e if c["end"] is None or e > c["end"] else c["end"]

rows = []
for cls, c in classes.items():
    wall = (c["end"] - c["start"]).total_seconds() if c["start"] else 0.0
    rows.append((wall, cls, c["tests"], c["sum"], c["failed"]))
rows.sort(reverse=True)

total = sum(r[2] for r in rows)
failed = sum(r[4] for r in rows)
print(f"[test-integration] {total} tests in {len(rows)} classes, {failed} failed")
print(f"{'class':58} {'tests':>5} {'tests s':>8} {'wall s':>7}")
for wall, cls, n, s, f in rows[:15]:
    flag = "  <- FAILURES" if f else ""
    print(f"{cls[:58]:58} {n:5d} {s:8.1f} {wall:7.1f}{flag}")
PY
fi

exit $status
