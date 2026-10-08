#!/usr/bin/env python3
"""
Update docs/gate-reports/M1.json and docs/HANDOFF.json for the rung-2 revision.

WHY A GENERATOR AND NOT A HAND EDIT
-----------------------------------
Two reasons, both about not introducing defects:

1. The rung-2 numbers must equal the ones `Tools/window_model.py` computes. This
   script RUNS that script and embeds its parsed output, so the document and the
   model cannot drift. Transcribing nine figures by hand is how a report ends up
   disagreeing with its own evidence, which is precisely the defect M1 §5.1
   documents.

2. Both files are large JSON documents that other files reference by field name.
   Loading, mutating and re-dumping keeps every field this revision does not
   touch byte-faithful, and cannot silently drop a key.

The script is idempotent: running it twice produces the same bytes.

It writes no secret values. The only literal from a secret is a byte count that
is already public in the CI log.
"""

import copy
import json
import os
import subprocess
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
M1 = os.path.join(ROOT, "docs", "gate-reports", "M1.json")
HANDOFF = os.path.join(ROOT, "docs", "HANDOFF.json")
MODEL = os.path.join(ROOT, "Tools", "window_model.py")

# The commit this revision is written against. Set by the caller so the
# document cannot claim a HEAD it was not built on.
NEW_HEAD = os.environ.get("PET_HEAD_SHA", "")
REVISION = "r2"
REVISION_DATE = "2026-10-07"


def load_model():
    """Run the model script and return its JSON. Single source of the numbers."""
    out = subprocess.run(
        [sys.executable, MODEL], capture_output=True, text=True, check=True
    ).stdout
    return json.loads(out)


# The three files this revision adds or changes, with their sizes filled in by
# the caller after the bytes are final. Kept minimal on purpose: the commit
# message is the authority on SHAs.
IMPLEMENTATION = {
    "new_files": [
        "Assets/Scripts/Water/WaterWindow.cs",
        "Assets/Tests/Benchmarks/ActiveWindowConfig.cs",
    ],
    "changed_files": [
        "Assets/Scripts/Water/WaterSolver.cs",
        "Assets/Tests/Benchmarks/BenchmarkScenarios.cs",
        "Assets/Tests/Benchmarks/BenchmarkHarness.cs",
        "Assets/Tests/Benchmarks/StaticSoakScenario.cs",
        "Assets/Tests/Benchmarks/StepFloodScenario.cs",
        "Assets/Tests/Benchmarks/FastForwardScenario.cs",
        "Assets/Tests/Benchmarks/CorrectnessScenario.cs",
        "Tools/run_bench.sh",
        "Tools/window_model.py",
    ],
    "frozen_untouched": {
        "Assets/Scripts/Water/IWaterSolver.cs": "77af73ae",
        "Assets/Scripts/Water/HeightfieldWaterSolver.cs": "c61898e0",
        "Assets/Scripts/Water/ChannelGraphWaterSolver.cs": "e36039ed",
        "note": "Blob prefixes, unchanged by this revision, as required.",
    },
    "mechanism": (
        "WaterSolver.Step skips any cell outside a Chebyshev window in all four "
        "passes. Pass 1 clears flux globally and a windowed-out cell never "
        "re-populates it, so no flux crosses the boundary and the step is a "
        "closed system over the active set. A cell outside the window is never "
        "assigned, so it holds its last depth."
    ),
    "data_driven": (
        "Scope, radius and origin are configuration, read from "
        "PET_BENCH_WINDOW_SCOPE / PET_BENCH_WINDOW_RADIUS. Scope=none is the "
        "identity, which is how rungs 0 and 1 stay reproducible from one tree."
    ),
}


