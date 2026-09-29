# PETRICHOR — Week-One Water Gate Report

> **This is a template.** Copy it to `docs/gate-reports/M1-water-gate-<tier>-<YYYY-MM-DD>.md`
> and fill every `<...>` placeholder.
>
> **A placeholder left in a committed report is an incomplete gate, and an incomplete
> gate is not a verdict.** The whole value of a gate is that it is binary. A report
> that says "broadly within budget" or "close enough on desktop" has not returned a
> verdict, and everything downstream of it is still conditional.

| | |
|---|---|
| **Gate** | M1 — Water Gate |
| **Tier** | `<linux \| android>` |
| **Report date** | `<YYYY-MM-DD>` |
| **Author** | `<name>` |
| **Status** | `<DRAFT \| FINAL>` |

---

## 1. Environment

Every number in this report is meaningless without the machine that produced it.
A frame-time figure with no device attached is not reproducible, and an
irreproducible gate is a rumour.

### 1.1 Build under test

| Field | Value | Where it comes from |
|---|---|---|
| Commit SHA | `<40-char sha>` | `git rev-parse HEAD` |
| Branch | `<wk/01-water-gate>` | `git rev-parse --abbrev-ref HEAD` |
| Working tree clean | `<yes \| no>` | `git status --porcelain` — must be empty |
| Ladder attempt | `<0>` | the `--attempt` value passed to `run_bench.sh` |
| Rungs already applied | `<none \| 1: halve sim res \| ...>` | see §5 |

### 1.2 Editor and toolchain

| Field | Value | Where it comes from |
|---|---|---|
| Unity editor | `<6000.3.XXfY>` | `ProjectSettings/ProjectVersion.txt` |
| Editor revision | `<hash>` | `m_EditorVersionWithRevision` in the same file |
| Scripting backend | `<IL2CPP \| Mono>` | build profile |
| Graphics API | `<Vulkan \| OpenGL Core \| OpenGL ES 3.x>` | build profile |
| Development build | `<no>` | must be **no** — a development build is not a gate build |
| Burst | `<version>` | Package Manager |
| Collections | `<version>` | Package Manager |
| Mathematics | `<version>` | Package Manager |
| Test Framework | `<version>` | Package Manager |
| Test Framework Performance | `<version>` | Package Manager |

> **Record the resolved versions, not the manifest versions.** A version copied
> from a design document is not a version record. Read them off Package Manager
> on the machine that ran the gate.

### 1.3 Host machine

| Field | Value |
|---|---|
| OS | `<Ubuntu 24.04.1 LTS>` |
| Kernel | `<6.8.0-45-generic>` |
| CPU | `<model, core count, base clock>` |
| RAM | `<GB>` |
| GPU vendor | `<NVIDIA \| AMD \| Intel>` |
| GPU model | `<...>` |
| GPU driver | `<version>` |
| Vulkan runtime | `<version>` |

> **The driver version is not optional.** Vulkan driver behaviour on Linux is not
> uniform across vendors, and a frame-time number without a driver attached cannot
> be compared against a later run.

### 1.4 Reference device (Android tier only)

| Field | Value |
|---|---|
| Device model | `<...>` |
| SoC | `<...>` |
| Android version | `<...>` |
| API level | `<...>` |
| RAM | `<GB>` |
| Thermal state at start | `<cool \| warm>` |
| Ambient temperature | `<deg C>` |
| Charging | `<no>` — charging changes the thermal envelope |

> **The reference device must be named before the gate runs.** Every Android
> performance number in the design package is meaningless until one specific
> mid-range 2022–2023 class phone is recorded. This is decision #3 from
> Deliverable 4 and it is still open.

### 1.5 Test configuration

| Field | Value | Source |
|---|---|---|
| Tile resolution | `257` | `bench_thresholds.json` → `tiers.<tier>.tile_res` |
| Active tiles | `4` | `tiers.<tier>.tile_count` |
| Cell size | `250 / 256 = 0.9766 m` | derived: tile width / (res − 1) |
| Frame budget | `<16.67 \| 33.33>` ms | `tiers.<tier>.frame_budget_ms` |
| Target FPS | `<60 \| 30>` | `tiers.<tier>.target_fps` |
| Thresholds file | `Tools/bench_thresholds.json` | committed |
| Thresholds file SHA | `<sha>` | `git hash-object Tools/bench_thresholds.json` |

