# Committed gate reports

One file per gate run that produced a verdict. This directory is the project's
**decision record** for the water gate — the document a future reader consults to
understand why the Android tier was or was not delayed, and which rung of the
ladder was taken and why.

The report *format* is `docs/GATE_REPORT_TEMPLATE.md`. This file is only about
**naming, provenance and retention**.

---

## 1. The filename pattern

```
gate-<yyyymmdd>-<milestone>-<tier>-<run>.md
```

| Field | Width | Allowed values | Example |
|---|---|---|---|
| `gate` | literal | always `gate` | `gate` |
| `<yyyymmdd>` | 8 digits | the **run** date, UTC, zero-padded | `20261005` |
| `<milestone>` | 3 chars | `M01`…`M16` — see the mapping in §2 | `M01` |
| `<tier>` | literal | `linux` or `android` | `linux` |
| `<run>` | 4 digits | the GitHub Actions `run_number`, zero-padded | `0012` |

### Why this field order

**The date comes first because the requirement is that a plain `ls` sorts
chronologically.** Any pattern that puts the milestone first sorts by milestone
name instead, and milestone names do not sort in plan order — `M10` sorts before
`M2` in every byte-wise comparison. Putting the date first makes the directory
listing a timeline, which is what a decision record is for.

Within one day the remaining fields break ties in the order a reader cares
about: which milestone, which tier, which attempt.

> **This differs from the illustrative example `gate-<milestone>-<tier>-<yyyymmdd>-<run>.md`.**
> That order cannot satisfy the chronological-sort requirement, so it was not
> adopted. If you would rather group by milestone than read a timeline, the
> alternative is `gate-<milestone>-<tier>-<yyyymmdd>-<run>.md` — but then the
> listing is grouped, not chronological, and `M10`–`M16` appear between `M1` and
> `M2`. Pick one; do not mix them.

### Why every numeric field is zero-padded

Unpadded numbers sort wrong. `gate-20261005-M1-...` and `gate-20261005-M10-...`
interleave, and run `9` sorts after run `10`. Zero-padding to a fixed width makes
byte-wise sort and numeric sort agree, which is the whole point of the pattern.

---

## 2. The milestone token

The plan's milestone names contain hyphens (`M3-4`, `M8-9`, `M12-13`), which
would collide with the filename's own delimiter. The token is therefore the
**first week number of the milestone**, zero-padded to two digits. Every
milestone's first week is unique, so the mapping is unambiguous.

| Plan milestone | Weeks | Token | Example filename |
|---|---|---|---|
| M1 — Water Gate | 1 | `M01` | `gate-20261005-M01-linux-0012.md` |
| M2 — Grey-box Terrain and Streaming | 2 | `M02` | `gate-20261012-M02-linux-0014.md` |
| M3-4 — Edit Pipeline and Journal | 3–4 | `M03` | `gate-20261019-M03-linux-0016.md` |
| M5 — Water in the World | 5 | `M05` | `gate-20261102-M05-linux-0018.md` |
| M6 — Slice Geometry Locked | 6 | `M06` | `gate-20261109-M06-linux-0020.md` |
| M7 — Instrument and Failure Messages | 7 | `M07` | `gate-20261116-M07-linux-0022.md` |
| M8-9 — Life and Succession | 8–9 | `M08` | `gate-20261123-M08-linux-0024.md` |
| M10 — Save Round-Trip | 10 | `M10` | `gate-20261214-M10-linux-0026.md` |
| M11 — Android Tier | 11 | `M11` | `gate-20261221-M11-android-0028.md` |
| M12-13 — Content Pass | 12–13 | `M12` | `gate-20261228-M12-linux-0030.md` |
| M14 — Art and Audio | 14 | `M14` | `gate-20270111-M14-linux-0032.md` |
| M15 — Performance | 15 | `M15` | `gate-20270118-M15-linux-0034.md` |
| M16 — Freeze and Playtest | 16 | `M16` | `gate-20270125-M16-linux-0036.md` |