def build_rung2(model):
    ps = model["per_scenario"]
    wa = model["window_arithmetic"]
    return {
        "rung": 2,
        "label": "active water window",
        "tile_res": 129,
        "source": "model, not measurement",
        "verdict": "NONE - NOT MEASURED",
        "why_not_measured": (
            "No Unity editor exists in the environment this revision was written "
            "in: /opt/unity/Editor/Unity is absent and run_bench.sh now refuses to "
            "start without it. No benchmark number here was measured on any machine."
        ),
        "implementation": IMPLEMENTATION,
        "window_scope_semantics": {
            "none": "every cell of every tile steps - the identity path rungs 0 and 1 take",
            "tiles": "a tile steps only if within r tiles of the camera's tile; per-tile cost unchanged",
            "cells": "cells within r cells of the camera's cell step; per-tile cost reduced",
            "note": (
                "The rung as named by the ladder is TILE-based. The cell scope exists "
                "because the harness allocates one tile per scenario, so a per-tile "
                "step cost is the only thing it can measure."
            ),
        },
        "window_arithmetic": wa,
        "model": dict(model["model"], **{
            "script": "Tools/window_model.py",
            "invocation": "python3 Tools/window_model.py",
            "formula": "T = S + W(res) * active_fraction;  S = (4*T129 - T257) / 3",
            "confidence": (
                "Low. Two points fit two unknowns exactly, so the fit is not evidence "
                "the model is right. The only independent support is the sublinear "
                "rung 0 -> 1 scaling, which is consistent with a nonzero fixed term."
            ),
        }),
        "per_scenario": ps,
        "key_finding": (
            "At a radius-1 cell window the windowed term is 9 cells of 16641, so the "
            "modelled cost collapses to the fixed term S. Rung 2's modelled floor is "
            "S, not zero. C_FastForward carries S = 1.116 ms, which is 74% of the "
            "1.500 ms p50 threshold spent before any water moves. That is the number "
            "to watch when a real measurement exists."
        ),
        "benchmark_blind_spot": (
            "The benchmark world is 2x2 tiles, so every tile is within 1 of every "
            "other and rung 2's tile saving there is exactly 1.0x. The 7.11x figure "
            "is a property of the shipping 8x8 slice (64 tiles -> 9 active) and cannot "
            "be observed until the harness steps more than one tile."
        ),
        "not_a_game_configuration": (
            "A radius-1 cell window freezes 99.95% of the tile. It is a pinhole, not "
            "a playable setting. The modelled pass means 'rung 2 is worth measuring', "
            "not 'rung 2 passes'."
        ),
    }