> **Record the thresholds file hash.** If the thresholds are edited after the run,
> the report no longer describes the gate that was actually run.

---

## 2. Scenario results

All timings are **milliseconds per solver step**, not per frame. Scenario C takes
3600 steps per sample and the harness divides by 3600, so C is directly comparable
with A and B.

### 2.1 Linux tier — thresholds p50 1.5 / p95 3.0 / max 6.0 ms

| Scenario | p50 (ms) | p95 (ms) | max (ms) | p50 limit | p95 limit | max limit | Result |
|---|---|---|---|---|---|---|---|
| **A — Static soak** | `<0.000>` | `<0.000>` | `<0.000>` | 1.5 | 3.0 | 6.0 | `<PASS \| FAIL>` |
| **B — Step flood** | `<0.000>` | `<0.000>` | `<0.000>` | 1.5 | 3.0 | 6.0 | `<PASS \| FAIL>` |
| **C — Fast-forward** | `<0.000>` | `<0.000>` | `<0.000>` | 1.5 | 3.0 | 6.0 | `<PASS \| FAIL>` |

### 2.2 Android tier — thresholds p50 2.5 / p95 5.0 / max 10.0 ms

| Scenario | p50 (ms) | p95 (ms) | max (ms) | p50 limit | p95 limit | max limit | Result |
|---|---|---|---|---|---|---|---|
| **A — Static soak** | `<0.000>` | `<0.000>` | `<0.000>` | 2.5 | 5.0 | 10.0 | `<PASS \| FAIL>` |
| **B — Step flood** | `<0.000>` | `<0.000>` | `<0.000>` | 2.5 | 5.0 | 10.0 | `<PASS \| FAIL>` |
| **C — Fast-forward** | `<0.000>` | `<0.000>` | `<0.000>` | 2.5 | 5.0 | 10.0 | `<PASS \| FAIL>` |

### 2.3 What each scenario is actually testing

| Scenario | Steps per sample | What it measures | Why it is separate |
|---|---|---|---|
| **A — Static soak** | 600 | Steady-state cost with one source and a settled field | The floor. If this fails, nothing else matters |
| **B — Step flood** | 600 | The transient after a large instantaneous inflow | A solver can be fast at rest and stutter on the transient |
| **C — Fast-forward** | 3600 | Stability and cost under the 3600× time toggle | A solver can pass A and B and fail C |

> **The three scenarios are not three measurements of the same thing.** A solver
> that passes A and B and fails C is a real and common outcome — the fast-forward
> path is where accumulated drift and the flux limiter's edge cases show up.

### 2.4 Headroom

| Scenario | p95 (ms) | p95 limit | Headroom | Headroom % |
|---|---|---|---|---|
| A | `<0.000>` | `<3.0>` | `<0.000>` | `<00.0%>` |
| B | `<0.000>` | `<3.0>` | `<0.000>` | `<00.0%>` |
| C | `<0.000>` | `<3.0>` | `<0.000>` | `<00.0%>` |

> **Headroom is the number to watch across runs, not the pass.** A scenario that
> passes at 2.9 ms against a 3.0 ms limit has no room for the content pass, and
> will fail in week 12 when the real terrain arrives.

---

## 3. Correctness

Correctness is evaluated **before** budget, deliberately. A fast wrong solver is
the most dangerous outcome this gate can produce, and the ordering makes it
impossible to mistake for a pass.

| Check | Limit | A | B | C | D | Result |
|---|---|---|---|---|---|---|
| Non-finite depth samples | `0` | `<0>` | `<0>` | `<0>` | `<0>` | `<PASS \| FAIL>` |
| Mass balance drift | `<= 0.1%` | `<0.000%>` | `<0.000%>` | `<0.000%>` | `<0.000%>` | `<PASS \| FAIL>` |
| Field hash non-zero | `true` | `<hash>` | `<hash>` | `<hash>` | `<hash>` | `<PASS \| FAIL>` |
| NUnit test result | all green | — | — | — | — | `<PASS \| FAIL>` |

### 3.1 Mass balance, stated precisely

The check is a **balance**, not a constancy check:

```
expected = mass_before + mass_inflow
drift    = |mass_after - expected| / expected
```

A field with an open source is *supposed* to gain water. Asserting constant mass
would fail a correct solver and pass a dry one.

