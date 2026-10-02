# PETRICHOR — Handoff

> **Read this first.** It is written for an agent or developer opening this
> repository with no memory of how it got here. Everything below is either a
> fact you can verify with one command, or a claim explicitly marked as
> unverified. Nothing in between.

| | |
|---|---|
| **Repository** | `bythewayz66-glitch/petrichor` (public since 2026-10-01) |
| **Branch** | `main` |
| **HEAD at handoff** | `fbef2d9c382edddd69a12f84c58c047abbdda9c5` |
| **Handoff written** | 2026-09-30 |
| **Engine** | Unity **6000.3.0f1** (changeset `d1870ce95baf`), URP |
| **Reference device** | Samsung Galaxy A54 5G (`SM-A546B`) |
| **Blocking item** | **No `UNITY_LICENSE` secret — the gate has never run.** See §3 |

---

## 1. Current state

### 1.1 What exists

| Thing | Count | Verified by |
|---|---|---|
| Issues | **144** | GraphQL `issues.totalCount` |
| Labels | **36** | GraphQL `labels.totalCount` |
| Milestones | **13** | GraphQL `milestones.totalCount` |
| Projects v2 board items | **144** | GraphQL `projectV2.items.totalCount` |
| Assemblies (`.asmdef`) | 12 | `find Assets -name '*.asmdef'` |
| C# source files | 14 | `find Assets -name '*.cs'` — 6 under `Assets/Scripts/Water`, 8 under `Assets/Tests/Benchmarks` |
| Committed gate reports | **0** | `ls docs/gate-reports/` — the directory exists and holds its convention README only |

The board, the issue set and the milestone set are **mutually consistent**: 144
board items for 144 issues, and every issue carries exactly one milestone.

### 1.2 The issue set, by milestone

Every week has nine tickets summing to **5.0 days**. The three two-week
milestones carry eighteen.

| Milestone | Weeks | Issues | Days |
|---|---|---|---|
| M1 — Water Gate | 1 | 9 | 5.0 |
| M2 — Grey-box Terrain and Streaming | 2 | 9 | 5.0 |
| M3-4 — Edit Pipeline and Journal | 3–4 | 18 | 10.0 |
| M5 — Water in the World | 5 | 9 | 5.0 |
| M6 — Slice Geometry Locked | 6 | 9 | 5.0 |
| M7 — Instrument and Failure Messages | 7 | 9 | 5.0 |
| M8-9 — Life and Succession | 8–9 | 18 | 10.0 |
| M10 — Save Round-Trip | 10 | 9 | 5.0 |
| M11 — Android Tier | 11 | 9 | 5.0 |
| M12-13 — Content Pass | 12–13 | 18 | 10.0 |
| M14 — Art and Audio | 14 | 9 | 5.0 |
| M15 — Performance | 15 | 9 | 5.0 |
| M16 — Freeze and Playtest | 16 | 9 | 5.0 |
| | | **144** | **80.0** |

Issue numbers run **#1–#144** in week order: #1–#18 are weeks 1–2, #19–#36
weeks 3–4, #37–#45 week 5, #46–#54 week 6, #55–#63 week 7, #64–#72 week 8,
#73–#81 week 9, #82–#90 week 10, #91–#99 week 11, #100–#108 week 12,
#109–#117 week 13, #118–#126 week 14, #127–#135 week 15, #136–#144 week 16.

### 1.3 What does **not** exist

- **No gate report.** `docs/gate-reports/` exists and holds its convention
  README, but no report has been written from `docs/GATE_REPORT_TEMPLATE.md`.
- **No benchmark result of any kind.** No `BenchmarkResults/`, no `verdict_*.txt`,
  no `summary_*.json`. The harness has never executed.
- **No art.** `Assets/Art/` contains only its `.gitignore` and `.gitattributes`.
  No purchased packs have been imported.
- **No scenes, prefabs, ScriptableObjects or shaders.** The `Assets/` tree is
  code and assembly definitions only.
- **No `Packages/packages-lock.json`.** The manifest exists; the lock file is
  written by the editor on first open.

---

## 2. Environment setup

### 2.1 Editor

```
m_EditorVersion: 6000.3.0f1
m_EditorVersionWithRevision: 6000.3.0f1 (d1870ce95baf)
```