def update_m1(doc, model):
    doc = copy.deepcopy(doc)
    doc["status"] = "REVISED"
    doc["revision"] = REVISION
    doc["revision_date"] = REVISION_DATE
    doc["revision_note"] = (
        "r2 adds the committed rung-2 implementation and its cost MODEL, the "
        "quietness instrumentation, and gap G6. Rung 2 has no verdict."
    )
    if NEW_HEAD:
        doc.setdefault("written_against", {})["head_sha"] = NEW_HEAD
    doc["written_against"]["previous_head_sha"] = (
        doc["written_against"].get("head_sha_before_r2")
        or "ce2f122453fc9159bba5cf20b7d3695f0155ef7d"
    )
    doc["written_against"]["previous_head_note"] = (
        "M1.md and M1.json were committed on 2026-10-06. The r2 revision is built on "
        "top of the commits that followed."
    )

    # Rounds the rung list out to three and keeps the two measured rungs first.
    doc["rungs"] = [r for r in doc["rungs"] if r.get("rung") != 2]
    doc["rung_2"] = build_rung2(model)

    doc["speedup_rung1_to_rung2_model"] = [
        {
            "scenario": p["scenario"],
            "p50_rung1_measured": p["measured_p50_rung1_ms"],
            "p50_rung2_modelled": p["model_p50_rung2_cells_ms"],
            "speedup_modelled": p["model_speedup_rung1_to_rung2"],
            "source": "model, not measurement",
        }
        for p in model["per_scenario"]
    ]
    doc["speedup_rung0_to_rung2_model"] = [
        {
            "scenario": p["scenario"],
            "p50_rung0_measured": p["measured_p50_rung0_ms"],
            "p50_rung2_modelled": p["model_p50_rung2_cells_ms"],
            "speedup_modelled": p["model_speedup_rung0_to_rung2"],
            "source": "model, not measurement",
        }
        for p in model["per_scenario"]
    ]

    gaps = doc.setdefault("known_gaps", [])
    gaps[:] = [g for g in gaps if g.get("id") not in ("G2", "G6", "G7")]
    gaps.append({
        "id": "G2",
        "severity": "medium",
        "title": "The quietness instrumentation is committed but has never been attached to a verdict",
        "detail": (
            "run_bench.sh now samples /proc/stat twice and writes "
            "BenchmarkResults/quietness_<tier>.txt with the core count, both busy "
            "fractions, the wall clock, the load average and a quiet/noisy/BUSY "
            "verdict. It has only been exercised on the failed no-editor run, where "
            "it reported 22.2% busy (10.7 of 48 cores) under loadavg 10.81."
        ),
        "consequence": (
            "WAS: no quietness was recorded at all. NOW: quietness is recorded, but "
            "the rung-0 and rung-1 verdicts predate it and still carry no machine "
            "state. They remain unattributable."
        ),
        "resolved": False,
        "regressed_in_severity": "high -> medium",
    })
    gaps.append({
        "id": "G6",
        "severity": "high",
        "title": "No Unity editor exists in the environment the rung-2 revision was written in",
        "detail": (
            "/opt/unity/Editor/Unity is absent, there is no dotnet, no mono and no "
            "docker in the sandbox. The C# added by this revision has never been "
            "compiled, and the rung-2 benchmark has never been run, in any "
            "environment."
        ),
        "consequence": (
            "The rung-2 implementation is unverified. A compile error in "
            "WaterWindow.cs or ActiveWindowConfig.cs would not be caught until a "
            "machine with the editor runs the gate."
        ),
        "resolved": False,
    })
    gaps.append({
        "id": "G7",
        "severity": "medium",
        "title": "The benchmark world cannot measure the tile mechanism the rung names",
        "detail": (
            "The harness reports tile_count = 4 as a 2x2 grid. In a 2x2 world the "
            "active-tile set at radius 1 is 4 of 4, so rung 2's tile selection saves "
            "nothing there."
        ),
        "consequence": (
            "Even with an editor, a rung-2 verdict from this harness measures the "
            "cell scope only. The tile mechanism needs a larger benchmark world."
        ),
        "resolved": False,
    })

    doc["next_gate"] = [
        {"order": 1, "action": "Resolve the rung-1 discrepancy (G1)",
         "why": "The repository's report and its own verdict file still disagree about whether rung 1 passes."},
        {"order": 2, "action": "Take a verdict with the quietness sampler attached (G2)",
         "why": "The instrumentation is committed but has never been attached to a verdict. Discard any run above 20% busy."},
        {"order": 3, "action": "Run rung 2 on a machine with the editor (G6)",
         "why": "The implementation is committed and the model predicts a pass. Neither is a measurement. Do not quote the model as a verdict."},
        {"order": 4, "action": "Widen the benchmark world past 3x3 to measure the tile mechanism (G7)",
         "why": "The 7.11x tile reduction belongs to the shipping 8x8 slice and is unobservable in a 2x2 world."},
        {"order": 5, "action": "Measure the Android tier",
         "why": "Never measured. Requires the reference device to be confirmed as owned."},
        {"order": 6, "action": "Get a CI verdict",
         "why": "Ten runs, no verdict. The gate is unverified end to end."},
    ]

    sign = doc.setdefault("sign_off", {})
    sign["rung2_verdict"] = "none - modelled only, no editor available"
    sign["rung2_implementation"] = "committed"
    sign["quietness_instrumentation"] = "committed, never attached to a verdict"
    sign["week2_may_begin"] = False
    sign["signed"] = "Open World Architect (agent), 2026-10-06, revised 2026-10-07 (r2)"
    return doc


