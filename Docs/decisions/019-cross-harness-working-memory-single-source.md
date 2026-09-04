# ADR-019: Cross-harness working memory lives in Docs/journal

**Status**: Accepted
**Date**: 2026-09-04
**Deciders**: Project owner (milo)

---

## Context

The project is worked from several AI harnesses (WorkBuddy, opencode, and
future ones). Working memory was drifting into two parallel stores:

- `Docs/journal/YYYY-MM-DD.md` — declared by AGENTS.md and CONTRIBUTING
  ("Document Routing") as the canonical cross-session working memory, written
  through the `session-journal` skill, git-tracked.
- `.workbuddy/memory/YYYY-MM-DD.md` — the WorkBuddy harness's native workspace
  memory, **gitignored** (`.gitignore` line ~511), machine-local.

In practice both were written for the same work (duplicated effort, two
granularities of the same facts), while earlier days (2026-09-01..03) existed
only in the gitignored store and were invisible to any other harness, to git
history, and to other machines. The harness memory location is fixed by the
host and cannot be reconfigured.

## Decision

1. **`Docs/journal/` is the single canonical working-memory store** for every
   harness. Per-day files `YYYY-MM-DD.md`, append-only.
2. **No duplicate writes into harness-native memory.** If a host auto-creates
   its own memory file (e.g. `.workbuddy/memory/YYYY-MM-DD.md`), the agent
   writes at most a one-line pointer to `Docs/journal/YYYY-MM-DD.md` — never a
   copy. This is enforced by the `session-journal` skill (Step 0) and AGENTS.md.
3. **Journal files are never deleted.** Git history is the archive. Stale
   months move to `Docs/archive/` per CONTRIBUTING; this overrides any
   host-side "distill and clean up old logs" routine.
4. **History was backfilled**: 2026-09-01..03 migrated verbatim from
   `.workbuddy/memory/` into `Docs/journal/`, and the unique 2026-09-04 content
   was merged; the gitignored originals were then removed.
5. **Language exception**: journal entries are written in Chinese. The
   CONTRIBUTING English-primary rule does not apply to working memory.
6. Durable knowledge still routes to its single home (ADR / lessons /
   CHANGELOG); journal only references it.

### Considered options

- **NTFS junction** (`.workbuddy\memory` → `Docs\journal`): zero discipline,
  but lets host-side log housekeeping delete git-tracked history through the
  link, can surprise other tooling, and does not fix the format/routing
  duplication. Rejected; may be revisited if discipline fails.
- **Keep both stores**: rejected — duplicated effort and split history
  (the status quo).
- **Delete history without backfill**: rejected — 09-01..03 contained
  decisions and pitfalls with no other copy.

## Consequences

### Positive
- One record per project, readable by any harness and machine, reviewable via
  git/PRs.
- The `session-journal` skill is now the single ritual; skill copies in
  `.agents/skills/` and `.opencode/skills/` are kept identical (mirror note).
- AGENTS.md / CONTRIBUTING routing is finally enforced end-to-end.

### Negative
- Journal diff noise enters git history (accepted: it is the record).
- Harness-native auto-memory may still be *suggested* by hosts; agents must
  hold the line via Step 0 of the skill (one-line pointer max).

### Neutral
- Chinese journal body diverges from the English-primary docs rule — explicit
  exception documented here and in CONTRIBUTING.

## Related Decisions

- ADR-012..018 (plugin runtime seams etc.) — content recorded in journal.
- CONTRIBUTING.md "Document Routing" / AGENTS.md conventions — policy basis.

## Change History

- 2026-09-04: Accepted and implemented (backfill + skill + docs).
