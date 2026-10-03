# PETRICHOR

A first-person open-world game about a world that is slowly turning to stone, and one person who can wake it back.

You are a **Waker**. The event that stilled the world is remembered as **the Stilling**: soil, sap and rivers turned to pale mineral rock in a wave that spread from the deep places. Life did not lose — it retreated into green pockets that survived. You can touch the seam between living and stilled ground and, locally and permanently, wake it back.

The rule that makes the game work is the **Mineral Law**: *stone cannot be forced back into soil.* Ground only wakes where **broken stone + water + a living seed** meet together. Terraforming supplies the broken ground, water delivers the awakening, planting makes it self-sustaining. The three shaping systems are not three features — they are three links in one causal chain, and the player learns the chain by reading three short messages.

No combat. No antagonist. Quiet, enormous, hopeful. The world can kill you with a fall or a flood, but it will never hunt you.

**Status:** pre-production. The design package is complete and the repository scaffold is committed — the twelve assemblies, the water solver interface, and the week-one benchmark harness. **The gate has now run, locally and in CI.** Locally it produced verdicts (`BUDGET FAIL` at rung 0, `PASS` at rung 1 on a quiet box); in CI it executed for the first time on 2026-10-03 (run 37107565152) and **failed on licence activation**, because the `UNITY_LICENSE` secret holds an entitlement XML and the workflow only knew how to feed Unity a `.ulf`. **The workflow now supports the entitlement XML directly** — it mounts the licence into the Licensing Client's own directory instead of handing it to the manual-activation loader. That path is wired and syntax-checked but **has not yet run**, so the CI gate is still without a verdict. Week 1 of the implementation roadmap is a **gate** — a headless benchmark that decides whether the water solver can run on a mid-range phone. Nothing downstream of it starts until it returns a reproducible verdict.

---

## Pinned editor version

| | |
|---|---|
| **Unity** | **6000.5.9f1** — the `6000.5` line |
| **Support window** | Two-year LTS through **December 2027** |
| **Template** | Universal 3D (URP) |
| **Exact patch** | Recorded in `ProjectSettings/ProjectVersion.txt` and in the table below |

Everyone on the project uses the **exact same patch version**. The render-pipeline packages are editor-coupled and must not be hand-pinned — move the editor, not the package.

### Record the exact patch here

`ProjectVersion.txt` is committed by Unity automatically, but state the patch in this README too so a new machine can be matched without opening the project:

```
m_EditorVersion: 6000.5.9f1
m_EditorVersionWithRevision: 6000.5.9f1 (b57deb96f08d)
```

The revision in parentheses is the **changeset**, and it is not decoration: it is what
`unityhub://6000.5.9f1/b57deb96f08d` resolves to, and it is what CI uses to fetch the
exact editor build. A `ProjectVersion.txt` whose revision reads `TBD` is a file Unity
has not written yet, and the first CI run fails on it.

> **If you install a different patch of the 6000.5 line**, open the project once and let
> Unity rewrite this file, then copy both lines here. Do not hand-edit the changeset to
> match a version you have not installed — the two must agree or the editor will
> re-import the whole project on every open.

---

## Package version record

Read these off **Package Manager** on the machine that created the project, and write them into the table. Do not copy versions from any design document — a wrong pin in a manifest is a compile error that looks like a code bug.

| Package | Policy | Version on this machine |
|---|---|---|
| `com.unity.inputsystem` | **Pin 1.19.0** — verified release, targeted at the 2022.3 / 6.0 / 6.2 / 6.3 editor lines | `1.19.0` |
| `com.unity.render-pipelines.universal` | **Do not pin.** SRP packages are tied to specific editor versions; hand-pinning causes missing types and permanent compile errors | *(editor bundled)* |
| `com.unity.addressables` | Take the **"Recommended"** tag, not "Latest" — Recommended is the version actually tested against your editor | *(read from Package Manager)* |
| `com.unity.test-framework` | Editor bundled. 1.5+ is Unity 6 only | *(editor bundled)* |
| `com.unity.test-framework.performance` | Editor bundled. **Must also be listed in the manifest's `testables` array** or its attributes silently fail to resolve | *(editor bundled)* |
| `com.unity.burst` | Editor bundled — keeps it matched to the editor's Collections package | *(editor bundled)* |
| `com.unity.collections` | Editor bundled — native containers for the solver's working buffers | *(editor bundled)* |
| `com.unity.mathematics` | Editor bundled — Burst-friendly math types | *(editor bundled)* |