def update_handoff(doc, model, cs_count):
    doc = copy.deepcopy(doc)
    doc["generated"] = REVISION_DATE
    doc["revision"] = REVISION
    if NEW_HEAD:
        doc.setdefault("repository", {})["head_commit"] = NEW_HEAD
        doc["repository"]["head_commit_note"] = (
            "HEAD at the moment this file was last revised (2026-10-07, r2). The "
            "commit that adds this revision is a child of it."
        )

    ladder = doc.get("rung_ladder", [])
    for row in ladder:
        if row.get("rung") == 2:
            row["status"] = "IMPLEMENTED - NOT MEASURED"
            row["implemented_by"] = (
                "Assets/Scripts/Water/WaterWindow.cs + "
                "Assets/Tests/Benchmarks/ActiveWindowConfig.cs, plus a window guard "
                "in all four passes of WaterSolver.cs"
            )
            row["measured"] = False
            row["model"] = {
                "source": "model, not measurement",
                "script": "Tools/window_model.py",
                "modelled_p50_ms": {
                    p["scenario"]: p["model_p50_rung2_cells_ms"]
                    for p in model["per_scenario"]
                },
                "all_under_threshold_modelled": all(
                    p["model_meets_p50_threshold"] for p in model["per_scenario"]
                ),
                "caveat": (
                    "Two points fitted two unknowns; the fit is exact by construction "
                    "and is not evidence the model is correct. Never quote as a verdict."
                ),
            }
            row["cost"] = "distant water stops updating"

    doc.setdefault("counts", {})["csharp_files"] = cs_count
    doc["counts"]["csharp_files_note"] = (
        f"Recounted from the committed tree on {REVISION_DATE}. Was 16; rung 2 adds "
        "WaterWindow.cs and ActiveWindowConfig.cs."
    )
    doc["counts"]["ci_compiles"] = 0
    doc["counts"]["ci_compiles_note"] = (
        "Nothing has ever been compiled in CI, and nothing was compiled anywhere for "
        "the rung-2 revision: no editor exists in the authoring environment (gap G6). "
        "Every CI run failed on activation before the editor loaded the project."
    )

    gh = doc.setdefault("gate_history", {})
    gh["rung2_note"] = (
        "Rung 2 is implemented and committed but unmeasured. The two committed "
        "verdicts remain a BUDGET FAIL at rungs 0 and 1. No new CI run occurred for "
        "this revision; the CI gate is still unverified."
    )
    gh["quietness_instrumentation"] = (
        "run_bench.sh samples /proc/stat before and during the editor and writes "
        "BenchmarkResults/quietness_<tier>.txt. Committed, never yet attached to a "
        "verdict."
    )

    doc["next_steps"] = [
        {"order": 1,
         "action": "Add UNITY_EMAIL and UNITY_PASSWORD, then re-run the workflow",
         "ref": "blocking_item",
         "note": "The entitlement step signs in before the benchmark, which obtains the access token the Licensing Client needs."},
        {"order": 2,
         "action": "Run rung 2 on a machine with Unity 6000.5.9f1",
         "command": "Tools/run_bench.sh --tier linux --attempt 2 --window-scope cells --window-radius 1",
         "note": "Replaces the model in M1.json rung_2 with a measured verdict. Check quietness_linux.txt is 'quiet' before believing it."},
        {"order": 3,
         "action": "Run rung 2 with the TILE scope once the benchmark world is wider than 3x3",
         "command": "Tools/run_bench.sh --tier linux --attempt 2 --window-scope tiles --window-radius 1",
         "note": "Also requires the harness to step more than one tile, which it currently does not."},
        {"order": 4,
         "action": "If the sign-in fails (2FA, or a machine-bound entitlement), switch to UNITY_SERIAL + UNITY_EMAIL + UNITY_PASSWORD",
         "note": "Re-issues the licence for the runner and needs no file. Needs a Plus/Pro seat and consumes an activation per run."},
    ]

    claims = doc.setdefault("unverified_claims", [])
    claims[:] = [c for c in claims if c.get("claim") not in (
        "The rung-2 active water window reduces the per-frame cost",
        "The rung-2 code compiles",
    )]
    claims.append({
        "claim": "The rung-2 code compiles",
        "status": "UNVERIFIED",
        "reason": (
            "No Unity editor, no dotnet, no mono and no docker exist in the "
            "environment the revision was authored in. The new C# has never been "
            "compiled anywhere."
        ),
    })
    claims.append({
        "claim": "The rung-2 active water window reduces the per-frame cost",
        "status": "MODELLED ONLY - NOT MEASURED",
        "reason": (
            "A two-term cost model fitted to the committed rung-0 and rung-1 p50 "
            "values predicts all three scenarios under the 1.500 ms threshold, with "
            "C_FastForward passing by only 0.383 ms on a fixed cost of 1.116 ms. Two "
            "points fitted two unknowns, so this is a prediction and not evidence."
        ),
    })

    doc["one_line"] = (
        "Rung 2 is implemented and committed but unmeasured - the authoring "
        "environment has no Unity editor - so its cost is a labelled model, not a "
        "verdict. The two committed local verdicts remain a BUDGET FAIL; the CI gate "
        "still has no verdict and nothing has ever been compiled in CI."
    )
    return doc


def main():
    model = load_model()

    cs = 0
    for dirpath, _dirnames, filenames in os.walk(os.path.join(ROOT, "Assets")):
        cs += sum(1 for f in filenames if f.endswith(".cs"))

    m1 = json.load(open(M1))
    handoff = json.load(open(HANDOFF))

    m1 = update_m1(m1, model)
    handoff = update_handoff(handoff, model, cs)

    for path, doc in ((M1, m1), (HANDOFF, handoff)):
        with open(path, "w") as fh:
            json.dump(doc, fh, indent=2, ensure_ascii=False)
            fh.write("\n")
        print(f"wrote {os.path.relpath(path, ROOT)}")

    print(f"Assets/**/*.cs count: {cs}")


if __name__ == "__main__":
    main()
