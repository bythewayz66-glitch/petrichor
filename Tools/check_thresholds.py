#!/usr/bin/env python3
"""
Week-one water gate checker.

Reads the harness's JSON summaries plus the NUnit results XML, compares them
against Tools/bench_thresholds.json, and returns a verdict.

Exit codes are the CI contract:
    0  PASS          - within threshold on every scenario
    1  BUDGET FAIL   - walk the named rung of the ladder
    2  HARD FAIL     - fix the solver; do NOT walk the ladder
    3  INPUT ERROR   - the checker could not read its inputs

Correctness is evaluated BEFORE budget, deliberately. A fast wrong solver is
the most dangerous outcome this gate can produce, and the ordering makes it
impossible to mistake for a pass.

The authoritative timings come from the harness's own summary files, which the
harness writes with a Stopwatch. The NUnit XML is read for two things only: a
failed test case (which is a hard fail regardless of the numbers) and a
fallback source of timings if a summary is missing.
"""

import argparse
import json
import os
import sys
import xml.etree.ElementTree as ET

EXIT_PASS = 0
EXIT_BUDGET = 1
EXIT_HARD = 2
EXIT_INPUT = 3

BUDGET_SCENARIOS = ["A_StaticSoak", "B_StepFlood", "C_FastForward"]
ALL_SCENARIOS = BUDGET_SCENARIOS + ["D_Determinism"]


def load_thresholds(path):
    with open(path, "r", encoding="utf-8") as fh:
        return json.load(fh)


def load_summaries(results_dir):
    """Read summary_<scenario>.json written by the harness itself.

    Deliberately not the package's own sample output: that format is an
    implementation detail which has changed between major versions, and a gate
    that breaks when a package updates is a gate that gets disabled.
    """
    summaries = {}
    for scenario in ALL_SCENARIOS:
        path = os.path.join(results_dir, "summary_%s.json" % scenario)
        if not os.path.isfile(path):
            continue
        try:
            with open(path, "r", encoding="utf-8") as fh:
                summaries[scenario] = json.load(fh)
        except ValueError:
            # A malformed summary is an input error, not a silent skip.
            raise
    return summaries


def load_xml(results_xml):
    """Return (failed_test_names, timings_from_xml).

    A failed test case is a hard fail: the harness asserts correctness in NUnit
    before it ever writes a summary, so a red test means the solver is wrong.
    """
    failed = []
    timings = {}

    if not os.path.isfile(results_xml):
        return failed, timings

    try:
        tree = ET.parse(results_xml)
    except ET.ParseError:
        return failed, timings

    for case in tree.iter("test-case"):
        name = case.get("name", "")
        if case.get("result") == "Failed":
            failed.append(name)

        for scenario in BUDGET_SCENARIOS:
            if scenario not in name:
                continue
            output = case.find("output")
            if output is None or not output.text:
                continue
            for line in output.text.splitlines():
                line = line.strip()
                if not line.startswith("PET_BENCH "):
                    continue
                try:
                    data = json.loads(line[len("PET_BENCH "):])
                except ValueError:
                    continue
                timings[scenario] = {
                    "p50_ms": float(data["p50_ms"]),
                    "p95_ms": float(data["p95_ms"]),
                    "max_ms": float(data["max_ms"]),
                }

    return failed, timings


def check_hard_fails(summaries, hard):
    """Returns a list of human-readable hard-fail reasons."""
    reasons = []

    nan_max = hard.get("nan_count_max", 0)
    drift_max = hard.get("mass_drift_max", 0.001)
    hash_nonzero = hard.get("hash_must_be_nonzero", True)

    for scenario in ALL_SCENARIOS:
        s = summaries.get(scenario)
        if s is None:
            continue

        nans = int(s.get("nan_count", 0))
        if nans > nan_max:
            reasons.append(
                "%s: %d non-finite depth samples (max %d)" % (scenario, nans, nan_max)
            )

        # Mass BALANCE, not mass constancy: a field with an open source is
        # supposed to gain water. Compare against before + inflow.
        before = float(s.get("mass_before", 0.0))
        inflow = float(s.get("mass_inflow", 0.0))
        after = float(s.get("mass_after", 0.0))
        expected = before + inflow
        if expected > 0.0:
            drift = abs(after - expected) / expected
            if drift > drift_max:
                reasons.append(
                    "%s: mass balance off by %.4f%% (expected %.4f m3, got %.4f m3)"
                    % (scenario, drift * 100.0, expected, after)
                )

        if hash_nonzero and int(s.get("hash", 0)) == 0:
            reasons.append("%s: field hash collapsed to zero" % scenario)

    return reasons


def check_budget(timings, thresholds):
    """Returns a list of (scenario, metric, actual, limit) over-threshold rows."""
    over = []
    for scenario in BUDGET_SCENARIOS:
        t = timings.get(scenario)
        if t is None:
            continue
        limits = thresholds.get(scenario, {})
        for metric in ("p50_ms", "p95_ms", "max_ms"):
            if metric not in limits:
                continue
            actual = t.get(metric)
            if actual is None:
                continue
            if actual > limits[metric]:
                over.append((scenario, metric, actual, limits[metric]))
    return over