Install **Unity 6.3 LTS** via Unity Hub with **Linux Build Support (IL2CPP)**,
**Android Build Support** (bundled OpenJDK/SDK/NDK) and optionally **Linux Build
Support (Mono)**. Do not open the project before the modules are installed.

> **The changeset is Unity's published value, not a value read off a machine.**
> If you install a different 6.3 patch, open the project once and let Unity
> rewrite `ProjectSettings/ProjectVersion.txt`, then copy both lines into
> `README.md`. Do not hand-edit the changeset to match a version you have not
> installed — the editor will re-import the whole project on every open.

### 2.2 Packages

Read the resolved versions off **Package Manager** after first open and fill in
the README's package table. Policy:

| Package | Policy |
|---|---|
| `com.unity.inputsystem` | Pin **1.19.0** |
| `com.unity.render-pipelines.universal` | **Do not pin** — editor-coupled |
| `com.unity.addressables` | Take the **Recommended** tag, not Latest |
| `com.unity.test-framework.performance` | Must also be in the manifest's `testables` array, or its attributes silently fail to resolve |
| Burst / Collections / Mathematics / Test Framework | Editor bundled |

### 2.3 Git configuration — four commands, once per clone

```bash
git lfs install
git config blame.ignoreRevsFile .git-blame-ignore-revs
git config merge.unityyamlmerge.name "Unity SmartMerge"
git config merge.unityyamlmerge.driver "<UNITYYAMLMERGE> merge -p --force %O %B %A %A"
git config merge.unityyamlmerge.recursive binary
```

`<UNITYYAMLMERGE>` is the full path inside your editor install — Unity does not
put it on `PATH`, and the path contains the editor version:

| Platform | Path |
|---|---|
| Windows | `C:\Program Files\Unity\Hub\Editor\<version>\Editor\Data\Tools\UnityYAMLMerge.exe` |
| macOS | `/Applications/Unity/Hub/Editor/<version>/Unity.app/Contents/Tools/UnityYAMLMerge` |
| Linux | `~/Unity/Hub/Editor/<version>/Editor/Data/Tools/UnityYAMLMerge` |

Verify it resolves — a wrong path makes git fall back to a line-based merge on
the next conflicting `.unity` file, which produces a **corrupt scene rather than
a conflict marker**:

```bash
git config --get merge.unityyamlmerge.driver
```

`merge=unityyamlmerge` is already declared per file type in `.gitattributes` for
`.unity`, `.prefab`, `.asset` and `.mat`. The attribute and the driver are two
halves of one mechanism; without both, neither does anything.

### 2.4 Editor settings to verify after first open

- `Edit → Project Settings → Editor`: **Asset Serialization = Force Text**,
  **Version Control Mode = Visible Meta Files**
- `Player → Other Settings`: **Color Space = Linear**,
  **Api Compatibility Level = .NET Standard 2.1**

---

## 3. The one blocking item

**There is no `UNITY_LICENSE` secret on the repository, so the `gate` job in
`.github/workflows/water-gate.yml` has never executed.** The `preflight` job
detects the absence, writes a skip notice, and exits green — so the workflow
shows a green tick while doing nothing. Do not read a green run as a pass.

Verified: `GITHUB_LIST_REPOSITORY_SECRETS` returns `total_count: 0`. No
`UNITY_LICENSE`, `UNITY_SERIAL`, `UNITY_EMAIL` or `UNITY_PASSWORD` exists.

### To unblock it

1. Activate a personal licence on a machine with the editor installed, so Unity
   writes `Unity_lic.ulf`:

   | Platform | Path |
   |---|---|
   | Linux | `~/.local/share/unity3d/Unity/Unity_lic.ulf` |
   | Windows | `C:\ProgramData\Unity\Unity_lic.ulf` |
   | macOS | `/Library/Application Support/Unity/Unity_lic.ulf` |

2. Copy **the whole file**, including the `<?xml ... ?>` declaration and the
   closing `</root>` tag. A partial copy is the most common cause of a failed
   activation.
3. Go to **`https://github.com/bythewayz66-glitch/petrichor/settings/secrets/actions`**
   → **New repository secret**.
4. Name it **exactly `UNITY_LICENSE`**. A typo leaves the gate skipped and the
   run green — the failure mode that wastes an afternoon.
5. Re-run the workflow. The `gate` job will execute.