| Scenario | mass_before (m3) | mass_inflow (m3) | expected (m3) | mass_after (m3) | drift |
|---|---|---|---|---|---|
| A | `<0.0000>` | `<0.0000>` | `<0.0000>` | `<0.0000>` | `<0.000%>` |
| B | `<0.0000>` | `<0.0000>` | `<0.0000>` | `<0.0000>` | `<0.000%>` |
| C | `<0.0000>` | `<0.0000>` | `<0.0000>` | `<0.0000>` | `<0.000%>` |

### 3.2 Determinism

| Check | Result |
|---|---|
| Scenario D — same input, same hash, two runs | `<PASS \| FAIL>` |
| Hash run 1 | `<hash>` |
| Hash run 2 | `<hash>` |

> **This asserts within-device determinism only.** Cross-device bit-identity is
> explicitly not promised: Burst compiles to different SIMD widths on x86-64 and
> ARM64. The save system depends on within-device determinism, which is what this
> check covers.

### 3.3 Why these are not budget failures

**NaN, mass drift and hash mismatch are hard fails, and the ladder cannot fix
them.** The ladder buys performance. Halving the resolution of a broken solver
produces a broken solver that is twice as fast.

| Condition | What it means | What to do |
|---|---|---|
| Non-finite depth | The flux limiter is not holding — a cell gave away more water than it held, depth went negative, and the next step divided by a negative depth | Fix the limiter. Do **not** walk the ladder |
| Mass drift | Water is being created or destroyed. Usually a boundary condition or an unaccounted inflow | Fix the balance. Do **not** walk the ladder |
| Hash mismatch | The solver is non-deterministic. The save system re-derives water from terrain and sources on load, so this breaks permanence | Fix the determinism. Do **not** walk the ladder |
| Red NUnit test | The harness asserts correctness before it writes a summary, so a red test means the solver is wrong | Fix the solver |

---

## 4. Verdict

> **State one of these four, with no hedging.**
>
> - `PASS` — every scenario within threshold on this tier, correctness clean
> - `BUDGET FAIL — RUNG <n>` — correctness clean, one or more scenarios over threshold
> - `HARD FAIL` — correctness broken; the ladder does not apply
> - `INPUT ERROR` — the checker could not read its inputs; the run is invalid

**VERDICT: `<PASS | BUDGET FAIL — RUNG n | HARD FAIL | INPUT ERROR>`**

### 4.1 Verdict file, verbatim

Paste the contents of `BenchmarkResults/verdict_<tier>.txt` here, unedited. This
is the machine output and it is the primary evidence for the verdict above.

```
<PASTE verdict_<tier>.txt HERE, UNEDITED>
```

### 4.2 Checker exit code

| Exit code | Meaning | Observed |
|---|---|---|
| `0` | PASS | `< >` |
| `1` | BUDGET FAIL — walk the named rung | `< >` |
| `2` | HARD FAIL — fix the solver | `< >` |
| `3` | INPUT ERROR — the run is invalid | `< >` |

---

## 5. Rung-ladder decision record

**Complete this section only if the verdict is `BUDGET FAIL`.** If the verdict is
`PASS`, write `Not applicable — verdict was PASS` and move to §6.

The rung is **named by the checker, not chosen**. `--attempt` tells the checker
how many rungs are already applied; it returns the next one. Do not improvise a
rung, and do not skip one.

| Rung | Action | Cost | Taken? |
|---|---|---|---|
| 1 | Halve the simulation resolution to 129. No visible loss — the render grid is unchanged | none visible | `<yes \| no>` |
| 2 | Shrink the active water window to the tiles within 1 of the camera | distant water stops updating | `<yes \| no>` |
| 3 | Channel graph on Android only. A real gameplay difference | Android water is not freely redirectable | `<yes \| no>` |
| 4 | Delay the Android tier. **NOT the design** | Linux-only slice | `<yes \| no>` |
| 5 | Water stops being redirectable. Revisit Deliverable 1 | a pillar | `<yes \| no>` |

### 5.1 Rung taken

| Field | Value |
|---|---|
| Rung number | `<n>` |
| Action | `<verbatim from bench_thresholds.json>` |
| Cost accepted | `<verbatim from bench_thresholds.json>` |
| Re-run attempt value | `<n>` |
| Re-run verdict | `<PASS \| BUDGET FAIL — RUNG n+1>` |
| Milestone M1 closed as | `<passed \| taken>` |

