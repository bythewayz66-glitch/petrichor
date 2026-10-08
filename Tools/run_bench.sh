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
WINDOW_SCOPE=""
WINDOW_RADIUS=""

while [[ $# -gt 0 ]]; do
  case "$1" in
    --tier)          TIER="$2"; shift 2 ;;
    --attempt)       ATTEMPT="$2"; shift 2 ;;
    --solver)        SOLVER="$2"; shift 2 ;;
    --tile-res)      TILE_RES="$2"; shift 2 ;;
    --window-scope)  WINDOW_SCOPE="$2"; shift 2 ;;
    --window-radius) WINDOW_RADIUS="$2"; shift 2 ;;
    *) echo "unknown argument: $1" >&2; exit 3 ;;
  esac
done

if [[ -z "$TIER" ]]; then
  echo "usage: $0 --tier {linux|android} [--attempt N] [--solver {heightfield|channel-graph}] [--tile-res N] [--window-scope {none|tiles|cells}] [--window-radius N]" >&2
  exit 3
fi

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
UNITY="${UNITY_PATH:-/opt/unity/Editor/Unity}"
RESULTS_DIR="$ROOT/BenchmarkResults"
RESULTS_XML="$RESULTS_DIR/results_${TIER}.xml"
VERDICT="$RESULTS_DIR/verdict_${TIER}.txt"
QUIETNESS="$RESULTS_DIR/quietness_${TIER}.txt"

mkdir -p "$RESULTS_DIR"

# ---------------------------------------------------------------------------
# Refuse to start if there is no editor to run.
#
# This is a data-loss guard, not a convenience check. The verdict below is
# written unconditionally at the end of this script, and it is a COMMITTED file
# (BenchmarkResults/verdict_*.txt is one of the two paths .gitignore re-includes
# under BenchmarkResults/). Without this guard, a checkout with no editor
# installed runs to completion, finds no summaries, and overwrites the committed
# verdict with an INPUT ERROR - destroying the only committed evidence the gate
# has, silently, with no error. That is exactly what happened on this machine
# before this guard existed: rung 0's verdict (blob 179076dd) was replaced by an
# INPUT ERROR and had to be restored from git.
#
# The guard is placed after the findings path is computed and before any file is
# touched, so a run with no editor leaves the tree exactly as it found it.
# ---------------------------------------------------------------------------
if [[ ! -x "$UNITY" ]]; then
  echo "REFUSING TO RUN: no editor at $UNITY" >&2
  echo >&2
  echo "  Set UNITY_PATH to the editor binary, or install the editor." >&2
  echo "  Example:" >&2
  echo "    UNITY_PATH=\$HOME/Unity/Hub/Editor/6000.5.9f1/Editor/Unity \\\\" >&2
  echo "      HOME=\$HOME Tools/run_bench.sh --tier $TIER --attempt $ATTEMPT" >&2
  echo >&2
  echo "  Nothing was written. The committed verdict is untouched." >&2
  exit 3
fi

# ---------------------------------------------------------------------------
# Machine quietness.
#
# The rung-1 verdict flips on CPU contention alone: the same binary on the same
# box reports A p50 1.544 ms (over the 1.500 ms limit) or 1.377 ms (under it)
# depending on what else is running. A frame-time number without the machine's
# idle fraction attached is therefore not reproducible, and the gate cannot tell
# a real regression from a noisy neighbour.
#
# Two samples are taken. QUIET_PRE is a short idle window BEFORE the editor
# starts, so a box that is already busy is caught before a run is spent. The
# second spans the Unity invocation itself and is the number that describes the
# conditions the measurement actually experienced.
#
# Both are printed to stdout so they land in the CI log, and written to a file
# so a local run leaves the same record. The file is git-ignorable like the
# other per-run samples.
# ---------------------------------------------------------------------------
read_stat() {
  local _cpu rest
  read -r _cpu rest < /proc/stat
  # shellcheck disable=SC2086
  set -- $rest
  local user=${1:-0} nice=${2:-0} system=${3:-0} idle=${4:-0} iowait=${5:-0}
  local irq=${6:-0} softirq=${7:-0} steal=${8:-0}
  echo "$((idle + iowait)) $((user + nice + system + idle + iowait + irq + softirq + steal))"
}

# Busy percent of ALL cores over the window, as an integer and as tenths, so it
# can be printed with one decimal without floating point.
busy_tenths() { # idle_delta total_delta
  local didle="$1" dtotal="$2"
  [[ "$dtotal" -le 0 ]] && { echo 0; return; }
  echo $(( (1000 * (dtotal - didle)) / dtotal ))
}