> **Unity 6.3 note.** 6.3 adds a `pinnedPackages` property to the project manifest, which forces specified direct dependencies to use their exact manifest versions during resolution. It is the right tool for the input system and the wrong tool for the SRPs.

---

## Repository layout

```
Assets/
  Art/                # purchased and authored art, by category
    Terrain/  Vegetation/  Rock/  Water/  Props/  Sky/
  Audio/              # MUS_* bed, AMB_* loops, SFX_*
  Data/               # ALL ScriptableObjects. This is the game.
    Biomes/  Guilds/  Seeds/  Resources/  Tools/  POI/  Tiers/  Budgets/
  Scenes/
    SC_Boot.unity            # always first, single scene, tiny
    SC_Persistent.unity      # additive, never unloaded
    Tiles/                   # SC_T_x00_y00 ... x07_y07  (64)
    Caves/                   # SC_Cave_Karst_01, _02
    SC_Editor_Sandbox.unity  # authoring only, excluded from every build
  Prefabs/            # PF_*
  Settings/
    URP/  Input/  Quality/
  Scripts/            # mirrors the assembly list
    Core/  Data/  World/  Water/  Life/  Edit/  Save/  Player/  UI/  Render/  Boot/
  Shaders/            # SH_* Shader Graph assets, custom HLSL
  Tests/
    EditMode/  PlayMode/  Benchmarks/
Plugins/              # third-party only, kept quarantined
Packages/
ProjectSettings/
BuildProfiles/        # Linux-Release, Android-Release (Unity 6 build profiles)
Tools/                # run_bench.sh, check_thresholds.py, bench_thresholds.json, CI scripts
docs/                 # the project's written record
  GATE_REPORT_TEMPLATE.md   # the week-one gate report format
  gate-reports/             # one committed report per gate run
  decisions/                # decision records - what was rejected, and why
.github/workflows/    # water-gate.yml - the week-one gate, on every push
.gitattributes        # LFS rules + merge strategy per file type
.gitignore            # Unity generated state and build output
.git-blame-ignore-revs# revisions `git blame` should look through
Builds/               # git-ignored
```

### Art-pipeline rules

`Assets/Art/` carries its own `.gitignore` and `.gitattributes`. The root pair is the
project's contract; the art pair covers the vendor and intermediate formats a purchased
photoreal pipeline delivers — `.dds`, `.sbsar`, `.abc`, `.usd`, `.r16`, `.flac` and the
rest — which the root file has no reason to know about. Git applies a `.gitattributes`
to its own directory and everything below it, so the split is safe.

The reason for the split is that the two files change for different reasons. The root
file changes when the *project* changes; the art file changes when a *vendor pack*
arrives. Keeping them apart means a new vendor format is added in the same commit that
adds the first asset of that type, without touching the project's contract.

> **LFS storage is a real budget, not a formality.** GitHub Free and Pro include **10 GiB**
> of LFS storage and **10 GiB** of bandwidth per month; Team and Enterprise Cloud include
> **250 GiB** of each. The estimated art set for the 4 km² slice is **≈ 1.4 GB** — about
> 15% of the Free tier's storage. See the storage budget table in the repository rules
> page for the per-category breakdown.
>
> **Bandwidth, not storage, is what runs out first.** Every clone, pull and CI checkout
> that fetches an LFS object counts against the bandwidth quota, and it counts against
> the *repository owner's* account — including clones by other people. A full checkout of
> the ≈ 1.4 GB art set is **~7 fetches** against a 10 GiB monthly quota. A CI job that
> pulled art on every push would exhaust a month's bandwidth in about seven pushes.
>
> **The mitigation is in the workflow, not the plan.** `water-gate.yml` checks out with
> `lfs: false` and gates its LFS fetch behind a `needs_art` input that defaults to false,
> so the gate fetches **zero** LFS bytes — it measures a solver and has no use for a
> texture. When a job genuinely needs art, the object store is cached under
> `.git/lfs/objects`, keyed on the `.gitattributes` files. LFS objects are
> content-addressed and immutable, so a cache hit can never be stale.

### The twelve assemblies

An assembly may only reference assemblies **below** it. The rule is enforced by the compiler, not by code review.

