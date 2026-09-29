#!/usr/bin/env bash
#
# Week-one water gate runner.
#
# Invokes Unity in batch mode, runs the benchmark, then calls the checker.
# Works identically on a developer machine and in CI.
#
# Usage:
#   Tools/run_bench.sh --tier linux   [--attempt 0]
#   Tools/run_bench.sh --tier android [--attempt 0]
#
set -euo pipefail

TIER=""
ATTEMPT=0

while [[ $# -gt 0 ]]; do
  case "$1" in
    --tier)    TIER="$2"; shift 2 ;;
    --attempt) ATTEMPT="$2"; shift 2 ;;
    *) echo "unknown argument: $1" >&2; exit 3 ;;
  esac
done

if [[ -z "$TIER" ]]; then
  echo "usage: $0 --tier {linux|android} [--attempt N]" >&2
  exit 3
fi

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
UNITY="${UNITY_PATH:-/opt/unity/Editor/Unity}"
RESULTS_DIR="$ROOT/BenchmarkResults"
RESULTS_XML="$RESULTS_DIR/results_${TIER}.xml"
VERDICT="$RESULTS_DIR/verdict_${TIER}.txt"

mkdir -p "$RESULTS_DIR"

echo "== PETRICHOR water gate =="
echo "tier:    $TIER"
echo "attempt: $ATTEMPT"
echo "unity:   $UNITY"
echo

# The tier is passed to the harness through the environment so the summary
# records which device produced it.
export PET_BENCH_TIER="$TIER"

"$UNITY" \
  -batchmode \
  -nographics \
  -projectPath "$ROOT" \
  -runTests \
  -testPlatform EditMode \
  -testFilter PET.Benchmarks \
  -testResults "$RESULTS_XML" \
  -logFile "$RESULTS_DIR/unity_${TIER}.log" \
  || true   # a test failure is a verdict, not a script error

python3 "$ROOT/Tools/check_thresholds.py" \
  --tier "$TIER" \
  --results-dir "$RESULTS_DIR" \
  --results-xml "$RESULTS_XML" \
  --thresholds "$ROOT/Tools/bench_thresholds.json" \
  --attempt "$ATTEMPT" \
  --out "$VERDICT"

STATUS=$?
echo
echo "verdict written to $VERDICT"
exit $STATUS