**Alternative (Plus/Pro):** `UNITY_SERIAL` + `UNITY_EMAIL` + `UNITY_PASSWORD`.
The personal route is preferred: one secret, and no account password in CI.

> A personal licence is machine-bound. GameCI's activation step re-issues it for
> the runner. If activation fails, the usual cause is that the licence is active
> on too many machines — deactivate one from the Unity account page and retry.

---

## 4. What to do next, in order

### Step 1 — Unblock the gate (§3)

Nothing downstream is meaningful until the gate returns a verdict. Week 1 is a
**gate**, not a week of work: it decides whether the water solver can run on a
mid-range phone, and it can re-baseline the entire plan.

### Step 2 — Run the gate

```bash
Tools/run_bench.sh --tier linux   --attempt 0
Tools/run_bench.sh --tier android --attempt 0
```

Then write the report from `docs/GATE_REPORT_TEMPLATE.md` into
`docs/gate-reports/gate-<yyyymmdd>-<Mnn>-<tier>-<run>.md` and commit it. The
naming convention is defined in `docs/gate-reports/README.md`.

### Step 3 — Work the tickets in order

| Order | Issues | Milestone |
|---|---|---|
| 1 | **#1–#9** | M1 — Water Gate |
| 2 | **#10–#18** | M2 — Grey-box Terrain and Streaming |
| 3 | **#19–#36** | M3-4 — Edit Pipeline and Journal |
| 4 | **#37–#45** | M5 — Water in the World |
| 5 | **#46–#54** | M6 — Slice Geometry Locked |
| 6 | **#55–#63** | M7 — Instrument and Failure Messages |
| 7 | **#64–#81** | M8-9 — Life and Succession |
| 8 | **#82–#90** | M10 — Save Round-Trip |
| 9 | **#91–#99** | M11 — Android Tier |
| 10 | **#100–#117** | M12-13 — Content Pass |
| 11 | **#118–#126** | M14 — Art and Audio |
| 12 | **#127–#135** | M15 — Performance |
| 13 | **#136–#144** | M16 — Freeze and Playtest |

**Do not start week 2 until the M1 report is committed.** Every ticket from #10
onward is labelled `status:backlog` with the description *"Conditional on the
week-one gate. Do not start before it returns a verdict."*

### Step 4 — Expect the first compile to fail

The C# has never been compiled (§5). Budget an hour for the first open.

---

## 5. Known risks and unverified claims

**Read this section before trusting anything in the repository.**

| # | Claim | Status | What it means |
|---|---|---|---|
| 1 | The C# harness compiles | **UNVERIFIED** | No Unity licence exists in the environment where it was written, so no compiler ever saw it. Verified by inspection and API cross-check only. Expect minor fixes on first open — the most likely candidates are `using var` disposal order in the scenarios and the `[Performance]` attribute's namespace |
| 2 | The gate passes | **UNVERIFIED** | It has never run. No benchmark has executed, on any tier |
| 3 | The four scenarios are correctly parameterised | **UNVERIFIED** | 3000 settle steps, 500 samples, 3600× multiplier are reasoned, not tuned. If scenario A's settle loop is too short, A reports a transient cost and becomes a second B |
| 4 | The changeset `d1870ce95baf` | **PUBLISHED VALUE** | Unity's published changeset for 6000.3.0f1, not read off a machine. Confirm against your install |
| 5 | The thresholds are mirrored in two places | **KNOWN DRIFT RISK** | `Tools/bench_thresholds.json` and `BenchmarkScenarios.cs` both hold them. The harness writes the values it used into every summary, so drift is visible in results rather than inferred — but it is still two places to change |
| 6 | The reference device is owned | **UNCONFIRMED** | The A54 is a recommendation. The gate cannot run on a device nobody has |
| 7 | The LFS estimate (≈ 1.4 GB) | **ESTIMATE** | Not measured — no art has been imported. Bandwidth, not storage, is the binding constraint |

### Risks specific to the plan

- **The water solver is the project's largest technical risk.** URP ships no
  water system, so this is a simulation the project owns. Week 1 exists to find
  out whether it is feasible.
- **Terrain collider rebuild spikes** are the highest-*likelihood* risk in the
  register. A heightmap write triggers a collider rebuild and LOD recalculation;
  done in the input callback, every stroke drops a frame.
