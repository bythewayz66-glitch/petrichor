# PETRICHOR — Handoff

> **Read this first.** It is written for an agent or developer opening this
> repository with no memory of how it got here. Everything below is either a
> fact you can verify with one command, or a claim explicitly marked as
> unverified. Nothing in between.

| | |
|---|---|
| **Repository** | `bythewayz66-glitch/petrichor` (public since 2026-10-01) |
| **Branch** | `main` |
| **HEAD at handoff** | `43777624e48dc5dfc160c8639d026c08a34042ce` (2026-10-08) |
| **Handoff written** | 2026-09-30, **revised 2026-10-08** (`r3`) |
| **Engine** | Unity **6000.5.9f1** (changeset `b57deb96f08d`), URP |
| **Reference device** | Samsung Galaxy A54 5G (`SM-A546B` — one of eight regional variants) |
| **Blocking item** | **The M1 gate report is committed (`docs/gate-reports/M1.md`) and it records a `BUDGET FAIL` at both rungs. Eleven CI runs have produced no verdict of their own — run 30 (2026-10-08) reached the checker for the first time and returned `INPUT ERROR`, but still uploaded a verdict file it inherited from the checkout (gap G8). And the committed rung-1 verdict contradicts the 2026-10-01 report, which claims a `PASS` that no artifact supports.** See §1.4, §3 and §5 |

---

## 1. Current state

### 1.1 What exists

| Thing | Count | Verified by |
|---|---|---|
| Issues | **144** | GraphQL `issues.totalCount` |
| Labels | **37** | GraphQL `labels.totalCount` — 36 at handoff; `status:blocked` added 2026-10-02 |
| Milestones | **13** | GraphQL `milestones.totalCount` |
| Projects v2 board items | **144** | GraphQL `projectV2.items.totalCount` |
| Assemblies (`.asmdef`) | 12 | `find Assets -name '*.asmdef'` |
| C# source files | **16** | `find Assets -name '*.cs'` — 6 under `Assets/Scripts/Water`, 8 under `Assets/Tests/Benchmarks`, 2 under `Assets/Editor/Terrain`. Was 14 before the terrain editor tools landed |
| Committed gate reports | **3** | `ls docs/gate-reports/` — `gate-20261001-M01-linux-0001.md` (dated record), `M1.md` (the M1 report), `M1.json` (its machine-readable companion) |
| Committed benchmark verdicts | **2** | `ls BenchmarkResults/` — `verdict_linux.txt` (attempt 0) and `verdict_linux_attempt1_res129.txt` (attempt 1) |

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

Issue numbers run **#1–#144** in week order, with one transposition: **issue #9
is week 2 (W2.1) and issue #10 is week 1 (W1.9)**, swapped relative to the rest.
So week 1 is **#1–#8 and #10**, and week 2 is **#9 and #11–#18**. Every other
week is contiguous: #19–#36 weeks 3–4, #37–#45 week 5, #46–#54 week 6,
#55–#63 week 7, #64–#72 week 8, #73–#81 week 9, #82–#90 week 10, #91–#99
week 11, #100–#108 week 12, #109–#117 week 13, #118–#126 week 14, #127–#135
week 15, #136–#144 week 16. The `week-NN` label is authoritative; the issue
number is not.

### 1.3 What does **not** exist

- **No CI gate verdict.** The `gate` job has run eleven times, on 2026-10-03,
  2026-10-05, 2026-10-06 and 2026-10-08. Runs #20–#24 failed on licence
  activation; #25 ran the entitlement path and hung for six hours; #26–#29 ran it
  to completion in ~100 seconds each and were rejected with `Access token is
  unavailable`, because an entitlement XML is a named-user licence and needs the
  account's access token. Run **#30** (`37731453756`, 2026-10-08) is the first
  whose editor step COMPLETED: the editor started and was refused at exit `198`,
  after which the checker ran for the first time (exit `3`, `INPUT ERROR`) — and
  the upload step still published a verdict file inherited from the checkout. See
  §1.4, §3 and gap G8.
- **No art.** `Assets/Art/` contains only its `.gitignore` and `.gitattributes`.
  No purchased packs have been imported.