NCORES="$(nproc)"

QUIET_PRE_SECONDS=5
read -r I0 T0 <<< "$(read_stat)"
sleep "$QUIET_PRE_SECONDS"
read -r I1 T1 <<< "$(read_stat)"
PRE_TENTHS="$(busy_tenths $((I1 - I0)) $((T1 - T0)))"

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

# Ladder rung 2 shrinks the ACTIVE WATER WINDOW. It is passed the same way as
# the solver and the resolution, and for the same reason: the run summary
# records the scope and radius it actually measured at, so a rung-2 number can
# never be reported as a rung-0 one.
#
# Empty means "no window" - the harness's own default - so a run that does not
# pass --window-scope measures exactly what it measured before the option
# existed. An unrecognised value is NOT caught here; the harness throws on it,
# which fails the NUnit run, which the checker reads as a HARD FAIL. Validating
# in two places would mean two places to keep in step.
export PET_BENCH_WINDOW_SCOPE="${WINDOW_SCOPE:-}"
export PET_BENCH_WINDOW_RADIUS="${WINDOW_RADIUS:-}"

# One-decimal rendering of an integer number of tenths, with no floating point.
# `bc` is not guaranteed present and is not worth a dependency for this.
fmt_tenths() { echo "$(( $1 / 10 )).$(( $1 % 10 ))"; }

echo "solver:  $PET_BENCH_SOLVER"
echo "tile res: ${PET_BENCH_TILE_RES:-257 (shipping)}"
echo "rung:    $ATTEMPT"
if [[ -n "$PET_BENCH_WINDOW_SCOPE" ]]; then
  echo "window:  $PET_BENCH_WINDOW_SCOPE (radius ${PET_BENCH_WINDOW_RADIUS:-1})"
else
  echo "window:  none"
fi
echo
echo "== quietness before the editor =="
echo "cores:        $NCORES"
echo "busy:         $(fmt_tenths "$PRE_TENTHS")% of all cores over ${QUIET_PRE_SECONDS} s"
echo "loadavg:      $(cut -d' ' -f1-3 /proc/loadavg)"
echo

read -r I2 T2 <<< "$(read_stat)"

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

read -r I3 T3 <<< "$(read_stat)"

RUN_TENTHS="$(busy_tenths $((I3 - I2)) $((T3 - T2)))"
RUN_SECONDS=$(( T3 - T2 ))

QUIET_VERDICT="quiet"
if [[ "$RUN_TENTHS" -gt 200 ]]; then
  QUIET_VERDICT="BUSY - the numbers below are not attributable"
elif [[ "$RUN_TENTHS" -gt 50 ]]; then
  QUIET_VERDICT="noisy"
fi

echo
echo "== quietness result =="
printf 'busy during the run: %.1f%% of all cores  (= %.1f of %s cores)\n' \
  "$(fmt_tenths "$RUN_TENTHS")" \
  "$(python3 -c "print($RUN_TENTHS/1000.0*$NCORES)" 2>/dev/null || echo "n/a")" \
  "$NCORES"
echo "run wall clock:      ${RUN_SECONDS} s"
echo "verdict:             $QUIET_VERDICT"

if [[ "$RUN_TENTHS" -gt 200 ]]; then
  echo
  echo "WARNING: the machine was more than 20% busy across the run." >&2
  echo "WARNING: the timings below describe this machine's contention as much" >&2
  echo "WARNING: as the solver. Re-run on an idle box before acting on them." >&2
fi

{
  echo "PETRICHOR gate - machine quietness"
  echo "tier:            $TIER"
  echo "attempt:         $ATTEMPT"
  echo "solver:          $PET_BENCH_SOLVER"
  echo "tile_res:        ${PET_BENCH_TILE_RES:-257}"
  echo "window_scope:    ${PET_BENCH_WINDOW_SCOPE:-none}"
  echo "window_radius:   ${PET_BENCH_WINDOW_RADIUS:-0}"
  echo "cores:           $NCORES"
  echo "busy_pre_pct:    $(fmt_tenths "$PRE_TENTHS")"
  echo "busy_run_pct:    $(fmt_tenths "$RUN_TENTHS")"
  echo "busy_run_cores:  $(python3 -c "print(round($RUN_TENTHS/1000.0*$NCORES,2))" 2>/dev/null || echo unmeasured)"
  echo "run_seconds:     $RUN_SECONDS"
  echo "loadavg:         $(cut -d' ' -f1-3 /proc/loadavg)"
  echo "verdict:         $QUIET_VERDICT"
} > "$QUIETNESS"

echo "quietness written to $QUIETNESS"

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
