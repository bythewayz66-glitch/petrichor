# Contributing to PETRICHOR

This is a one-person project, which is exactly why these rules are written down. A rule that lives only in your head is a rule you will break in week eleven when you are tired, and the cost of breaking it will not be obvious until week fourteen.

---

## 1. The assembly dependency rule

**An assembly may only reference assemblies below it in the list.** The full ordering is in the README.

```
PET.Core  →  PET.Data  →  PET.Edit  →  PET.Save
                              ↓
                          PET.Water  →  PET.Life  →  PET.World
                                                        ↓
                                          PET.Player  →  PET.Render  →  PET.UI
                                                        ↓
                                                    PET.Boot
```

### Why the compiler enforces it, not code review

The rule is not a convention. It is expressed as `.asmdef` references, which means a violation is a **compile error**, not a comment in a pull request. That matters because the failure mode this rule prevents is invisible at the point of the mistake:

- If `PET.Edit` could reference `PET.World`, then the edit journal could learn what a biome is. It would compile. It would work. And then the journal would have a dependency on world content, and the save system would have a dependency on the journal, and the two would become impossible to test independently — which is the exact property the whole save architecture depends on.
- If `PET.Core` could reference anything, it would slowly accumulate helpers for every layer above it, and it would stop being the one assembly that can be tested in isolation in milliseconds.

**The rule is: if you need something from a higher assembly, the thing you need is in the wrong place.** Move it down, or pass it in as data.

### The one hard prohibition

**No `#if UNITY_ANDROID` anywhere under `PET.Edit` or `PET.Save`.** The two tiers differ only in `SO_Tier_*` ScriptableObject values. A platform conditional in the persistence layer means a save made on desktop can behave differently on Android, which breaks the permanence pillar. This is checked as a milestone exit criterion.

---

## 2. Commit messages

Conventional Commits, with the assembly or area as the scope.

```
<type>(<scope>): <subject>

<body — what and why, not how>

<footer — refs, cuts, risks>
```

**Types:** `feat`, `fix`, `perf`, `refactor`, `test`, `docs`, `build`, `chore`, `cut`

**Scopes:** the assembly name without the prefix (`core`, `data`, `edit`, `save`, `water`, `life`, `world`, `player`, `render`, `ui`, `boot`, `bench`), or an area (`art`, `audio`, `ci`, `docs`, `board`).

**Examples:**

```
feat(water): add flux limiter to the step function

Without the limiter a cell can give away more water than it holds, which
drives depth negative and produces a NaN on the following step. The limiter
scales all outgoing flux by (held / totalOut) when totalOut exceeds held.

Refs: W1.2
```

```
perf(water): halve sim resolution to 129 for the Android tier

Gate returned RUNG 1 at 257 squared: p95 6.71 ms against a 5.0 ms threshold.
Sim and render grids are decoupled, so this is invisible at the render layer.

Refs: W1.7, R1
```

```
cut(scope): drop the body-scale set-piece

Cut order row 1. Recovers ~1 week. The player loses one rare hand-authored
moment. Already declared the first thing cut in Deliverable 3.

Refs: Deliverable 5 S08
```

### Subject line rules

- Imperative mood: "add", not "added" or "adds".
- Lowercase after the colon.
- No full stop at the end.
- Under 72 characters.

---

## 3. Branch strategy

Trunk-based with short-lived branches. There is one developer, so the branch strategy exists to protect the gate, not to coordinate people.

| Branch | Purpose | Lifetime |
|---|---|---|
| `main` | Always compiles. Always passes the gate. | permanent |
| `wk/NN-slug` | One week's work, e.g. `wk/01-water-gate` | ≤ 1 week |
| `exp/slug` | A spike that may be thrown away, e.g. `exp/channel-graph` | ≤ 3 days |
| `fix/slug` | A bug fix that should not wait for the week branch | hours |

**Rules:**

- `main` must compile and pass `Tools/run_bench.sh linux` before any merge.
- An `exp/` branch that survives is squashed into a `wk/` branch. An `exp/` branch that does not survive is deleted without merging — that is a successful outcome, not a wasted one.
- **Never commit directly to `main`** except for documentation and the board data file.
- Tag every milestone: `git tag -a m1-water-gate -m "Gate: PASS at 257 squared"`.

---

## 4. Code style

### C#

- **Four spaces**, no tabs. `.editorconfig` is committed and authoritative.
- **`_camelCase` for private fields**, `PascalCase` for public members and types, `camelCase` for locals and parameters.
- **Explicit access modifiers** on everything, including `private`.
- **Braces on their own line** for types and methods; inline for single-statement guards is acceptable.
- **`var` only when the type is obvious from the right-hand side.** `var field = new WaterField(...)` is fine; `var x = Compute()` is not.
- **No `Region`.** If a file needs regions to be readable, it needs to be two files.

### The hot path

Anything in `PET.Water`'s step function, or in any `[BurstCompile]` method, follows stricter rules:

- **No managed allocations.** No `new` on a class, no LINQ, no `foreach` over a managed collection, no string formatting.
- **No virtual dispatch.** No interfaces with virtual members, no delegates, no `abstract` calls.
- **`math.*` not `Mathf.*`** — the `Unity.Mathematics` types are Burst-friendly and vectorise.
- **`FloatMode.Strict`** on the `[BurstCompile]` attribute for the solver. Fast-math would let the compiler reassociate float operations, which breaks within-device determinism, which breaks the save system.
- **Every `NativeArray` is disposed.** A leak in a persistent allocation shows up as a slow memory climb over a long session, which is the hardest kind of bug to attribute.