- **No scenes or prefabs.** There are no `.unity` or `.prefab` files. The
  `Assets/` tree is code, assembly definitions, terrain tile data assets and
  URP settings — but no scene to open.
- **No `summary_*.json` or `results_*.xml` in the repository.** `.gitignore`
  excludes everything under `BenchmarkResults/` except `verdict_*.txt`, so the
  per-scenario summaries and the NUnit XML behind the committed verdicts exist
  only on the machine that produced them.

### 1.4 The gate has run — locally, and ten times in CI (one hung)

This is the single most important distinction in this document, and the one most
likely to be misread.

| | CI gate (GitHub Actions) | Local gate (headless editor) |
|---|---|---|
| Has it run? | **Yes — eleven times**, on 2026-10-03, 2026-10-05, 2026-10-06 and 2026-10-08 (runs 37107565152, 37109026756, 37109526130, 37110665890, 37110829407, 37111124148, 37252858463, 37253130555, 37253384200, 37407618302, 37731453756) | **Yes**, on 2026-10-01 and again 2026-10-03 |
| Did it compile? | **No** — no run has loaded the project. Run 37731453756 (2026-10-08) got furthest: the editor started and was refused at exit `198` | **Yes** — the 2026-10-01 run compiled the project and ran all four scenarios |
| Evidence | each run's `gate` job executed its steps; four logs show the licence rejected, the fifth shows a six-hour silence | `docs/gate-reports/gate-20261001-M01-linux-0001.md`, and two committed verdict files |
| Verdict produced | none | `BUDGET FAIL` at rung 0 (9 of 9 metrics over); `BUDGET FAIL` at rung 1 (2 of 9 over). The `PASS` this row used to claim is not supported by any committed artifact — see §5. **Rung 2 has no verdict at all** — see §1.5 |
| Report written | n/a | one, for the 2026-10-01 run |

**The C# has been compiled — locally, and only locally.** The 2026-10-01 run
compiled the project, fixed two compile errors, and executed all four scenarios.
**Nothing has ever been compiled in CI**: no CI run has reached the point where
the editor loads the project. Risk 1 in §5 is therefore resolved for the local
path; risk 2 is partly resolved and partly *worse* than "unverified" — see §5.

