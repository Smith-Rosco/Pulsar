# Documentation Contributing Guide

**Version**: v2.0.0
**Last Updated**: 2026-09-08
**Supersedes**: v1.0.0 (2026-03-01) — the `.draft.md` lifecycle and the English-primary rule from v1.0.0 were never followed in practice and are removed here.

---

## Overview

This guide defines how Pulsar documentation is created, routed, and retired. The
operational (build/test/commit) rules live in [AGENTS.md](../AGENTS.md); this
file covers documents only.

Two principles govern everything below:

1. **One fact, one home.** A fact lives in exactly one authoritative file;
   every other mention links to it. Never copy concepts between documents —
   copied text drifts.
2. **The index is the contract.** Every document that is added, moved, or
   deleted MUST be reflected in [Docs/README.md](./README.md) in the same
   change. A document not in the index does not exist.

---

## Language Policy (v2.0.0)

The v1.0.0 "English-primary" rule is abolished — it was contradicted by the
repo's own practice (journal, manual/zh, plugin docs, reports are Chinese).

| Audience | Language |
|---|---|
| User-facing (README, README_EN, Docs/manual/) | Bilingual — README keeps zh/en pair in sync; manual keeps en/zh trees in sync |
| AI- and contributor-facing (AGENTS.md, guides, lessons, architecture, ADRs, ops) | Chinese-first is acceptable; grep-able keywords (`Symptom:`, `Root cause:`, `Rule (TL;DR):`) stay English |
| `Docs/journal/` | Chinese (owner's working language; ADR-019) |
| Historical documents | Original language, never retro-translated |

---

## Document Lifecycle

1. **Draft** — write in its final home directory; no `.draft.md` suffix needed
   (practice showed the suffix adds a rename step with zero review value).
2. **Published** — standard naming (below), registered in `Docs/README.md`.
3. **Archived** — moved to `Docs/archive/YYYY-MM/` with its date prefix kept;
   add a one-line note at top: `> ARCHIVED YYYY-MM-DD — superseded by <link>`
   when a replacement exists. Never rewrite archived content.
4. **Deleted** — only for zero-reference orphans (verify with a repo-wide grep
   before deleting). Git history is the archive of last resort.

ADRs are immutable once accepted: supersede (ADR-NNN header `Superseded by
ADR-XXX`), never edit.

---

## Naming Conventions

| Kind | Format | Location |
|---|---|---|
| Root-level core docs | `UPPERCASE_WITH_UNDERSCORES.md` | repo root |
| Guides / lessons / architecture / ops | `UPPERCASE_WITH_UNDERSCORES.md` | `Docs/<dir>/` |
| ADRs | `NNN-descriptive-title.md` (zero-padded, sequential) | `Docs/decisions/` |
| Archive | `YYYY-MM-DD-DESCRIPTIVE_NAME.md` | `Docs/archive/YYYY-MM/` |
| Daily journal | `YYYY-MM-DD.md` | `Docs/journal/` |

---

## Document Routing: specs vs ADR vs lessons vs journal vs report

When a change produces knowledge, route it to exactly **one** home by what it
answers. Do not copy the same fact into multiple locations.

| Knowledge kind | Answers | Home | When to write |
|---|---|---|---|
| Behavioral spec | **What** the system must do (`SHALL`, WHEN-THEN scenarios) | `openspec/specs/<capability>/spec.md` | via `/opsx-sync` / archive |
| Architecture decision | **Why** we chose this over alternatives (trade-offs, rejected options) | `Docs/decisions/NNN-*.md` (ADR) | when an architectural choice lands |
| Lesson / pitfall | **What broke and how to avoid it** (symptom → root cause → fix) | `Docs/lessons/*.md` | after fixing a non-obvious bug |
| Working memory | **Where things stand** (progress, next steps, open questions) | `Docs/journal/YYYY-MM-DD.md` | session start/end via `session-journal` skill |
| Point-in-time research / comparison | **What the landscape looked like** at date X | `Docs/reports/` (with a validity note) | market eval, benchmark, one-off analysis |

**Decision flow** — ask "what does this answer?":
1. It defines system behavior → `openspec/specs/` (proposal/design lives in `openspec/changes/` until merged)
2. It records a human trade-off the code can't show → ADR in `Docs/decisions/`
3. It's a hard-won fix that should not recur → `Docs/lessons/`
4. It's transient progress → `Docs/journal/` (never promote to specs/lessons)
5. It's a dated snapshot of research or comparison → `Docs/reports/` (label the validity date; archive when stale)

**Working memory is single-source (ADR-019)**: `Docs/journal/` is the only
cross-harness working-memory store. AI harnesses that maintain their own local
memory (`~/.workbuddy/memory/`, `.opencode/**`, etc.) must **not** copy journal
content into those files — at most a one-line pointer back to
`Docs/journal/YYYY-MM-DD.md`.

---

## Size & Authority Discipline (v2.0.0)

- **One file = one topic.** If a file answers two unrelated questions, split it
  or rename it to the narrower truth.
- **Authoritative sources are named, not implied.** The current map:
  - Plugin system *concepts* (tiers, Circuit Breaker, lifecycle, PulsarContext):
    `Docs/architecture/PLUGIN_SYSTEM.md`
  - Plugin *development how-to* (interfaces, manifests, checklists):
    `PLUGIN_DEVELOPMENT.md` (repo root)
  - System architecture *overview*: `ARCHITECTURE.md` (repo root)
  - Terminology: `CONTEXT.md`
  - Change history: `CHANGELOG.md`
  - Operational procedures: `Docs/ops/`
  - Documentation rules: this file
- When two documents must say the same thing, the second one links to the
  first — it does not restate it. If you find yourself pasting a section from
  another doc, stop and link instead.
- Keep root-level docs lean. Detail belongs in `Docs/` subdirectories; the root
  file routes to it.

---

## AI-Optimized Writing Rules

- Put the decision or constraint near the top; details later.
- Use consistent grep-able keywords: `Symptom:` · `Root cause:` · `Correct pattern:` · `Incorrect pattern:` · `Applies to:` · `Rule (TL;DR):`
- Prefer tables for comparisons; keep code samples minimal and canonical.
- Use relative links with descriptive text:
  ✅ See [Plugin Development Guide](../PLUGIN_DEVELOPMENT.md)
  ❌ See [here](../PLUGIN_DEVELOPMENT.md)

---

## Quality Checklist

Before publishing a document, verify:

- [ ] Naming matches the table above
- [ ] Registered in `Docs/README.md` (or index entry updated)
- [ ] No concept restated from another file — linked instead
- [ ] Links valid (no broken relative paths)
- [ ] Grep-able keywords present (lessons) / Status header present (ADR)
- [ ] Related documents cross-referenced

---

## Change History

- v2.0.0 (2026-09-08): Rewritten after docs-structure audit (see
  `Docs/reports/2026-09-07-DOC_STRUCTURE_AUDIT.html`). Removed unused `.draft.md`
  lifecycle; replaced English-primary rule with audience-based language policy;
  added size/authority discipline, reports routing, archive month-buckets,
  mandatory index sync.
- v1.0.0 (2026-03-01): Initial version.