| # | Assembly | References | Owns |
|---|---|---|---|
| 1 | `PET.Core` | *nothing* | Math helpers, tile coordinates, deterministic RNG, tick scheduler, allocation-free collections |
| 2 | `PET.Data` | Core | All ScriptableObject definitions, tier profile, budget assets, ID enums, validation attributes |
| 3 | `PET.Edit` | Core, Data | The edit journal, `EditRecord`, the fracture mask, journal compaction, the maturity bake |
| 4 | `PET.Save` | Core, Data, Edit | Serialization, chunked file writer, versioning and migration, the load sequence |
| 5 | `PET.Water` | Core, Data, Edit | The heightfield flow solver, source points, channel graph fallback, the water field |
| 6 | `PET.Life` | Core, Data, Edit | Seed registry, growth stages, scatter rules, guild definitions |
| 7 | `PET.World` | Core, Data, Edit, Water, Life | Terrain tiles, streaming, the wake check, biome sampling |
| 8 | `PET.Player` | Core, Data, World | Controller, camera, scale toggle, the hand instrument, the three shaping tools |
| 9 | `PET.Render` | Core, Data, World | Water rendering, wake mask shader binding, tier-driven quality switching |
| 10 | `PET.UI` | Core, Data, Player | HUD, the three failure messages, inventory display |
| 11 | `PET.Boot` | everything above | The bootstrap order, scene loading, the single entry point |
| 12 | `PET.Benchmarks` | Core, Data, Water, Edit | Test-only. Excluded from player builds. The week-one harness lives here. |

---

## How to open and run

### First time

1. Install **Unity 6000.5.9f1** through Unity Hub with these modules:
   - **Linux Build Support (IL2CPP)** — required for the flagship tier. Unity ships a Linux IL2CPP cross-compiler, so this builds from any standalone host.
   - **Android Build Support** with the bundled OpenJDK, SDK and NDK.
   - **Linux Build Support (Mono)** — optional, useful as a fast iteration target.
2. Clone the repository. **Do not** open it before installing the modules.
3. Open the project folder in the pinned editor. First import takes several minutes.
4. Verify `Edit → Project Settings → Editor`:
   - **Asset Serialization** = *Force Text*
   - **Version Control Mode** = *Visible Meta Files*
5. Verify `Player → Other Settings`:
   - **Color Space** = *Linear*
   - **Api Compatibility Level** = *.NET Standard 2.1* (unless a package demands otherwise)
6. Open `Assets/Scenes/SC_Boot.unity` and press Play. The boot scene loads `SC_Persistent` additively and unloads itself.

> **Not available yet.** `Assets/Scenes/` does not exist — the tree above is the target
> layout, not the current contents. Step 6 is the first thing that will work once the
> scenes are authored (W6.1). Until then the only thing that can be run is the gate.

### Configure git before your first commit

Four commands, once per clone. They are not optional and they are not
per-machine preferences — two of them are what make a Unity repository
mergeable at all.

```bash
git lfs install
git config blame.ignoreRevsFile .git-blame-ignore-revs
git config merge.unityyamlmerge.name "Unity SmartMerge"
git config merge.unityyamlmerge.driver "<UNITYYAMLMERGE> merge -p --force %O %B %A %A"
git config merge.unityyamlmerge.recursive binary
```

**`<UNITYYAMLMERGE>` is the full path to the tool inside your editor install.**
Unity does not put it on `PATH`, and the path contains the editor version, so it
is different on every machine. Substitute the line that matches your platform —
this is the documented layout of a Unity Hub install:

| Platform | Path |
|---|---|
| **Windows** | `C:\Program Files\Unity\Hub\Editor\<version>\Editor\Data\Tools\UnityYAMLMerge.exe` |
| **macOS** | `/Applications/Unity/Hub/Editor/<version>/Unity.app/Contents/Tools/UnityYAMLMerge` |
| **Linux** | `~/Unity/Hub/Editor/<version>/Editor/Data/Tools/UnityYAMLMerge` |

Windows users: in Git Bash use forward slashes and quote the whole path, or the
backslashes are eaten before git sees them.

```bash
git config merge.unityyamlmerge.driver "/c/Program\ Files/Unity/Hub/Editor/6000.5.9f1/Editor/Data/Tools/UnityYAMLMerge.exe merge -p --force %O %B %A %A"
```

**Verify the path before you trust it.** The install location depends on where
Unity Hub put the editor and whether you used a custom install root. Check with:

```bash
ls "$(dirname "$(command -v unity-hub 2>/dev/null || echo /Applications/Unity/Hub/Editor)")"   # or just browse to it
```

If the path is wrong, git will silently fall back to a line-based text merge on
the next conflicting `.unity` file, which produces a corrupt scene rather than a
conflict marker. Confirm the driver resolves:

```bash
git config --get merge.unityyamlmerge.driver
```

#### What each of these does, and why

| Command | What it does | Why it is not optional |
|---|---|---|
| `git lfs install` | Registers the LFS filter in your global git config | Without it the `.gitattributes` LFS rules are inert and binary art lands in the object database. The repository becomes unusable within a month |
| `blame.ignoreRevsFile` | Points `git blame` at the revision-skip list | Without it a formatting sweep makes every line of a file look like it was written by whoever ran the tool, and the person who wrote the logic disappears from the history |
| `merge.unityyamlmerge.driver` | Runs Unity's semantic merge on conflicting YAML assets | The default line-based merge does not understand Unity's YAML structure and will happily produce a `.unity` file that no longer opens |
| `merge.unityyamlmerge.recursive binary` | Declares the driver binary, so it is used when merging a merge | An unset recursive driver degrades to a text merge inside a recursive merge — the case where a scene conflict is most likely |

**`merge=unityyamlmerge` is already declared per file type in `.gitattributes`.**
These commands register the DRIVER that attribute names. Without both halves,
neither does anything: the attribute alone points at a driver that does not
exist, and the driver alone is never selected for any file.

The four asset types carrying that attribute are `.unity`, `.prefab`,
`.asset` and `.mat` — all Force Text with LF normalisation, which is exactly
what the semantic merge needs. `TerrainData` is deliberately absent: it stays
binary under LFS, because Unity does not serialise it as text (see
CONTRIBUTING §6).

### Running the tests

```bash
# EditMode + PlayMode, in the editor
Window → General → Test Runner → Run All

# Headless, from the command line
/opt/unity/Editor/Unity -batchmode -nographics -quit \
  -projectPath . -runTests -testPlatform EditMode \
  -testResults BenchResults/editmode.xml
```

### Running the week-one gate

```bash
Tools/run_bench.sh --tier linux   --attempt 0
Tools/run_bench.sh --tier android --attempt 0

# Measure a ladder rung instead of the reference solver
Tools/run_bench.sh --tier android --attempt 0 --solver channel-graph
```

`--attempt` names how many rungs of the ladder are already applied; the checker
returns the **next** rung rather than choosing one. `--solver` selects the
implementation under test and defaults to `heightfield`, so a run that omits it
measures exactly what it measured before the option existed.

Each run writes `BenchmarkResults/results_<tier>.xml`, `summary_<scenario>.json`
and `verdict_<tier>.txt`. The verdict is one of `PASS`, `BUDGET FAIL`,
`HARD FAIL` or `INPUT ERROR` — the rung, when there is one, is named inside the
verdict as `NEXT RUNG: n`. See the harness specification for what each means.

> **`BenchResults/` and `BenchmarkResults/` are different directories.** The
> first is game-ci's artifact directory and holds the NUnit results XML; the
> second is the harness's own output directory and holds the summaries and the
> verdict. The checker reads the XML from one and the summaries from the other.

---

## Build profiles

Unity 6 build profiles are **assets in the project**, so they are committed and shared rather than re-configured by hand on each machine.

| Profile | Target | Key settings |
|---|---|---|
| `Linux-Release` | Linux x86_64 | IL2CPP, Vulkan (OpenGL Core configured as fallback), 64-bit, no development build. Scenes: `SC_Boot` + `SC_Persistent` only — tiles arrive through Addressables |
| `Android-Release` | Android | IL2CPP, **ARM64 only**, Vulkan with OpenGL ES 3.x fallback, **target API level 35 or higher**. Scenes: same two |

> **Two Android facts that are easy to get wrong.**
> **ARM64 is mandatory** — Google Play requires 64-bit support and rejects a 32-bit-only build. Set Target Architectures to ARM64 and leave it there.
> **Target API level 35 or higher** — Google Play's requirement moved to Android 15 (API 35) for new apps and updates, with API 36 following. Unity 6.0.18f1 and later support API 35; 6.0.46f1 and later support API 36. Unity 6.3 LTS is past both, but the setting is not automatic.

### Building