> **Both `passed` and `taken` are legitimate outcomes. Only an unstated one is a
> failure.** A gate that returns a rung has done its job: it has converted an
> unknown into a known cost, in week one, while the cost is still cheap.

### 5.2 What the rung changes downstream

| Affected | How |
|---|---|
| Deliverable 4 §02 (tier budgets) | `<unchanged \| updated: ...>` |
| Deliverable 5 §03 (week plan) | `<unchanged \| updated: ...>` |
| Risk register | `<R1 closed \| R1 escalated \| new risk: ...>` |
| Android tier (M11) | `<on schedule \| delayed \| out of the sixteen weeks>` |

---

## 6. Sign-off

| Field | Value |
|---|---|
| Verdict | `<PASS \| BUDGET FAIL — RUNG n \| HARD FAIL \| INPUT ERROR>` |
| Week 2 may begin | `<yes \| no>` |
| Plan re-baselined | `<no \| yes — see §5.2>` |
| Report committed at | `<sha>` |
| Signed | `<name>`, `<YYYY-MM-DD>` |

> **Week 2 does not begin until this report is committed.** The report is a project
> decision record: it is the document a future reader consults to understand why
> the Android tier was or was not delayed.

---

## Appendix A — Reproducing every number

Every figure in this report comes from one command. Nothing here is entered by
hand except the environment block.

```bash
# The whole gate, one tier at a time.
Tools/run_bench.sh --tier linux   --attempt 0
Tools/run_bench.sh --tier android --attempt 0
```

| Number | Produced by | Read from |
|---|---|---|
| p50 / p95 / max per scenario | `run_bench.sh` → harness | `BenchmarkResults/summary_<Scenario>.json` |
| `nan_count` | same | same |
| `mass_before` / `mass_inflow` / `mass_after` | same | same |
| `hash` | same | same |
| NUnit pass/fail | same | `BenchmarkResults/results_<tier>.xml` |
| Verdict and next rung | `Tools/check_thresholds.py` (called by `run_bench.sh`) | `BenchmarkResults/verdict_<tier>.txt` |
| Unity log | same | `BenchmarkResults/unity_<tier>.log` |

### A.1 Reading a single scenario's summary

```bash
cat BenchmarkResults/summary_A_StaticSoak.json
```

```json
{
  "scenario": "A_StaticSoak",
  "tier": "linux",
  "steps_per_sample": 600,
  "p50_ms": 0.941,
  "p95_ms": 1.872,
  "max_ms": 3.418,
  "nan_count": 0,
  "mass_before": 1284.5000,
  "mass_inflow": 0.0000,
  "mass_after": 1284.5000,
  "hash": 2918473625
}
```

### A.2 Re-running the checker alone

Useful when the thresholds changed and the harness does not need to run again.

```bash
python3 Tools/check_thresholds.py \
  --tier linux \
  --results-dir BenchmarkResults \
  --results-xml BenchmarkResults/results_linux.xml \
  --thresholds Tools/bench_thresholds.json \
  --attempt 0 \
  --out BenchmarkResults/verdict_linux.txt
echo "exit: $?"
```

### A.3 What is committed and what is not

| Artifact | Committed? | Why |
|---|---|---|
| `verdict_<tier>.txt` | **yes** | A project decision record |
| `summary_<Scenario>.json` | **yes** | Small, and the evidence behind the verdict |
| `results_<tier>.xml` | **yes** | Small, and the NUnit evidence |
| `unity_<tier>.log` | no | Large, machine-specific, regenerated |
| Raw per-run samples | no | Large, machine-specific, regenerated |

---

## Appendix B — Worked examples

Three filled examples, so the format is unambiguous. **These are illustrative
numbers, not measurements** — no gate has been run on this project yet.

### B.1 A PASS on the Linux tier

| Scenario | p50 | p95 | max | Result |
|---|---|---|---|---|
| A — Static soak | 0.941 | 1.872 | 3.418 | PASS |
| B — Step flood | 1.104 | 2.233 | 4.907 | PASS |
| C — Fast-forward | 1.088 | 2.190 | 5.112 | PASS |

Correctness: `nan_count` 0, drift 0.000%, hash non-zero, all tests green.

