# Decision records

One file per decision that was **made and could have gone the other way**.

This directory exists because the design package records what was *chosen*. It does not
record what was *rejected*, and a rejected option is the thing that gets re-proposed six
weeks later by someone who has forgotten why it was rejected — including by the person
who rejected it.

A decision record is not a design document. It is short, it is written at the moment of
the decision, and it is never edited afterwards. If a decision is reversed, the reversal
is a **new** record that supersedes the old one; the old one stays, because the reason it
was wrong is itself worth keeping.

---

## When to write one

Write one when all three are true:

1. **A choice was made** between real alternatives — not a default that nobody considered.
2. **The choice is expensive to reverse** — it constrains code, content, or the schedule.
3. **The reasoning is not obvious from the result.** If the code already says why, the
   record is noise.

Do **not** write one for a decision that is already recorded somewhere authoritative. The
rung ladder lives in `Tools/bench_thresholds.json`; the cut order lives in Deliverable 5
Section 08; the assembly dependency rule lives in `CONTRIBUTING.md` §1. A decision record
that restates one of those is a second copy that will drift.

---

## Filename convention

```
NNNN-slug.md
```

| Field | Rule | Example |
|---|---|---|
| `NNNN` | Four digits, zero-padded, assigned in order. Never reused, never renumbered. | `0007` |
| `slug` | Lowercase, hyphens, no spaces. Short enough to read in a directory listing. | `channel-graph-on-android` |

Zero-padding is what makes a plain `ls` sort in decision order. Four digits is enough for
ten thousand decisions, which is more than this project will ever have.

**Superseding a record does not change its filename.** The new record gets the next number
and says which one it supersedes.

---

## Template

```markdown
# NNNN — <the decision, as a sentence>

- **Date:** YYYY-MM-DD
- **Status:** accepted | superseded by NNNN
- **Supersedes:** NNNN (omit if none)

## Context

What forced a decision. Two or three sentences. Facts, not narrative.

## Options

| Option | What it costs | Why it was not chosen |
|---|---|---|
| A | | |
| B | | |

## Decision

What was chosen, in one sentence, in the active voice.

## Consequences

What this makes easy, and what it makes hard. Include the cost that was accepted
knowingly — a record with no downside is a record that was not written honestly.

## Revisit if

The condition that would make this decision wrong. If there is none, say so.
```

---

## What is deliberately not here

**No decision records have been written yet.** The decisions made so far — URP over HDRP,
the basin, the twelve assemblies, the rung ladder — are all recorded in the design package
and in the repository's own files, which is where they belong. This directory is for the
decisions that come next, starting with the ones the first gate run will force.

Writing records for decisions that are already documented would create a second copy of
the design package, and the second copy is always the one that goes stale.