### Comments

Comment the **why**, never the **what**. `// increment i` is noise. `// the limiter is what keeps mass conserved; without it a cell goes negative and the next step divides by a negative depth` is the reason the line exists.

Every non-obvious constant gets a comment explaining where the number came from. If the answer is "it felt right", say so — that is useful information for whoever tunes it next.

---

## 5. Asset naming

All asset names are `PREFIX_Name_Variant`. Prefixes are mandatory and are checked by an automated naming test.

| Prefix | Type | Example |
|---|---|---|
| `SC_` | Scene | `SC_Boot`, `SC_T_x03_y05`, `SC_Cave_Karst_01` |
| `PF_` | Prefab | `PF_Seed_Pioneer`, `PF_Water_Source` |
| `SO_` | ScriptableObject | `SO_Biome_Fen`, `SO_Tier_Android` |
| `MAT_` | Material | `MAT_Rock_Stilled_01` |
| `TEX_` | Texture | `TEX_Rock_Stilled_Albedo` |
| `SH_` | Shader / Shader Graph | `SH_Water_Surface` |
| `TL_` | Terrain Layer | `TL_Scree_01` |
| `MUS_` | Music | `MUS_Bed_Waking` |
| `AMB_` | Ambience loop | `AMB_Fen_Dawn` |
| `SFX_` | Sound effect | `SFX_Stone_Fracture` |
| `ANIM_` | Animation | `ANIM_Seedling_Sprout` |
| `GRP_` | Addressables group | `GRP_Tile_x03_y05` |

**Tile scenes are `SC_T_x##_y##`** with zero-padded two-digit coordinates, `x` first. This is not cosmetic: the streamer parses the name to resolve a tile coordinate, and a naming test enforces the pattern.

**No spaces, no uppercase in file names, no non-ASCII characters.** Lowercase with underscores for the name part, as above.

---

## 6. LFS policy

Git LFS is required. Binary assets in a normal git object store make the repository unusable within a month.

```bash
git lfs install
git lfs track "*.png" "*.jpg" "*.tga" "*.psd" "*.fbx" "*.wav" "*.mp3" "*.exr" "*.terrainlayer"
```

**Tracked by LFS:** images, audio, models, terrain layers, and any other binary art asset.

**Not tracked by LFS, and git-ignored:** `Library/`, `Temp/`, `Obj/`, `Build/`, `Builds/`, `Logs/`, `UserSettings/`, `MemoryCaptures/`, IDE project files, and the Addressables `ServerData` folder.

### The terrain data exception

**Terrain data stays binary and is not Force Text.** This is a real limitation, not an oversight — Unity does not serialise `TerrainData` as text.

The mitigation is structural: **terrain data is a separate asset referenced by the tile scene**, never embedded in it. A tile edit therefore touches one small binary file rather than a monolithic scene, and two people editing neighbouring tiles do not conflict.

**Do not put tile scenes under LFS as one blob.** Force Text handles the scenes; LFS handles the terrain data and the art.

---

## 7. The two rules that matter most

### Rule one: a cut is recorded in the same commit

When you take a cut from the cut order in Deliverable 5, Section 08, **write it down in the same commit that takes it.**

```
cut(scope): drop weather and day-night

Cut order row 3. Recovers ~1 week. The player loses atmosphere and one of
the five pillars' supporting systems. The pillar is exploration, and the
day-night cycle supports it rather than being it.

Refs: Deliverable 5 S08, R13
```

**Why this is a rule and not a suggestion.** A cut that exists only in your memory becomes a thing you quietly re-add in week fourteen. Then the schedule is wrong again, nobody knows why, and the cut order has been silently invalidated. The cut order is only useful if taking a cut is a recorded event.

**Cut from the top of the order. Do not skip a row. Do not cut two things at once to avoid a hard conversation.**

### Rule two: three things are never cut

At any point, for any reason, under any schedule pressure:

1. **The three shaping tools.** They are the game. A slice without terraforming, water and planting is not a smaller PETRICHOR — it is a different and worse one.
2. **The save system.** Permanence is a pillar. A slice whose world forgets is not a slice of this game.
3. **The three failure messages.** They are the entire tutorial. Cutting them means the Mineral Law has to be explained in text, which is a worse game and a bigger job.

If a schedule can only be met by cutting one of these three, the schedule is wrong. Say so, and re-baseline.

---

## 8. Before you commit

- [ ] The project compiles with **zero errors** in the pinned editor.
- [ ] `Tools/run_bench.sh linux` passes, if the change touches `PET.Water` or `PET.Core`.
- [ ] EditMode and PlayMode tests pass.
- [ ] No new `#if UNITY_ANDROID` under `PET.Edit` or `PET.Save`.
- [ ] No new assembly reference that points **upward**.
- [ ] New assets follow the naming convention.
- [ ] New binary assets are LFS-tracked.
- [ ] If a cut was taken, it is recorded in this commit.
- [ ] If a risk's trigger condition was met, the response was taken and noted.

---

## 9. When you fall behind

The trigger is **two consecutive milestones missing their exit criterion**. Not one — one is normal. Two in a row means the plan is wrong, not the week.

When it fires: **re-read Deliverable 5, Section 08 before taking any cut.** On a one-person project the person who is behind is also the person deciding what to cut, which is the worst possible arrangement for that decision. The written order is the second opinion.
