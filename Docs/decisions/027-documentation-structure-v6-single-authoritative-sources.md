# ADR-027: Documentation Structure v6 — Single Authoritative Sources and Full Index Registry

**Status**: Accepted
**Date**: 2026-09-08
**Deciders**: milo (owner), WorkBuddy agent (audit + implementation)
**Audit Report**: `Docs/reports/2026-09-07-DOC_STRUCTURE_AUDIT.html`

---

## Context

A 2026-09-07 audit of all non-code documentation (openspec/ excluded by
decision) found:

1. **Plugin-system concepts written three times** — `ARCHITECTURE.md` §2.2/§3,
   `PLUGIN_DEVELOPMENT.md` 核心概念, and `Docs/architecture/PLUGIN_SYSTEM.md`
   restated tiers, Circuit Breaker, runtime kernel, and `PulsarContext` with
   drift (e.g. `ARCHITECTURE.md` self-contradictory version stamps, a newer
   `PulsarContext` immutability note existing in only one copy).
2. **The docs index was unreliable** — `Docs/README.md` v5.2.0 registered 11
   directories while `Docs/` had 17 first-level entries; counts were stale.
3. **Three overlapping "how we work" docs** — `DEVELOPER.md`,
   `Docs/CONTRIBUTING.md` (whose `.draft.md` lifecycle had zero real usage and
   whose English-primary rule was contradicted by practice), and `AGENTS.md`.
4. **Release-notes content in three places** (root template, versioned
   instance, CHANGELOG sections).
5. **Structural smells both ways** — over-fine (`roadmap/`, `proposals/`,
   `diagrams/` with 2–3 files each) and over-flat (`archive/` with 62 files in
   one directory, 54 from a single month).
6. **Plugin script directories unindexed** — `VbaRunner`/`BookmarkletRunner`
   `TestScripts`/`DemoScripts` and `Pulsar/Samples` had no README and no index
   entry, though their locations were a deliberate prior decision (openspec
   `2026-06-16-project-structure-cleanup`) and `DemoScripts` files are
   referenced by `Pulsar.csproj`.

## Decision

1. **Single authoritative sources** (others link, never restate):
   - Plugin-system *concepts* → `Docs/architecture/PLUGIN_SYSTEM.md`
   - Plugin *interface reference & how-to* → `PLUGIN_DEVELOPMENT.md`
   - System *overview* → `ARCHITECTURE.md` (detail sections collapsed)
   - Developer *onboarding* → `DEVELOPER.md` (recaps replaced by a summary
     table linking to sources)
   - Documentation *rules* → `Docs/CONTRIBUTING.md` v2.0.0
   - Operational procedures → `Docs/ops/`
   - Release-notes *template* → `Docs/ops/TEMPLATE_RELEASE_NOTES.md`
2. **Full index registry**: `Docs/README.md` v6.0.0 registers every first-level
   entry; every doc add/move/delete must update it in the same change.
3. **Directory consolidation**: `roadmap/` + `proposals/` → `Docs/planning/`;
   `diagrams/` → `Docs/architecture/`; `Docs/Plugins/` → `Docs/plugins/`;
   `archive/` bucketed by month (`2026-03/` …); orphan `Docs/screenshots/`
   deleted; retired `Docs/demo/` (all contents were gitignored and superseded
   by `Docs/media/release/demos/`).
4. **Plugin script directories managed in place** (respecting the 2026-06-16
   openspec decision and csproj links): each gets a README stating purpose,
   csproj coupling, and the governing spec.
5. **Language policy** (CONTRIBUTING v2.0.0): user-facing content bilingual
   (README zh/en, manual en/zh); AI/contributor-facing content Chinese-first
   with English grep-able keywords; journal Chinese (ADR-019).

## Consequences

### Positive
- One fact, one home: concept edits can no longer silently drift across copies.
- The index is trustworthy again; onboarding and AI routing start from one map.
- `Docs/` first-level entries 21 → 17 (13 directories + 4 loose files); root md files 10 → 9.
- Rollback safety: implemented as a sequence of small commits (see CHANGELOG).

### Negative
- Historical documents and CHANGELOG entries referencing old paths
  (`Docs/Plugins/`, `roadmap/`, `proposals/`) are intentionally not rewritten
  (append-only history); only living documents were link-fixed.
- `Docs/demo/` local-only raw captures were lost to an external-process
  deletion incident before retirement; no tracked content was affected
  (everything in it was gitignored and superseded).

### Neutral
- `Docs/agents/` stays machine-consumed and unrelocatable (hardcoded paths in
  vendored skills) but is now registered in the index.

## Related Decisions

- [ADR-019: Cross-harness working memory single source](./019-cross-harness-working-memory-single-source.md)
- [ADR-021: Journal rotation and parallel worktree discipline](./021-journal-rotation-and-parallel-worktree-discipline.md)
- openspec `2026-06-16-project-structure-cleanup` (script directory placement)