There is no `M04`, `M09` or `M13` — those weeks belong to the two-week milestones
above them. A token that is not in this table is a typo.

---

## 3. Which fields come from where

A gate report mixes machine output with hand-entered environment facts. Keeping
the two apart is what makes the report reproducible.

### From the workflow run — copy, never retype

| Field | Source | Where it goes in the report |
|---|---|---|
| Run number | the Actions run | filename `<run>`, and §6 `Report committed at` context |
| Run URL | `https://github.com/bythewayz66-glitch/petrichor/actions/runs/<id>` | header block, and §6 |
| Commit SHA | `github.sha` — the job summary prints it | §1.1 `Commit SHA` |
| Tier | the `tier` matrix value | filename `<tier>`, header block, §2.1 or §2.2 |
| Ladder attempt | the `attempt` dispatch input | §1.1 `Ladder attempt` |
| Editor version | the `Resolve the pinned editor version` step, read from `ProjectSettings/ProjectVersion.txt` | §1.2 `Unity editor` |
| Verdict | `BenchmarkResults/verdict_<tier>.txt`, and the job summary | §4 and §6 |
| Checker exit code | the `Check thresholds` step output | §4.2 |
| Verdict file contents | `BenchmarkResults/verdict_<tier>.txt` | §4.1, pasted **unedited** |
| Scenario numbers | `BenchmarkResults/summary_<Scenario>.json` | §2.1–§2.4, §3.1 |
| NUint result | `BenchmarkResults/results_<tier>.xml` | §3 |
| Artifact names | `water-gate-samples-<tier>-<sha>`, `water-gate-verdict-<tier>-<sha>` | §4.1 provenance note |

### From the machine — hand-entered, and only from the machine that ran the gate

| Field | Where it goes |
|---|---|
| OS, kernel, CPU, RAM, GPU vendor/model, GPU driver, Vulkan runtime | §1.3 |
| Device model, SoC, GPU, Android version, API level, RAM, storage, thermal state, ambient temperature, charging | §1.4 (Android tier only) |
| Resolved package versions | §1.2 — read off **Package Manager**, not off a design document |
| Thresholds file SHA | §1.5 — `git hash-object Tools/bench_thresholds.json` |
| Rung decision and its downstream effects | §5 |
| Sign-off | §6 |

> **The GPU driver version is not optional.** Vulkan driver behaviour on Linux is
> not uniform across vendors, and a frame-time number without a driver attached
> cannot be compared against a later run.

---

## 4. The verdict vocabulary

The workflow emits exactly these tokens. The report's §4 verdict line must be one
of the first four, or the run is not a report.

| Checker exit code | Token the workflow emits | Build | Reportable? |
|---|---|---|---|
| `0` | `PASS` | green | **yes** |
| `1` | `BUDGET FAIL` | green | **yes** — §5 must name the rung |
| `2` | `HARD FAIL` | **fails** | **yes** — the ladder does not apply |
| `3` | `INPUT ERROR` | **fails** | **yes** — the run is invalid |
| anything else | `UNKNOWN` | **fails** | **no** — see §5 |
| no verdict file | `not produced` | — | **no** — see §5 |

`BUDGET FAIL` is the only token that carries a rung number in the report
(`BUDGET FAIL — RUNG n`). The rung is **named by the checker, not chosen**:
`--attempt` says how many rungs are already applied and the checker returns the
next one.

---

## 5. The no-verdict case — the rule that matters most

> **Never commit a report for a run whose `gate` job was skipped.**

A skipped gate means no benchmark ran, no checker ran, and no verdict exists. A
report written for such a run would record a pass that never happened, in the one
document the project treats as its decision record. That is worse than having no
report at all.

The same rule covers `UNKNOWN` and `not produced`: if the checker did not return
`0`–`3`, there is nothing to report. Fix the run and re-run it.

### Why this rule exists here specifically

The `preflight` job exits **green** when no Unity licence secret is present, and
the `gate` job is then **skipped**. The workflow therefore shows a green tick
while doing nothing. That was the state of the first 18 runs.