- **The maturity bake is the only place the journal may shrink.** A bug there is
  silent data loss rather than a crash — the world forgets something it grew.
- **Thermal throttling on Android is not a frame-time problem** you can profile
  your way out of. It is a sustained-load problem, and week 11 is the phase most
  likely to slip.

---

## 6. Conventions you must follow

Full detail in **`CONTRIBUTING.md`**. The parts that will bite you:

### The assembly rule

An assembly may only reference assemblies **below** it. It is enforced by
`.asmdef` references, so a violation is a **compile error**, not a review
comment. If you need something from a higher assembly, the thing you need is in
the wrong place — move it down, or pass it in as data.

```
PET.Core → PET.Data → PET.Edit → PET.Save
                          ↓
                      PET.Water → PET.Life → PET.World
                                                ↓
                                  PET.Player → PET.Render → PET.UI
                                                ↓
                                            PET.Boot
```

**The one hard prohibition: no `#if UNITY_ANDROID` anywhere under `PET.Edit` or
`PET.Save`.** The tiers differ only in `SO_Tier_*` values. A platform conditional
in the persistence layer means a save made on desktop can behave differently on
Android, which breaks the permanence pillar. Checked as a milestone exit
criterion (W11.8).

### The never-cut list

At any point, for any reason, under any schedule pressure, these three are never
cut:

1. **The three shaping tools** — terraforming, water, planting. They are the game.
2. **The save system.** Permanence is a pillar.
3. **The three failure messages.** They are the entire tutorial.

If a schedule can only be met by cutting one of these, **the schedule is wrong.
Say so, and re-baseline.**

### The cut order

Cuts come from the top of the written order in Deliverable 5 §08. **Do not skip a
row. Do not cut two things at once to avoid a hard conversation.** A cut is
recorded **in the same commit that takes it** — a cut that lives only in memory
gets quietly re-added in week fourteen and the schedule is wrong again with no
one knowing why.

### Commit messages

Conventional Commits, scope = assembly without the prefix (`water`, `edit`,
`save`, `bench`) or an area (`art`, `ci`, `docs`, `board`). Imperative mood,
lowercase after the colon, no full stop, under 72 characters.

### Branch strategy

Trunk-based. `main` always compiles and always passes the gate. Work on
`wk/NN-slug` (≤ 1 week), `exp/slug` (≤ 3 days, may be thrown away), `fix/slug`
(hours). **Never commit directly to `main`** except documentation and the board
data file. Tag every milestone.

### Before you commit

- [ ] Compiles with **zero errors** in the pinned editor
- [ ] `Tools/run_bench.sh linux` passes, if the change touches `PET.Water` or `PET.Core`
- [ ] EditMode and PlayMode tests pass
- [ ] No new `#if UNITY_ANDROID` under `PET.Edit` or `PET.Save`
- [ ] No new assembly reference pointing **upward**
- [ ] New assets follow the naming convention (`SC_`, `PF_`, `SO_`, `MAT_`, `TEX_`, `SH_`, `TL_`, `MUS_`, `AMB_`, `SFX_`, `ANIM_`, `GRP_`)
- [ ] New binary assets are LFS-tracked
- [ ] If a cut was taken, it is recorded in this commit

### When you fall behind

The trigger is **two consecutive milestones missing their exit criterion**. Not
one — one is normal. When it fires, **re-read Deliverable 5 §08 before taking any
cut.** On a one-person project the person who is behind is also the person
deciding what to cut, which is the worst possible arrangement for that decision.

---

## 7. Running the gate locally

### The whole gate, one tier at a time

```bash
Tools/run_bench.sh --tier linux   --attempt 0
Tools/run_bench.sh --tier android --attempt 0

# Measure a ladder rung instead of the reference solver
Tools/run_bench.sh --tier android --attempt 0 --solver channel-graph
```

`--attempt` names how many rungs of the ladder are already applied. The checker
returns the **next** rung; it does not choose one for you. `--solver` selects the
implementation under test and defaults to `heightfield`, so a run that omits it
measures exactly what it measured before the option existed. An unrecognised
value fails the NUnit run, which the checker reads as a HARD FAIL.

### Re-running the checker alone

Useful when the thresholds changed and the harness does not need to run again:

