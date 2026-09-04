# ADR-021: Journal size-capping + parallel-agent worktree discipline

**Status**: Accepted
**Date**: 2026-09-04
**Deciders**: Project owner (milo)

---

## Context

Two operational problems surfaced on 2026-09-04:

1. **Journal bloat eats unrelated sessions' context.** `Docs/journal/YYYY-MM-DD.md`
   is append-only and uncapped. The daily file reached 24.6 KB (09-03) then
   47 KB / 416 lines (09-04) — ~6× the 7.9 KB of 09-01. The mandatory session
   ritual reads the whole newest day file, so every session — including ones
   for completely unrelated tasks — pays ~10k+ tokens of orientation for a
   record whose per-task relevant slice is a single `## Session` block. The
   ritual is cheap only while day files stay small.

2. **Parallel agents collide in one working directory.** The owner routinely
   runs several AI agents in parallel on the same repo. With a single shared
   working tree, agents clobber each other's edits, commit each other's
   uncommitted changes (observed today: a parallel session committed the main
   worktree's work-in-progress), share `bin/obj` (parallel `dotnet build`
   races), and — if each worked on its own branch with its own journal copy —
   would diverge the single-source journal (ADR-019).

## Decision

### A. Journal: keep the ritual cheap

1. **Session start reads the smallest slice**: `Docs/journal/NEXT.md` (the
   persistent pending list) + the **tail** of the newest day file (last
   `## Session` block only). Older / archived entries are read only when the
   task is related. Enforced in the `session-journal` skill (Step 1) and
   AGENTS.md §8.
2. **Day files are size-capped at ~15 KB.** When appending would exceed the
   cap, `git mv` the full file **verbatim** to
   `Docs/journal/archive/YYYY-MM-DD.md` and open a fresh `YYYY-MM-DD.md` with a
   one-line pointer header. Rotation is a move, never a delete/truncate — git
   history stays the archive (ADR-019 rule 3 unchanged). Day-level archive
   lives under `Docs/journal/archive/`; stale *months* still go to
   `Docs/archive/` per CONTRIBUTING.
3. **`Docs/journal/NEXT.md` becomes the single canonical 下一步 list.** Session
   blocks stop re-accumulating a per-entry next-steps list; they strike
   completed items / append new ones in NEXT.md instead.
4. **Per-entry line budget**: `## Session` blocks are capped at ~25 lines
   (excluding 相关引用). Deep detail belongs in its single home (ADR / lesson /
   CHANGELOG), referenced, not duplicated.

### B. Parallel development: worktree discipline

5. **One worktree per concurrent agent, each on its own branch.** Never run two
   agents in the same working directory. Main worktree = integration + journal
   keeper. (`git worktree add -b feat/<name> <path>`.)
6. **Branch discipline**: one worktree per branch; never check out the same
   branch in two worktrees; rebase onto `main` and fast-forward merge (or PR)
   when done.
7. **`Docs/journal/` + `CHANGELOG.md` are committed on `main` only.** Feature
   worktrees read them via `git fetch` + `git show main:…` for the session
   ritual and never commit a divergent copy on their own branch. Append-only
   journal means even a rare divergence merges cleanly.
8. Build/test isolation is a free side effect: separate `bin/obj` per
   worktree; shared `.git` objects and NuGet cache are read-safe.

### Considered options

- **Journal: read-tail-only without rotation** — cheap ritual, but day files
  keep growing and any full-file read (grep, archive, review) keeps paying;
  rotation also bounds repo size and review cost. Chosen together.
- **Journal: single file with checkpoints** — one file never grows but loses
  the per-day chronology and the append-only invariant; rejected.
- **Worktree: separate clones per agent** — maximal isolation but duplicates
  `.git`/history and needs re-sync of every branch; rejected in favor of
  worktrees (one shared object store).
- **Worktree: no rule on journal** — each branch would carry a diverging
  journal; merge churn and stale cross-session memory; rejected (rule 7).

## Consequences

### Positive
- Session orientation cost is bounded and predictable regardless of total
  journal volume; oversized days no longer penalize unrelated tasks.
- Parallel agents stop clobbering each other; external commits / stale
  worktrees / `bin` races disappear; journal stays single-source on `main`.
- The `session-journal` skill + AGENTS.md now encode both the ritual and the
  concurrency rules in one place.

### Negative
- Rotation splits a day's record across `YYYY-MM-DD.md` + its archive copy
  (pointer header explains it); a small maintenance step on oversized days.
- Worktree setup is one extra command per agent; rule 7 requires agents on
  feature branches to route journal writes through `main` (or accept clean
  append-only merges).

### Neutral
- `Docs/journal/archive/` is a new location distinct from `Docs/archive/`
  (month-level) — documented in the skill to avoid ambiguity.

## Related Decisions

- ADR-019 — cross-harness working memory single source; this ADR extends it
  (rotation is the sanctioned "archive", NEXT.md is the 下一步 owner).
- AGENTS.md §8 (ritual) + §10 (worktree discipline) — enforcement points.
- `session-journal` skill — operational procedure (Steps 1–3).

## Change History

- 2026-09-04: Accepted and implemented via a worktree (branch
  `docs/journal-worktree-discipline`), merged to `main`.