```bash
# Linux
/opt/unity/Editor/Unity -batchmode -quit -projectPath . \
  -buildTarget StandaloneLinux64 -executeMethod PET.Boot.BuildScript.BuildLinux

# Android
/opt/unity/Editor/Unity -batchmode -quit -projectPath . \
  -buildTarget Android -executeMethod PET.Boot.BuildScript.BuildAndroid
```

---

## Where the design documents live

The design package is five documents plus three follow-on artifacts. They are the authority for every decision in this repository — if code and a design document disagree, one of them is a bug and it is worth finding out which.

| # | Document | What it settles |
|---|---|---|
| 1 | **Game Design Document** | PETRICHOR, the Stilling, the Wakers, the Mineral Law. URP with two tiers, not HDRP |
| 2 | **World Concept** | The closed 4 km² basin, eight biomes, four percent alive, five seed guilds across four Refuges |
| 3 | **Systems Breakdown** | Seven loop states, fracture as a mask, water re-derived not stored, a journal not a snapshot |
| 4 | **Technical Architecture** | One project with tiers as data, twelve assemblies, sixty-four tile scenes, the week-one gate |
| 5 | **Implementation Roadmap** | Ten bootstrap steps, sixteen weeks, ten milestones, fourteen risks, a written cut order |
| — | **Tracked Project Board** | The sixteen weeks as tracked items, plus one hundred and forty-four tickets for weeks 1–16 |
| — | **Harness Specification** | The week-one gate in full: solver interface, scenarios, thresholds, the checker, the ladder |
| — | **README / CONTRIBUTING** | This file, and the working rules |
| — | **Repository rules** | Every `.gitignore` and `.gitattributes` rule, with the reason it exists |

The repository-rules page exists because a `.gitignore` line with no
justification is a line somebody deletes. Every entry there is paired with what
breaks without it.

---

## Contributing

Read **[CONTRIBUTING.md](CONTRIBUTING.md)** before your first commit. It covers the assembly dependency rule, commit conventions, branch strategy, code style, asset naming, the LFS policy, and the two rules that matter most: **cuts are recorded in the same commit**, and **three things are never cut**.

---

## Reference device

**The Android tier is measured on one named phone, and it is this one:**

| Field | Value |
|---|---|
| Device | **Samsung Galaxy A54 5G** (`SM-A546B`) |
| SoC | Samsung Exynos 1380 (5 nm) — 4× Cortex-A78 @ 2.4 GHz + 4× Cortex-A55 @ 2.0 GHz |
| GPU | ARM Mali-G68 MP5 |
| RAM | 6 GB |
| Display | 6.4" Super AMOLED, 1080 × 2340, 120 Hz |
| Android | shipped Android 13 (One UI 5.1); upgradable to Android 15 (One UI 7), API 35 |
| Released | March 2023 |

### Why this device

- **It is the most representative mid-range phone in the install base.** The A54 was the best-selling Android phone of 2023. A number measured on it describes a phone a large fraction of players actually own; a number measured on a flagship describes a phone almost nobody does.
- **The Mali-G68 MP5 is the weakest GPU class the project targets.** If the water sim fits here, it fits on most of the install base. Measuring on the weakest supported device is what makes the result a floor rather than a hope.
- **6 GB of RAM is the binding constraint.** Four 257² water fields, plus terrain and the streamer, have to fit alongside the OS. A device with 8 GB would hide a memory problem that a 6 GB device exposes.
- **API 35 satisfies the Play requirement** without needing a newer device to test it.

### This is a recommendation, not a measurement

**Confirm it against hardware you actually own.** The gate cannot be run on a device nobody has. If you own a different mid-range 2022–2023 phone, use it and record it in the gate report — the choice of device matters far less than the fact that one specific device is named and stays named across runs.

What changes if you use a different device:

| If your device is… | Then… |
|---|---|
| A flagship (Snapdragon 8 Gen 2, Adreno 740) | The numbers will look comfortable and will not describe the install base. Keep it as a *second* device, not the reference. |
| Older or weaker (Snapdragon 695, Adreno 619) | Expect the Android tier to land on rung 3 or 4. That is a real result, not a failure — it is the gate doing its job. |
| A different 2023 mid-range (Pixel 7a, Galaxy A34) | Fine. Record it and keep it. The thresholds do not change; only the device line in the report does. |
| An emulator | **Not acceptable.** An emulator's GPU path is not the device's, and the thermal behaviour is absent entirely. The gate measures sustained load, which an emulator cannot reproduce. |