**It is no longer the state.** A `UNITY_LICENSE` secret was added on 2026-10-03,
and the `gate` job executed in CI for the first time (run 37107565152). It
**failed on activation** — the secret holds an entitlement XML, not a ULF — so
the checker never ran and **no CI report has been written**. The run is red for a
real reason, not skipped.

A green run is not a pass. Do not read one as a pass, and do not write a report
from one. Equally, a red run whose failure is an input error is not a solver
result — do not write a report from that either.

### What to do instead

1. Replace the `UNITY_LICENSE` secret with a real `.ulf` — see `docs/HANDOFF.md` §3.
2. Re-run the workflow, or run the gate locally:
   ```bash
   Tools/run_bench.sh --tier linux   --attempt 0
   Tools/run_bench.sh --tier android --attempt 0
   ```
3. Only then write the report from `docs/GATE_REPORT_TEMPLATE.md`.

---

## 6. Retention — superseded reports are kept

**Never delete a gate report.** A later report supersedes an earlier one; it does
not replace it.

| Situation | What to do |
|---|---|
| A re-run produces a different verdict | Commit the new report. Leave the old one. |
| A rung was taken and the re-run passes | Commit the re-run. The `BUDGET FAIL — RUNG n` report is what records *why* the rung was taken. |
| A report is found to be wrong | Commit a corrected report with a later date. Do not edit history in place. |
| A report was committed for a skipped run | **Delete it** — it is the one exception, because it records a verdict that never existed. |

The reason is the same one that makes the report a decision record: the template's
§5.2 asks what a rung changed downstream, and §6 asks whether the plan was
re-baselined. Those questions are only answerable if the earlier report still
exists. A deleted report turns a decision back into a rumour.

---

## 7. Worked example

### Filename

```
docs/gate-reports/gate-20261005-M01-linux-0012.md
```

Reads as: the water gate, run on 2026-10-05, for milestone M1, on the Linux tier,
Actions run number 12.

### Header block, filled in

```markdown
# PETRICHOR — Week-One Water Gate Report

| | |
|---|---|
| **Gate** | M1 — Water Gate |
| **Tier** | `linux` |
| **Report date** | `2026-10-05` |
| **Author** | `<name>` |
| **Status** | `FINAL` |

| | |
|---|---|
| **Run** | [#12](https://github.com/bythewayz66-glitch/petrichor/actions/runs/0000000000) |
| **Commit under test** | `0000000000000000000000000000000000000000` |
| **Ladder attempt** | `0` |
| **LFS art fetched** | `false` |
```

> The run URL and commit SHA above are **placeholders**. No gate run has ever
> produced a verdict, so no real values exist to show. Replace both with the
> values from the job summary of the run you are reporting.

### The rest of the report

Copy `docs/GATE_REPORT_TEMPLATE.md` and fill every `<...>` placeholder. A
placeholder left in a committed report is an incomplete gate, and an incomplete
gate is not a verdict.

---

## 8. Checklist before committing a report

- [ ] The `gate` job **executed** — it was not skipped. Check the job list, not the run's green tick.
- [ ] The filename matches `gate-<yyyymmdd>-<milestone>-<tier>-<run>.md` exactly.
- [ ] The milestone token is in the §2 table.
- [ ] Every `<...>` placeholder is filled. Search the file for `<` before committing.
- [ ] The verdict line states one of `PASS`, `BUDGET FAIL — RUNG n`, `HARD FAIL`, `INPUT ERROR`, with no hedging.
- [ ] The verdict file in §4.1 is pasted **unedited**.
- [ ] The checker exit code in §4.2 matches the verdict.
- [ ] §5 is either completed or explicitly marked `Not applicable — verdict was PASS`.
- [ ] The environment block names the GPU driver, and for Android the device model.
- [ ] The thresholds file hash is recorded.
- [ ] The commit SHA under test is recorded.
- [ ] If a rung was taken, §5.2 says what it changes downstream.
- [ ] The report is committed **before** the next week's work begins.