```bash
python3 Tools/check_thresholds.py \
  --tier linux \
  --results-dir BenchmarkResults \
  --results-xml BenchmarkResults/results_linux.xml \
  --thresholds Tools/bench_thresholds.json \
  --attempt 0 \
  --solver heightfield \
  --out BenchmarkResults/verdict_linux.txt
echo "exit: $?"
```

### The exit-code contract

| Code | Verdict | Build | Meaning |
|---|---|---|---|
| `0` | PASS | green | Every scenario within threshold |
| `1` | BUDGET FAIL | **green** | Walk the named rung. A budget miss is a project event, not a broken commit |
| `2` | HARD FAIL | **fails** | NaN, mass imbalance or a red test. **The ladder cannot fix these** |
| `3` | INPUT ERROR | **fails** | The checker could not read its inputs; the run is invalid |

**Correctness is evaluated before budget.** A fast wrong solver is the most
dangerous outcome the gate can produce, and the ordering makes it impossible to
mistake for a pass.

### Reading a scenario summary

```bash
cat BenchmarkResults/summary_A_StaticSoak.json
```

```json
{
  "scenario": "A_StaticSoak",
  "tier": "linux",
  "solver": "heightfield",
  "tile_res": 257,
  "tile_count": 4,
  "dt": 0.033333335,
  "samples": 500,
  "p50_ms": 0.941,
  "p95_ms": 1.872,
  "max_ms": 3.418,
  "nan_count": 0,
  "mass_before": 1284.5000,
  "mass_inflow": 0.0000,
  "mass_after": 1284.5000,
  "hash": 2918473625,
  "device": "unknown",
  "gpu": "unknown",
  "gpu_driver": "unknown"
}
```

> The numbers above are **illustrative, not measurements.** No gate has run.
> The `solver` key is what makes a run attributable: the checker refuses a
> results directory whose summaries disagree about it, and refuses one whose
> summaries have no `solver` key at all.

### Thresholds

| Tier | p50 | p95 | max | Frame budget | Target |
|---|---|---|---|---|---|
| Linux | 1.5 ms | 3.0 ms | 6.0 ms | 16.67 ms | 60 fps |
| Android | 2.5 ms | 5.0 ms | 10.0 ms | 33.33 ms | 30 fps |

All timings are **milliseconds per solver step**, not per frame. Scenario C takes
3600 steps per sample and the harness divides by 3600, so C is directly
comparable with A and B.

### The rung ladder

The rung is **named by the checker, not chosen**. Do not improvise one, and do
not skip one.

| Rung | Action | Cost |
|---|---|---|
| 1 | Halve the simulation resolution to 129 | none visible — the render grid is unchanged |
| 2 | Shrink the active water window to tiles within 1 of the camera | distant water stops updating |
| 3 | Channel graph on Android only | Android water is not freely redirectable |
| 4 | Delay the Android tier | Linux-only slice |
| 5 | Water stops being redirectable | a pillar — revisit Deliverable 1 |

---

## 8. Where the design documents live

The design package is the authority. If code and a design document disagree, one
of them is a bug and it is worth finding out which.

| # | Document | What it settles |
|---|---|---|
| 1 | Game Design Document | PETRICHOR, the Stilling, the Wakers, the Mineral Law. URP with two tiers, not HDRP |
| 2 | World Concept | The closed 4 km² basin, eight biomes, four percent alive, five seed guilds across four Refuges |
| 3 | Systems Breakdown | Seven loop states, fracture as a mask, water re-derived not stored, a journal not a snapshot |
| 4 | Technical Architecture | One project with tiers as data, twelve assemblies, sixty-four tile scenes, the week-one gate |
| 5 | Implementation Roadmap | Ten bootstrap steps, sixteen weeks, ten milestones, fourteen risks, a written cut order |
| — | Tracked Project Board | The sixteen weeks as tracked items, plus 144 tickets |
| — | Harness Specification | The week-one gate in full: solver interface, scenarios, thresholds, the checker, the ladder |
| — | Repository rules | Every `.gitignore` and `.gitattributes` rule, with the reason it exists |

---

## 9. The one-line version

**The plan is fully ticketed and the repository is consistent; the gate has never
run because there is no Unity licence secret, and the C# has never been
compiled. Add `UNITY_LICENSE`, run `Tools/run_bench.sh --tier linux --attempt 0`,
and write the report before starting week 2.**