---

## Continuous integration

`.github/workflows/water-gate.yml` runs the week-one gate on every push that touches
`Assets/Scripts/Water/**`, `Assets/Tests/Benchmarks/**`, `Tools/**` or the workflow
itself, and on every pull request against those paths. It can also be run by hand from
the Actions tab (`workflow_dispatch`), with a `tier` input (`linux` or `android`), an
`attempt` input naming how many rungs of the ladder are already applied, and a `solver`
input selecting the implementation under test (`heightfield` or `channel-graph`).

### It needs a Unity licence — and it now accepts the one you have

**The `UNITY_LICENSE` secret exists** (added 2026-10-03) and holds an **entitlement
licence** — the XML document Unity Personal issues, whose root element is `<License>` and
which carries `<EntitlementGroups>`. It may be named `UnityEntitlementLicense.xml` or
carry a `.txml` extension; the extension is irrelevant, the root element is what matters.

**Both licence formats are supported.** The `preflight` job reads the secret's root
element and routes the gate accordingly:

| Detected | `licensed` | What the gate does |
|---|---|---|
| *(no secret)* | `false` | skips, exits green — deliberate: a repository that is red on every push because a secret is missing is one whose CI everyone learns to ignore |
| `ulf` — root `<root>` | `true` | runs via `game-ci/unity-test-runner`, which activates with `-manualLicenseFile` |
| `entitlement-xml` — root `<License>` | `true` | runs via the **Licensing Client** path — see below |
| anything else | `false` | **fails in ~5 s**, naming the format. A hard failure, not a skip: a licence *is* configured, so a green tick would be a lie |

#### Why the two formats need different mechanisms

They are different documents, read by different parts of Unity:

| Format | Root element | Distinctive child | Read by |
|---|---|---|---|
| `.ulf` | `<root>` | `<Signature>` | `-manualLicenseFile`, and game-ci's activation step |
| entitlement `.xml` / `.txml` | `<License>` | `<EntitlementGroups>` | the **Licensing Client**, from its own licences directory |

An entitlement licence also contains a `<Signature>` element, so a test for `<Signature>`
alone misclassifies it as a `.ulf`. **The root element is the discriminator**, and the
preflight checks it first.

The two live in **different directories**, which is why pointing `-manualLicenseFile` at an
entitlement licence cannot work however the file is named:

| | Path |
|---|---|
| `.ulf` (manual activation) | `~/.local/share/unity3d/Unity/Unity_lic.ulf` |
| entitlement (Licensing Client) | `~/.config/unity3d/Unity/licenses/UnityEntitlementLicense.xml` |

*Source: Unity, "License troubleshooting" (`ActivationFAQ`), which gives both paths.*

#### How the entitlement path runs

The gate writes the secret to the Licensing Client's directory and mounts it into the same
editor image game-ci would have used, then runs the editor directly:

```bash
LIC_DIR="$HOME/.config/unity3d/Unity/licenses"
mkdir -p "$LIC_DIR"
printf '%s' "$UNITY_LICENSE" > "$LIC_DIR/UnityEntitlementLicense.xml"

docker run --rm \
  -v "$PWD:/github/workspace" \
  -v "$LIC_DIR:/root/.config/unity3d/Unity/licenses" \
  -w /github/workspace \
  "unityci/editor:ubuntu-<version>-linux-il2cpp-3" \
  xvfb-run -ae /dev/stdout /opt/unity/Editor/Unity \
    -batchmode -nographics -projectPath /github/workspace \
    -runTests -testPlatform EditMode -assemblyNames PET.Benchmarks \
    -testResults /github/workspace/BenchResults/results_<tier>.xml \
    -logFile /dev/stdout
```

The licence value is never printed — it goes from the environment straight to the file,
and the file is never `cat`'d. Only its byte count and root element are reported.

> **This path is UNVERIFIED.** It was written on 2026-10-03 and no run has used it. Two
> things could still be wrong, and both are visible in the log:
>
> 1. **The entitlement licence may be bound to the machine that activated it.** If the
>    Licensing Client rejects it in the container, the log will say so. The fallback is the
>    Plus/Pro route below, which re-issues the licence for the runner.
> 2. **The editor may need the Licensing Client started explicitly.** The log shows whether
>    it launched on its own.
>
> If the entitlement path fails, the fallback is `UNITY_SERIAL` + `UNITY_EMAIL` +
> `UNITY_PASSWORD`, which is a supported game-ci route and needs no licence file at all.

