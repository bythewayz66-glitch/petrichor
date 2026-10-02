#!/usr/bin/env bash
#
# Week-one water gate runner.
#
# Invokes Unity in batch mode, runs the benchmark, then calls the checker.
# Works identically on a developer machine and in CI.
#
# Usage:
#   Tools/run_bench.sh --tier linux   [--attempt 0] [--solver heightfield]
#   Tools/run_bench.sh --tier android [--attempt 0] [--solver channel-graph]
#
set -euo pipefail

TIER=""
ATTEMPT=0
SOLVER=""
TILE_RES=""

while [[ $# -gt 0 ]]; do
  case "$1" in
    --tier)     TIER="$2"; shift 2 ;;
    --attempt)  ATTEMPT="$2"; shift 2 ;;
    --solver)   SOLVER="$2"; shift 2 ;;
    --tile-res) TILE_RES="$2"; shift 2 ;;
    *) echo "unknown argument: $1" >&2; exit 3 ;;
  esac
done

if [[ -z "$TIER" ]]; then
  echo "usage: $0 --tier {linux|android} [--attempt N] [--solver {heightfield|channel-graph}] [--tile-res N]" >&2
  exit 3
fi

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
UNITY="${UNITY_PATH:-/opt/unity/Editor/Unity}"
RESULTS_DIR="$ROOT/BenchmarkResults"
RESULTS_XML="$RESULTS_DIR/results_${TIER}.xml"
VERDICT="$RESULTS_DIR/verdict_${TIER}.txt"

mkdir -p "$RESULTS_DIR"

# Clear the previous run's summaries before measuring anything.
#
# This is a correctness fix, not housekeeping. If Unity aborts before the
# harness writes anything - a compile error, a licence failure, a crashed import
# - the last run's summaries are still on disk, and the checker below reads
# them and prints a confident verdict for a run that measured nothing. The
# resulting output is indistinguishable from a real measurement and would
# advance the rung ladder on numbers the failing run never produced.
#
# Removing them up front means "no summaries" means what it says, which is the
# condition the checker already treats as an INPUT ERROR.
rm -f "$RESULTS_DIR"/summary_*.json

echo "== PETRICHOR water gate =="
echo "tier:    $TIER"
echo "attempt: $ATTEMPT"
echo "unity:   $UNITY"
echo

# The tier is passed to the harness through the environment so the summary
# records which device produced it.
export PET_BENCH_TIER="$TIER"

# The solver is passed the same way, and for the same reason: the summary has
# to record which rung of the fallback ladder produced the numbers, or a report
# cannot be attributed to a solver.
#
# The default is the reference heightfield solver, so a run that does not pass
# --solver measures exactly what it measured before this option existed. The
# harness itself defaults to the same value, so the two agree whether the
# variable is set here or not.
#
# An unrecognised value is NOT caught here. The harness throws on it, which
# fails the NUnit run, which the checker reads as a HARD FAIL. Validating in
# two places would mean two places to keep in step, and the harness is the one
# that actually knows which solvers exist.
export PET_BENCH_SOLVER="${SOLVER:-heightfield}"

# Ladder rung 1 lowers the simulation resolution. It is passed the same way as
# the solver, and for the same reason: the summary records the resolution it
# actually measured at, so a rung-1 number can never be reported as a rung-0 one.
#
# Empty means "shipping resolution" - the harness's own default - so a run that
# does not pass --tile-res measures exactly what it measured before the option
# existed.
export PET_BENCH_TILE_RES="${TILE_RES:-}"

echo "solver:  $PET_BENCH_SOLVER"
echo "tile res: ${PET_BENCH_TILE_RES:-257 (shipping)}"
echo

"$UNITY" \
  -batchmode \
  -nographics \
  -projectPath "$ROOT" \
  -runTests \
  -testPlatform EditMode \
  -assemblyNames PET.Benchmarks \
  -testResults "$RESULTS_XML" \
  -logFile "$RESULTS_DIR/unity_${TIER}.log" \
  || true   # a test failure is a verdict, not a script error

# set +e around the checker, deliberately.
#
# The checker's exit code IS the verdict (0 PASS, 1 BUDGET, 2 HARD FAIL,
# 3 INPUT ERROR) and this script must propagate it. Under `set -e` a non-zero
# exit would terminate the script at the checker call, so the STATUS capture
# below would be dead code and the "verdict written to" line would never print
# on exactly the runs where knowing the path matters most.
set +e
python3 "$ROOT/Tools/check_thresholds.py" \
  --tier "$TIER" \
  --results-dir "$RESULTS_DIR" \
  --results-xml "$RESULTS_XML" \
  --thresholds "$ROOT/Tools/bench_thresholds.json" \
  --attempt "$ATTEMPT" \
  --out "$VERDICT"
STATUS=$?
set -e

echo
echo "verdict written to $VERDICT"
exit $STATUS
