#!/usr/bin/env python3
"""
Rung 2 cost model, fitted to the two committed verdicts.

WHY A MODEL AND NOT A MEASUREMENT
---------------------------------
Rung 2 cannot be measured on this machine: there is no Unity editor in the
sandbox, and the gate runner now refuses to start without one. So the honest
options are (a) report nothing, or (b) report what the committed numbers
*imply*, labelled as a model with its assumptions written down.

(b) is worth doing because it is falsifiable: the fit uses the two measured
rungs, so a reader can check the arithmetic and the eventual real rung-2
verdict can be diffed against it in one line.

THE MODEL
---------
Per-step cost is decomposed into a term that does not scale with the active set
(S) and a term that does (W, the per-active-cell work):

    T(res, window) = S + W(res) * active_fraction

W is taken to scale with the number of CELLS in the tile, which is the
assumption rung 1's own result tests and roughly confirms: halving the
resolution cuts the cell count 3.97x, and the measured p50 speedup is 2.4-3.3x.

S is then solved from the two committed points. With W(129) = W(257)/4:

    T(257, none) = S + W257
    T(129, none) = S + W257/4
    =>  S = (4*T129 - T257) / 3

S is the number that matters most for rung 2, because a cell window of radius 1
at res 129 admits 9 cells of 16641 - an active fraction of 0.054%, at which the
windowed term is negligible and the step cost is essentially S ALONE. The model
therefore says rung 2's floor is the fixed cost, not zero, and it puts a number
on that floor.

WHAT THE MODEL DOES NOT SAY
---------------------------
It says nothing about whether rung 2 is a good idea. A cell window of radius 1
is a pinhole, not a game configuration: it leaves 99.95% of the tile frozen.
The rung the ladder actually names is TILE-based, which is a different mechanism
with a different number, and which the current 2x2 benchmark world cannot
measure at all - see tile_grid below.
"""

import json
import sys

# Committed rung-0 and rung-1 p50 values, verbatim from the verdict files.
# Blobs 179076dd... (rung 0) and 51c5f4de... (rung 1).
MEASURED = {
    "A_StaticSoak": {"r0": 5.062, "r1": 1.544},
    "B_StepFlood": {"r0": 3.633, "r1": 1.159},
    "C_FastForward": {"r0": 5.072, "r1": 2.105},
}

THRESHOLD_P50 = 1.500

RES_HI = 257   # rung 0 tile_res
RES_LO = 129   # rung 1 tile_res
RADIUS = 1

# Shipping slice, from the Deliverable 2 world concept: 8x8 tiles of 250 m.
SLICE_TILES_PER_SIDE = 8


def active_cells(res, radius):
    """Cells admitted by a Chebyshev cell window of `radius` centred on the tile."""
    mid = res // 2
    lo = max(0, mid - radius)
    hi = min(res - 1, mid + radius)
    return (hi - lo + 1) ** 2


def active_tiles_max(tiles_per_side, radius):
    """Worst-case active tile count over every camera tile position."""
    best = 0
    for y in range(tiles_per_side):
        for x in range(tiles_per_side):
            xs = max(0, x - radius)
            xe = min(tiles_per_side - 1, x + radius)
            ys = max(0, y - radius)
            ye = min(tiles_per_side - 1, y + radius)
            best = max(best, (xe - xs + 1) * (ye - ys + 1))
    return best


def active_tiles_at(tiles_per_side, radius, x, y):
    xs = max(0, x - radius)
    xe = min(tiles_per_side - 1, x + radius)
    ys = max(0, y - radius)
    ye = min(tiles_per_side - 1, y + radius)
    return (xe - xs + 1) * (ye - ys + 1)