#### Adding the secret

At *Settings → Secrets and variables → Actions → New repository secret*:

| Secret | For | Where the value comes from |
|---|---|---|
| `UNITY_LICENSE` | Personal licence | The **whole** entitlement XML or `.ulf` file, including the `<?xml ... ?>` declaration and the closing tag |
| `UNITY_SERIAL` + `UNITY_EMAIL` + `UNITY_PASSWORD` | Plus / Pro | The serial from your Unity licence page |

For the entitlement route, copy the file Unity Hub wrote — on Linux
`~/.config/unity3d/Unity/licenses/UnityEntitlementLicense.xml` — in full. A truncated
copy is the most common cause of a licence that "looks right" and is rejected.

> **A personal licence is machine-bound.** GameCI's activation step re-issues a `.ulf` for
> the runner; the entitlement path does not re-issue anything, which is exactly why it may
> be rejected. If activation fails with a licence error, the usual cause is that the
> personal licence is already active on too many machines — deactivate one from the Unity
> account page and retry.

### What the workflow does, and what fails the build

| Exit code | Verdict | Build | Meaning |
|---|---|---|---|
| `0` | PASS | green | Every scenario within threshold |
| `1` | BUDGET FAIL | **green** | Walk the rung named in the job summary. A budget miss is a project event, not a broken commit |
| `2` | HARD FAIL | **fails** | NaN, mass imbalance or a red test. The ladder cannot fix these |
| `3` | INPUT ERROR | **fails** | The checker could not read its inputs; the run is invalid |
| anything else | no verdict | **fails** | Treated as an input error, never as a pass |

Correctness is evaluated before budget, so a fast wrong solver cannot be mistaken for a
pass. The verdict, not the test runner's exit code, is authoritative — which is why the
benchmark step carries `continue-on-error` and the enforcement happens in one final step.

Raw samples are uploaded for 30 days; the verdict file for 90. `Library/` is cached on a
key that includes `Packages/packages-lock.json` and every `.asmdef`, so a dependency
change invalidates the cache rather than producing a stale import.

### LFS bandwidth

The gate checks out with `lfs: false` and gates its LFS fetch behind a `needs_art` input
that defaults to false, so a gate run fetches **zero** LFS bytes. When a job genuinely
needs art, `.git/lfs/objects` is cached, keyed on the `.gitattributes` files — LFS objects
are content-addressed and immutable, so a cache hit can never be stale.

| | Before | After |
|---|---|---|
| LFS bytes per gate run | ≈ 1.4 GB (full art set) | **0** |
| Gate runs before the 10 GiB monthly quota is gone | ~7 | unbounded |
| LFS bytes when art *is* needed | ≈ 1.4 GB every run | ≈ 1.4 GB once, then cache hits |

### Committed gate reports

A run that produces a verdict gets a report committed to **`docs/gate-reports/`**, named
`gate-<yyyymmdd>-<Mnn>-<tier>-<run>.md` so a plain `ls` sorts chronologically. The format
is `docs/GATE_REPORT_TEMPLATE.md`; the naming, provenance and retention rules are in
`docs/gate-reports/README.md`.

**A report is never committed for a run whose `gate` job was skipped.** A skipped gate
produced no verdict, and a report written from one would record a pass that never
happened. The first eighteen runs were skipped for want of a licence secret. Run
37107565152 was the first to execute the `gate` job — and it failed on activation before
the checker ran, so it produced no verdict either and **no CI report has been written**.
The only committed report, `gate-20261001-M01-linux-0001.md`, comes from a local run.

### Decision records

**`docs/decisions/`** holds one short record per decision that was made and could have
gone the other way — what was rejected, and why. The design package records what was
chosen; it does not record what was rejected, and a rejected option is the thing that
gets re-proposed six weeks later. The convention and template are in
`docs/decisions/README.md`.

---

## Assumptions

- **One developer, full time.** Art is purchased, not authored. If either is wrong, the sixteen-week plan is wrong.
- **The reference Android device is the Samsung Galaxy A54 5G**, recorded above. Every Android performance number in the design package is meaningless until one specific device is named and stays named.
- **The water solver is the project's largest technical risk.** URP ships no water system, so this is a simulation the project owns. Week 1 exists to find out whether it is feasible.
