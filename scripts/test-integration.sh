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
# the wall (the difference is its host boot plus the per-test resets). Sorted slowest first. The
# same script writes the table into the CI job summary.
trx=$RESULTS/integration.trx
if [[ -f "$trx" ]]; then
  python3 scripts/test-timings.py "$trx"
fi

exit $status