```
PETRICHOR week-one water gate
tier: linux (Linux desktop flagship)
attempt: 0

correctness: OK (no NaN, mass balanced, hash non-zero, all tests green)

scenario            p50      p95      max     (ms per step)
A_StaticSoak       0.941    1.872    3.418
B_StepFlood        1.104    2.233    4.907
C_FastForward      1.088    2.190    5.112

VERDICT: PASS
```

Exit code `0`. Week 2 begins. §5 is `Not applicable`.

### B.2 A BUDGET FAIL on the Android tier, naming rung 1

| Scenario | p50 | p95 | max | Result |
|---|---|---|---|---|
| A — Static soak | 2.310 | 4.880 | 9.140 | PASS |
| B — Step flood | 2.402 | **6.710** | **12.400** | **FAIL** |
| C — Fast-forward | 2.388 | **5.940** | **11.870** | **FAIL** |

Correctness: clean. This is a budget failure, not a hard fail.

```
PETRICHOR week-one water gate
tier: android (Android reference device (mid-range 2022-2023 class))
attempt: 0

correctness: OK (no NaN, mass balanced, hash non-zero, all tests green)

scenario            p50      p95      max     (ms per step)
A_StaticSoak       2.310    4.880    9.140
B_StepFlood        2.402    6.710   12.400
C_FastForward      2.388    5.940   11.870

VERDICT: BUDGET FAIL

  - B_StepFlood p95_ms = 6.710 ms (limit 5.000 ms)
  - B_StepFlood max_ms = 12.400 ms (limit 10.000 ms)
  - C_FastForward p95_ms = 5.940 ms (limit 5.000 ms)
  - C_FastForward max_ms = 11.870 ms (limit 10.000 ms)

NEXT RUNG: 1
  action: Halve the simulation resolution to 129. No visible loss - the render grid is unchanged.
  cost:   none visible

Re-run with --attempt 1 once applied.
```

Exit code `1`. §5 is completed: rung 1 taken, re-run with `--attempt 1`.

> **Note that A passed and B and C failed.** That is the expected shape of a
> marginal solver: steady state is fine, the transient and the fast-forward path
> are not. It is also why the three scenarios are separate.

### B.3 A HARD FAIL that is fast

| Scenario | p50 | p95 | max | Result |
|---|---|---|---|---|
| A — Static soak | 0.402 | 0.611 | 0.988 | within budget |
| B — Step flood | 0.418 | 0.640 | 1.104 | within budget |
| C — Fast-forward | 0.409 | 0.622 | 1.021 | within budget |

Every budget metric is comfortably inside its limit. The verdict is still
`HARD FAIL`, because correctness is evaluated first.

```
PETRICHOR week-one water gate
tier: android (Android reference device (mid-range 2022-2023 class))
attempt: 0

VERDICT: HARD FAIL

These are NOT budget failures. The ladder buys performance
and cannot buy correctness. Fix the solver.

  - A_StaticSoak: 17 non-finite depth samples (max 0)
  - B_StepFlood: mass balance off by 0.4800% (expected 1284.5000 m3, got 1278.3331 m3)
  - C_FastForward: field hash collapsed to zero
  - 1 test case(s) failed in the NUnit run: PET.Benchmarks.WaterSolverBenchmark.ScenarioA_StaticSoak
```

Exit code `2`. **Do not walk the ladder.** The solver is wrong, and a faster wrong
solver is worse than a slow correct one because it hides the problem longer.

> **This is the most dangerous outcome the gate can produce**, and the reason the
> checker evaluates correctness before budget. A report that only looked at the
> timing table would call this a pass.

---

## Appendix C — Checklist before committing this report

- [ ] Every `<...>` placeholder is filled. Search the file for `<` before committing.
- [ ] The verdict line states one of the four outcomes, with no hedging.
- [ ] The verdict file in §4.1 is pasted unedited.
- [ ] The checker exit code in §4.2 matches the verdict.
- [ ] §5 is either completed or explicitly marked not applicable.
- [ ] The environment block names the GPU driver and, for Android, the device model.
- [ ] The thresholds file hash is recorded.
- [ ] The commit SHA under test is recorded.
- [ ] If a rung was taken, §5.2 says what it changes downstream.
- [ ] The report is committed **before** week 2 begins.