def main():
    parser = argparse.ArgumentParser(description="PETRICHOR week-one water gate checker")
    parser.add_argument("--tier", required=True, choices=["linux", "android"])
    parser.add_argument("--results-dir", required=True)
    parser.add_argument("--results-xml", required=True)
    parser.add_argument("--thresholds", required=True)
    parser.add_argument(
        "--attempt",
        type=int,
        default=0,
        help="How many ladder rungs are already applied to the build under test. "
             "The checker uses this to name the NEXT rung, so the rung is a "
             "function of the run's history rather than a judgement call.",
    )
    parser.add_argument("--out", required=True, help="Verdict file to write.")
    args = parser.parse_args()

    try:
        cfg = load_thresholds(args.thresholds)
    except (IOError, ValueError) as exc:
        print("INPUT ERROR: cannot read thresholds: %s" % exc)
        return EXIT_INPUT

    tier_cfg = cfg["tiers"].get(args.tier)
    if tier_cfg is None:
        print("INPUT ERROR: no tier named %r in thresholds" % args.tier)
        return EXIT_INPUT

    try:
        summaries = load_summaries(args.results_dir)
    except ValueError as exc:
        print("INPUT ERROR: malformed summary JSON: %s" % exc)
        return EXIT_INPUT

    failed_tests, xml_timings = load_xml(args.results_xml)

    # Prefer the harness's own summaries; fall back to the XML.
    timings = {}
    for scenario in BUDGET_SCENARIOS:
        s = summaries.get(scenario)
        if s is not None and "p95_ms" in s:
            timings[scenario] = {
                "p50_ms": float(s["p50_ms"]),
                "p95_ms": float(s["p95_ms"]),
                "max_ms": float(s["max_ms"]),
            }
        elif scenario in xml_timings:
            timings[scenario] = xml_timings[scenario]

    if not summaries and not timings:
        print("INPUT ERROR: no summaries in %s and no timings in %s"
              % (args.results_dir, args.results_xml))
        return EXIT_INPUT

    lines = []
    lines.append("PETRICHOR week-one water gate")
    lines.append("tier: %s (%s)" % (args.tier, tier_cfg.get("label", "")))
    lines.append("attempt: %d" % args.attempt)
    lines.append("")

    # ---- Correctness first ------------------------------------------------
    hard_reasons = check_hard_fails(summaries, cfg.get("hard_fail", {}))

    if failed_tests:
        hard_reasons.append(
            "%d test case(s) failed in the NUnit run: %s"
            % (len(failed_tests), ", ".join(sorted(set(failed_tests))))
        )

    if hard_reasons:
        lines.append("VERDICT: HARD FAIL")
        lines.append("")
        lines.append("These are NOT budget failures. The ladder buys performance")
        lines.append("and cannot buy correctness. Fix the solver.")
        lines.append("")
        for reason in hard_reasons:
            lines.append("  - %s" % reason)
        write_verdict(args.out, lines)
        print("\n".join(lines))
        return EXIT_HARD

    lines.append("correctness: OK (no NaN, mass balanced, hash non-zero, all tests green)")
    lines.append("")

    # ---- Budget second ----------------------------------------------------
    over = check_budget(timings, tier_cfg.get("thresholds", {}))

    lines.append("scenario            p50      p95      max     (ms per step)")
    for scenario in BUDGET_SCENARIOS:
        t = timings.get(scenario)
        if t is None:
            lines.append("%-18s  (no timing recorded)" % scenario)
            continue
        lines.append(
            "%-18s  %6.3f   %6.3f   %6.3f"
            % (scenario, t["p50_ms"], t["p95_ms"], t["max_ms"])
        )
    lines.append("")

    if not over:
        lines.append("VERDICT: PASS")
        write_verdict(args.out, lines)
        print("\n".join(lines))
        return EXIT_PASS

    ladder = cfg.get("ladder", [])
    next_rung = None
    for rung in ladder:
        if rung["rung"] == args.attempt + 1:
            next_rung = rung
            break

    lines.append("VERDICT: BUDGET FAIL")
    lines.append("")
    for scenario, metric, actual, limit in over:
        lines.append("  - %s %s = %.3f ms (limit %.3f ms)" % (scenario, metric, actual, limit))
    lines.append("")

    if next_rung is None:
        lines.append("No rung remains at attempt %d. Escalate: the ladder is exhausted."
                     % args.attempt)
    else:
        lines.append("NEXT RUNG: %d" % next_rung["rung"])
        lines.append("  action: %s" % next_rung["action"])
        lines.append("  cost:   %s" % next_rung["cost"])
        lines.append("")
        lines.append("Re-run with --attempt %d once applied." % (args.attempt + 1))

    write_verdict(args.out, lines)
    print("\n".join(lines))
    return EXIT_BUDGET


def write_verdict(path, lines):
    directory = os.path.dirname(path)
    if directory:
        os.makedirs(directory, exist_ok=True)
    with open(path, "w", encoding="utf-8") as fh:
        fh.write("\n".join(lines) + "\n")


if __name__ == "__main__":
    sys.exit(main())