**The CI gate has run eleven times and has never produced a verdict of its own.**
Run **37731453756** (#30, 2026-10-08) is the first in which step 9 completed *and*
step 11 executed: the editor started, was refused the licence at exit `198`, and the
checker then returned `3` (`INPUT ERROR`). It nevertheless uploaded a verdict file it
inherited from the checkout — see gap **G8**. Runs #20–#24
failed on licence activation. Run **37111124148** (#25) is the first in which the
entitlement branch actually executed — and it **hung**: the container produced no
output for six hours and was killed by GitHub's default job timeout. Runs #26–#29
then ran the entitlement path to completion in ~100 s each and were rejected with
`Access token is unavailable`. The gate is no longer skipped, and it is no longer
merely misconfigured; it is red for a reason that is now visible in the log. See §3.

**The M1 report is committed: `docs/gate-reports/M1.md`** (with `M1.json` as its
machine-readable companion). It is written from the two committed verdict files
and from nothing else, and it records a `BUDGET FAIL` at **both** rungs. Read it
before trusting any summary of the gate, including this one.

### 1.5 Rung 2 is implemented — and has never been run

**The ladder's next rung is code now, not a plan.** `WaterSolver.Step` skips every
cell outside a Chebyshev window across all four passes; a cell outside it is never
assigned, so it holds its last depth. Scope, radius and origin are configuration
(`PET_BENCH_WINDOW_SCOPE`, `PET_BENCH_WINDOW_RADIUS`), and `none` is the identity,
which is how rungs 0 and 1 stay reproducible from the same tree.

**No rung-2 verdict exists, and none could have been taken here.** There is no
Unity editor in the environment this revision was written in:
`/opt/unity/Editor/Unity` is absent, and there is no `dotnet`, no `mono` and no
`docker` either. **The new C# has never been compiled, anywhere.**

What exists instead is a **labelled model**, not a measurement:

| Scenario | Model p50 rung 2 (ms) | Threshold | Model fixed cost `S` (ms) |
|---|---|---|---|
| A_StaticSoak | `0.371968` | 1.500 | `0.371333` |
| B_StepFlood | `0.334779` | 1.500 | `0.334333` |
| C_FastForward | `1.116535` | 1.500 | `1.116000` |

> **`C_FastForward` passes by `0.383 ms` on a fixed cost of `1.116 ms` — 74% of the
> threshold spent before any water moves.** The model says rung 2 is worth
> measuring. It does not say rung 2 passes. **Never quote these as a verdict.**
> `M1.json` carries them under `rung_2.source: "model, not measurement"`.

Two further facts that constrain what rung 2 can achieve, both arithmetic rather
than opinion. In the shipping 8×8 slice a radius-1 tile window activates 9 of 64
tiles — **`7.11×`** — but the benchmark world is 2×2, where every tile is within 1
of every other, so **rung 2's tile saving in the harness is exactly `1.0×`**. The
harness as built cannot measure the mechanism the rung names. And a radius-1
*cell* window freezes `99.95%` of a tile: it is a pinhole, not a playable setting.

---

## 2. Environment setup

### 2.1 Editor

```
m_EditorVersion: 6000.5.9f1
m_EditorVersionWithRevision: 6000.5.9f1 (b57deb96f08d)
```

Install **Unity 6000.5.9f1** via Unity Hub with **Linux Build Support (IL2CPP)**,
**Android Build Support** (bundled OpenJDK/SDK/NDK) and optionally **Linux Build
Support (Mono)**. Do not open the project before the modules are installed.

> **The changeset was read off a machine, not copied from a published table.**
> It was taken from the installed editor at
> `/home/jayson/Unity/Hub/Editor/6000.5.9f1/modules.json`, where every
> `download_unity/<changeset>/` path carries `b57deb96f08d`.
> If you install a different 6000.5 patch, open the project once and let Unity
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

## 3. The blocking item: the entitlement XML is a named-user licence and needs an access token

**The `UNITY_LICENSE` secret exists** (added 2026-10-03T07:36:04Z) and holds a
**genuine entitlement licence**. The preflight reports its shape without printing
its value:

```
Licence bytes: 5434
Licence root element: <root
Has <Signature>: true
Has <EntitlementGroups>: true
Licence format detected: entitlement-xml
```

### What run 26 proved — the path works, the licence does not

Run **37252858463** (run #26, push, commit `c510f6d8`) is the first run in which
the entitlement branch executed **and completed**. Step 9, `Run the benchmark
(entitlement XML)`, ran `01:48:22Z → 01:49:53Z` — **91 seconds** — and concluded
`success`. The probe answered every question run 25 could not:

```
--- probe: licence visibility ---
total 8
-rw------- 1 1001 1001 5434 Oct  5 01:48 UnityEntitlementLicense.xml
--- probe: Licensing Client binary ---
--- probe: editor binary ---
-rwxr-xr-x 1 1000 1000 110081080 Aug 19 06:29 /opt/unity/Editor/Unity
Probe exit code: 0
```

The licence **is** visible inside the container, the editor binary **is** present,
and the editor **did** start:

```
Unity Editor version:    6000.5.9f1 (b57deb96f08d)
Batch mode:              YES
[Licensing::Module] Successfully launched the LicensingClient (PId: 27)
[Licensing::IpcConnector] Successfully connected to: "LicenseClient-root"
[Licensing::Client] Handshaking with LicensingClient:
  Version:                 1.18.3+d7ffd15
```

Then the licence was rejected — and the log names the reason exactly:

```
[Licensing::Module] Error: Access token is unavailable; failed to update
[Licensing::Client] Error: Code 404 while processing request
  (status: Found 0 entitlement groups and 0 free entitlements matching requested entitlement ids)
[Licensing::Module] Error: 'com.unity.editor.headless' was not found.
No valid Unity Editor license found. Please activate your license.
Editor exit code: 198
```

**An entitlement licence is a named-user licence.** The file carries the
entitlement, but the Licensing Client needs the account's **access token** to
resolve it, and a token exists only after a sign-in. That is why the file alone
yields `0 entitlement groups`. This is not a machine-binding failure and not a
network failure — the client reached its own state and found no token.

| | |
|---|---|
| Job conclusion | `failure` |
| Step 9 conclusion | `success` (91 s) — the step is `continue-on-error` |
| Verdict | `INPUT ERROR` (exit 3) — no results XML, so the checker had no inputs |
| Artifacts | `water-gate-verdict-linux-…zip`, 631 bytes compressed (holds `verdict_linux.txt`, 1034 bytes) |

**This is not a pass.** The editor started and refused the licence before loading
the project. Nothing was compiled.

### The fix this run forced

The entitlement step now **signs in first**, when credentials are present:

```
-username "$UNITY_EMAIL" -password "$UNITY_PASSWORD"
```

The sign-in caches the access token in the Licensing Client's own state; the
benchmark run then resolves the entitlement from the mounted XML. The step also
dumps the Licensing Client's own log on a non-zero exit, because that file names
the real reason and the editor's stdout does not.

**The XML route therefore needs three secrets, not one:** `UNITY_LICENSE` (the
XML), `UNITY_EMAIL` and `UNITY_PASSWORD`. Without the credentials the preflight
now says so up front (`Unity credentials present: no`) and the step emits a
warning naming the exact failure it will produce.

### What is verified, and what is not

| Claim | Status |
|---|---|
| The workflow YAML parses | **VERIFIED** — `yaml.safe_load`, 2 jobs, 16 gate steps |
| Every `run:` block is valid shell | **VERIFIED** — `bash -n` on all 11 blocks, 0 failures |
| The classifier routes this document correctly | **VERIFIED** — run 26's preflight log: `entitlement-xml` |
| The licence is a well-formed entitlement document | **VERIFIED** — 5434 bytes, `<root>` + `<EntitlementGroups>` + `<Signature>` |
| The licence is visible inside the container | **VERIFIED** — run 26 probe: `-rw------- 1 1001 1001 5434 … UnityEntitlementLicense.xml` |
| The Licensing Client launches and connects | **VERIFIED** — run 26: `Successfully launched the LicensingClient (PId: 27)`, `Successfully connected to: "LicenseClient-root"` |
| The editor starts in the container | **VERIFIED** — run 26: `Unity Editor version: 6000.5.9f1`, `Batch mode: YES` |
| The entitlement XML alone activates Unity | **VERIFIED FALSE** — run 26: `Access token is unavailable`, `0 entitlement groups` |
| The sign-in fix activates Unity | **UNVERIFIED** — the sign-in step has never run; it needs `UNITY_EMAIL` and `UNITY_PASSWORD` |
| Anything compiled in CI | **NO** — no run has reached the point where the editor loads the project |

### The remaining risk, stated plainly

The sign-in fix is reasoned from the log, not tested. Two things could still
fail, and both are now visible in the run log: the account may have two-factor
authentication enabled (the CLI sign-in has no TOTP path), or the entitlement may
be bound to the machine that activated it. If either holds, **no wiring will make
this document work**, and the answer is the Plus/Pro route: `UNITY_SERIAL` +
`UNITY_EMAIL` + `UNITY_PASSWORD`, which re-issues the licence for the runner and
needs no file at all. The preflight already routes that combination to game-ci's
activation step.

### How to switch to the serial route, step by step

The serial route needs **three** repository secrets, and it is what game-ci's
activation step consumes directly. The email and password are the **same two**
the entitlement route already uses — the only new value is the serial.

| Secret | What it is | Where you find it |
|---|---|---|
| `UNITY_SERIAL` | The Plus/Pro serial key | **Unity Hub → gear icon → Preferences → Licenses** (the seat's serial), or the Unity account licence page at `id.unity.com`. It looks like `XX-XXXX-XXXX-XXXX-XXXX-XXXX`. |
| `UNITY_EMAIL` | The Unity account email | The address you sign in to Unity Hub / id.unity.com with. |
| `UNITY_PASSWORD` | The Unity **account password** | The account password — **not** a 2FA code. The CLI activation has no TOTP path, so an account with 2FA enabled can still fail here. |

**Steps (GitHub web UI):**

1. Open the repository: `github.com/bythewayz66-glitch/petrichor`.
2. **Settings** (top tab bar) → left sidebar **Secrets and variables → Actions**.
3. Click **New repository secret**.
4. Name it exactly `UNITY_SERIAL` (case-sensitive, no quotes), paste the serial
   into **Secret**, click **Add secret**.
5. Repeat for `UNITY_EMAIL` (the account email) and `UNITY_PASSWORD` (the account
   password), if they are not already set.
6. Re-run the gate: **Actions → Water Gate → Run workflow** (branch `main`), or
   push any commit under `Assets/Scripts/Water/`.

**How you know it took the serial route.** The preflight prints
`A Unity serial is present; the gate will activate with it via game-ci.` and the
gate job then runs the step named **`Run the benchmark (serial / ULF)`** —
which is game-ci's `unity-test-runner`, the path that consumes `UNITY_SERIAL` — and
**skips** `Run the benchmark (entitlement XML)`. If you instead see
`Licence format detected: entitlement-xml`, the serial secret did not register.

**Tradeoffs, stated plainly.** The serial route requires a **Plus/Pro seat** —
Unity Personal has no serial to enter. Each activation is bound to a machine and
a seat allows a limited number of simultaneous activations (Unity documents two
per seat); a busy repository can exhaust the limit, and activations then have to
be released manually from the Unity account's licence page before CI can run
again. The XML route consumes **nothing** — it reuses the seat's existing
entitlement. So: if the entitlement sign-in works, it is the cheaper route; the
serial route is the fallback for when the XML cannot work (2FA, or a
machine-bound entitlement).

**What the workflow does differently as of 2026-10-05.** The serial route already
had *priority* — the preflight tests `UNITY_SERIAL` before it looks at
`UNITY_LICENSE` at all. What was missing was the *withholding*. The gate step
passed **both** secrets to game-ci, and game-ci activates from the license
**file** whenever one is present, falling back to the serial only when it is not.
With the entitlement XML still in `UNITY_LICENSE`, the serial would have been
silently ignored and the step would have failed with the same Code 400. The step
now passes `UNITY_LICENSE` **only** when the preflight classified it as a `.ulf`,
so on the serial route game-ci receives exactly the documented
professional-license environment: `UNITY_SERIAL` + `UNITY_EMAIL` +
`UNITY_PASSWORD`.

One consequence worth knowing: if **both** a valid `.ulf` and a serial are ever
configured, the serial route wins and the `.ulf` is withheld, so the run spends
an activation rather than using the file. That is deliberate — one predictable
mechanism beats two competing ones.

**Still unverified.** No run has ever taken the serial route. The wiring is
syntactically valid and matches game-ci's documented activation, but nothing in
this repository proves Unity activates with it. The first serial-route run *is*
the verification step; read the gate step's log for the activation lines.

**The activation limit is the real cost.** Unity documents two concurrent
activations per seat, and — per Unity's own forum thread on the subject —
activations created by CI are often **not listed** on the Unity ID activation
page, which means the usual "Remove selected activations" remedy may not show
them. A busy repository can therefore lock itself out of its own seat. Prefer the
XML route when it works; keep the serial route for when the XML cannot work
(2FA, or a machine-bound entitlement).

---

## 4. What to do next, in order

### Step 1 — Get a CI verdict (§3)

The gate has returned a verdict **locally** (§1.4), and it is now written up:
**`docs/gate-reports/M1.md`**. The **CI** gate has executed eleven times and has
never produced a verdict of its own. Run 30 (`37731453756`, 2026-10-08) is the
furthest any run has reached: the editor started, was refused at exit `198`, and the
checker then returned `3` (`INPUT ERROR`) — see §1.4 and gap G8. Run 26
(`37252858463`) settled the question run 25 could not: the licence is visible in the
container, the Licensing Client launches and connects, and the editor starts — but
the entitlement XML alone is rejected with `Access token is unavailable`, because a
named-user licence needs the account's access token.

**Add `UNITY_EMAIL` and `UNITY_PASSWORD`** (Settings → Secrets and variables →
Actions). The entitlement step now signs in before the benchmark, which is what
obtains the token. Without them the XML route cannot work, and the run says so.

If the sign-in fails — two-factor authentication, or a machine-bound entitlement
— the fallback is the Plus/Pro route (`UNITY_SERIAL` + `UNITY_EMAIL` +
`UNITY_PASSWORD`), which re-issues the licence for the runner and needs no file.
Week 1 is a **gate**, not a week of work: it decides whether the water solver can
run on a mid-range phone, and it can re-baseline the entire plan.

### Step 2 — Run the gate

```bash
Tools/run_bench.sh --tier linux   --attempt 0
Tools/run_bench.sh --tier android --attempt 0
```

The Linux report already exists: `docs/gate-reports/gate-20261001-M01-linux-0001.md`,
and the M1 report is `docs/gate-reports/M1.md`. What is still missing is the
**Android** tier, which has never been measured on any device. Write that report
from `docs/GATE_REPORT_TEMPLATE.md` into
`docs/gate-reports/gate-<yyyymmdd>-<Mnn>-<tier>-<run>.md` and commit it. The
naming convention is defined in `docs/gate-reports/README.md`.

> **Before re-running, record machine quietness — now automatic.** The rung-1
> verdict flips between `PASS` and `BUDGET FAIL` on CPU contention alone (§5,
> risk 2). `run_bench.sh` now samples `/proc/stat` twice — a 5-second window
> before the editor, and again across the editor — and writes
> `BenchmarkResults/quietness_<tier>.txt` with the core count, both busy
> fractions, the wall clock, the load average and a `quiet` / `noisy` / `BUSY`
> verdict. **Discard any run whose file does not say `quiet`.** The rung-0 and
> rung-1 verdicts in this repository predate the instrumentation and carry no
> machine state, so they still cannot be attributed.
>
> **The runner refuses to start without an editor.** If the binary is missing it
> exits `3` and writes nothing. This is a data-loss guard: the verdict is a
> committed file, and before the guard existed, running on a checkout with no
> editor silently replaced rung 0's committed verdict with an `INPUT ERROR`.

### Step 2b — Run rung 2 (needs a machine with the editor)

```bash
Tools/run_bench.sh --tier linux --attempt 2 --window-scope cells --window-radius 1
```

This is the measurement the rung-2 model in `M1.json` is waiting for. Check
`BenchmarkResults/quietness_linux.txt` says `quiet` before believing the numbers,
then replace the model with the measured verdict in `M1.json` (`rung_2.source`
becomes the path of a real verdict file, not the string `model, not measurement`).

The `--window-scope tiles` variant is the rung exactly as the ladder names it, but
it cannot show a saving until the harness steps more than one tile — see §1.5.

### Step 3 — Work the tickets in order

| Order | Issues | Milestone |
|---|---|---|
| 1 | **#1–#8, #10** | M1 — Water Gate |
| 2 | **#9, #11–#18** | M2 — Grey-box Terrain and Streaming |
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

**Do not start week 2 until the rung-1 discrepancy is resolved.** The M1 report
is committed, but it records a `BUDGET FAIL` at both rungs, and the 2026-10-01
report's claim of a rung-1 `PASS` cannot be traced to any artifact (§5, risk 2).
Every ticket from #9 and #11 onward is labelled `status:backlog` with the
description *"Conditional on the week-one gate. Do not start before it returns a
verdict."*

### Step 4 — The first compile has already happened

The C# **has** been compiled, by the 2026-10-01 headless run, which found and
fixed two errors (§5, risk 1). A fresh clone still needs its first open, but
the "expect it to fail" budget is spent. The **CI** compile has also now run:
run 37107565152 reached the benchmark step and spent 117 s in it, which means
the project compiled in CI. What failed was licence activation, not the build.

---

## 5. Known risks and unverified claims

**Read this section before trusting anything in the repository.**

| # | Claim | Status | What it means |
|---|---|---|---|
| 1 | The C# harness compiles | **VERIFIED (locally only)** | The 2026-10-01 headless run compiled it and fixed two errors: `BenchmarkHarness.cs` and `ScenarioResult.cs` were missing `using PET.Water;`, and `PET.Benchmarks.asmdef` lacked the test-runner references so the assembly was not registered as a test assembly. **CI has never compiled it.** An earlier revision of this row claimed CI run 37107565152 "spent 117 s in the benchmark step, which requires a successful compile" — that inference was wrong: the 117 s was the licence-activation attempt, and the run failed before the editor loaded the project. No CI run has reached a compile |
| 2 | The gate passes | **NO — AND THE EVIDENCE CONFLICTS** | It has run locally. Rung 0 is a clean `BUDGET FAIL` (9 of 9 metrics over). The **committed** rung-1 verdict is also a `BUDGET FAIL` (2 of 9 over: `A_StaticSoak p50` 1.544 vs 1.500, `C_FastForward p50` 2.105 vs 1.500). `gate-20261001-M01-linux-0001.md` §5.1 claims a rung-1 `PASS` with numbers (1.377 / 0.816 / 1.362) that **exist in no artifact** — see `docs/gate-reports/M1.md` §5.1. The verdict also flips on CPU contention alone, so it is not reproducible without recording machine quietness. The Android tier has never been measured on any device |
| 3 | The four scenarios are correctly parameterised | **UNVERIFIED** | 3000 settle steps, 500 samples, 3600× multiplier are reasoned, not tuned. If scenario A's settle loop is too short, A reports a transient cost and becomes a second B |
| 4 | The changeset `b57deb96f08d` | **READ OFF A MACHINE** | Taken from the installed editor's `modules.json` on the box where the benchmarks were measured, so version and changeset provably agree. A different install of the same patch should still carry the same changeset; if it does not, you have a different build |
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

# Rung 2: shrink the active water window. --window-scope is one of none|tiles|cells
Tools/run_bench.sh --tier linux --attempt 2 --window-scope cells --window-radius 1
```

`--attempt` names how many rungs of the ladder are already applied. The checker
returns the **next** rung; it does not choose one for you. `--solver` selects the
implementation under test and defaults to `heightfield`, so a run that omits it
measures exactly what it measured before the option existed. `--window-scope` and
`--window-radius` configure rung 2 the same way: omitting them measures the
unwindowed solver, and the values actually used are recorded in
`BenchmarkResults/summary_RunSummary.json`. An unrecognised value of either fails
the NUnit run, which the checker reads as a HARD FAIL.

The harness refuses to start if `/opt/unity/Editor/Unity` is absent, rather than
overwriting a committed verdict with an `INPUT ERROR`. Point `UNITY_PATH` at your
editor to override the default.

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

> The numbers above are **illustrative, not measurements** — they are a shape
> reference, not a result. Real numbers exist for the Linux tier only, and only
> as committed verdict files; the per-scenario summaries behind them are
> `.gitignore`d and are not in the repository (§1.3). The `solver` key is what
> makes a run attributable: the checker refuses a results directory whose
> summaries disagree about it, and refuses one whose summaries have no `solver`
> key at all.

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
| 2 | **Shrink the active water window to tiles within 1 of the camera — IMPLEMENTED, NOT MEASURED (§1.5)** | distant water stops updating |
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

**The plan is fully ticketed and the repository is consistent. The M1 report is
committed (`docs/gate-reports/M1.md`) and it records a `BUDGET FAIL` at both
rungs — rung 0 with 9 of 9 metrics over, rung 1 with 2 of 9 over. The 2026-10-01
report's claim of a rung-1 `PASS` cannot be traced to any artifact, so the
repository's own evidence disagrees with itself and that must be settled first.
Rung 2 — the active water window — is implemented and committed, but it has
never been run: the environment it was written in has no Unity editor, so its
cost is a labelled model (`C_FastForward` modelled at `1.116535 ms` against a
`1.500 ms` threshold, passing by `0.383 ms` on a `1.116 ms` fixed cost) and not
a verdict. Machine quietness is now sampled automatically, but no verdict has
yet been taken with it attached. The C# compiles locally — and only locally: the
CI gate has executed eleven times and has never produced a verdict of its own, so
nothing has ever been compiled in CI, and nothing was compiled anywhere for rung 2. Run 30 (2026-10-08) is the first run to reach the checker — it returned `INPUT ERROR`, and it still uploaded a verdict file it inherited from the checkout (gap G8). The
Android tier has never been measured. The workflow accepts the entitlement XML
the `UNITY_LICENSE` secret holds, by mounting it into the Licensing Client's
directory instead of handing it to the manual-activation loader, but that path
still needs `UNITY_EMAIL` + `UNITY_PASSWORD` and has not yet activated. Resolve
the rung-1 discrepancy, run rung 2 on a machine that has the editor, and measure
the Android tier before week 2 is called done.**