def main():
    cells_hi = RES_HI * RES_HI
    cells_lo = RES_LO * RES_LO
    cell_ratio = cells_hi / cells_lo

    frac_cells_r2 = active_cells(RES_LO, RADIUS) / cells_lo

    slice_tiles = SLICE_TILES_PER_SIDE * SLICE_TILES_PER_SIDE
    at_max = active_tiles_max(SLICE_TILES_PER_SIDE, RADIUS)
    at_corner = active_tiles_at(SLICE_TILES_PER_SIDE, RADIUS, 0, 0)

    per_scenario = []
    for name, m in MEASURED.items():
        r0, r1 = m["r0"], m["r1"]
        # Solve S from the two points, with W(129) = W(257)/4.
        fixed = (4.0 * r1 - r0) / 3.0
        w_lo = r1 - fixed                      # per-active-cell work at res 129
        t_r2 = fixed + w_lo * frac_cells_r2    # cell-windowed rung 2, res 129
        per_scenario.append({
            "scenario": name,
            "measured_p50_rung0_ms": r0,
            "measured_p50_rung1_ms": r1,
            "model_fixed_cost_ms": round(fixed, 6),
            "model_tile_work_rung1_ms": round(w_lo, 6),
            "model_p50_rung2_cells_ms": round(t_r2, 6),
            "model_meets_p50_threshold": t_r2 <= THRESHOLD_P50,
            "model_margin_under_threshold_ms": round(THRESHOLD_P50 - t_r2, 6),
            "model_speedup_rung1_to_rung2": round(r1 / t_r2, 4),
            "model_speedup_rung0_to_rung2": round(r0 / t_r2, 4),
        })

    out = {
        "kind": "MODEL - not a measurement",
        "why_modelled": (
            "No Unity editor exists in this environment and the gate runner "
            "refuses to start without one, so rung 2 could not be measured. "
            "These numbers are the committed rung-0 and rung-1 values propagated "
            "through a two-term cost model whose assumptions are stated here."
        ),
        "model": {
            "form": "T = S + W(res) * active_fraction",
            "assumption": "W scales with the cell count of the tile",
            "solved_for": "S, the per-step cost that does not scale with the active set",
            "fit_inputs": "the committed rung-0 and rung-1 p50 values for each scenario",
            "identifiability": (
                "Two points and two unknowns, so the fit is exact by construction "
                "and is NOT evidence that the model is right. It is falsifiable: a "
                "real rung-2 verdict can be diffed against it."
            ),
        },
        "window_arithmetic": {
            "cell_window_res129": {
                "active_cells": active_cells(RES_LO, RADIUS),
                "cells_in_tile": cells_lo,
                "active_fraction": round(frac_cells_r2, 9),
                "frozen_fraction": round(1.0 - frac_cells_r2, 9),
            },
            "cell_window_res257": {
                "active_cells": active_cells(RES_HI, RADIUS),
                "cells_in_tile": cells_hi,
                "active_fraction": round(active_cells(RES_HI, RADIUS) / cells_hi, 9),
            },
            "cell_count_reduction_rung0_to_rung1": round(cell_ratio, 4),
            "cell_count_reduction_rung1_to_rung2": round(cells_lo / active_cells(RES_LO, RADIUS), 1),
            "tile_grid": {
                "tiles_per_side": SLICE_TILES_PER_SIDE,
                "world_tiles": slice_tiles,
                "radius_tiles": RADIUS,
                "active_tiles_max": at_max,
                "active_tile_fraction_max": round(at_max / slice_tiles, 6),
                "tile_reduction_factor_max": round(slice_tiles / at_max, 4),
                "active_tiles_corner": at_corner,
                "benchmark_world_tiles_per_side": 2,
                "benchmark_world_tiles": 4,
                "benchmark_active_tiles": active_tiles_at(2, RADIUS, 0, 0),
                "benchmark_tile_reduction_factor": 1.0,
                "note": (
                    "In a 2x2 world radius 1 covers every tile, so rung 2's TILE "
                    "mechanism has zero effect in the benchmark world. The 7.11x "
                    "reduction is a property of the shipping 8x8 slice and the "
                    "harness cannot measure it as built."
                ),
            },
        },
        "scaling_check": {
            "rung0_to_rung1_cell_reduction": round(cell_ratio, 4),
            "rung0_to_rung1_measured_speedup_p50": {
                "A_StaticSoak": 3.3, "B_StepFlood": 3.1, "C_FastForward": 2.4,
            },
            "finding": (
                "A 3.97x reduction in cells bought 2.4-3.3x in time, not 3.97x. "
                "The scaling is sublinear in the same direction and by roughly the "
                "same amount as the model's fixed term predicts, which is the one "
                "piece of independent support the model has."
            ),
        },
        "per_scenario": per_scenario,
    }

    json.dump(out, sys.stdout, indent=2)
    print()


if __name__ == "__main__":
    main()
