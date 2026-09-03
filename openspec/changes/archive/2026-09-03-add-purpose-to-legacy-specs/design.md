## Context

See proposal.md — Why. In short: 40 specs under `openspec/specs/` are legacy change-delta fragments (files starting with `## ADDED Requirements` / `## MODIFIED Requirements`) that predate the current validation rules, which require `## Purpose` + `## Requirements` headers. The 38 already-passing specs define the canonical target format:

```markdown
# <capability-name>

## Purpose
<one short paragraph>

## Requirements

### Requirement: ...
#### Scenario: ...
```

Constraints: spec files contain non-ASCII content (emoji in scenario text, e.g. ✅/⚠️/🛑/⏳) and the repo runs on Windows, so encoding (UTF-8) and line endings must be preserved exactly.

## Goals / Non-Goals

**Goals:**

- Every failing spec gains a `## Purpose` section, a single `## Requirements` section, and an H1 title — i.e. `openspec validate --all` passes (85/85).
- Requirement and scenario bodies are preserved byte-for-byte apart from the removed/renamed section headers.
- The transformation is deterministic and reviewable (scripted, one commit-style pass).

**Non-Goals:**

- No rewording, merging, splitting, or re-scoping of any requirement or scenario.
- No cross-file de-duplication: if a legacy delta fragment restates a requirement that also exists in another spec file, both stay as-is (pre-existing condition, out of scope here).
- No changes to `changes/archive/*`, `config.yaml`, or any runtime code.

## Decisions

1. **`skip_specs: true` instead of shipping delta files.** The change modifies file headers only; no requirement is added/modified/removed, so there is no meaningful delta to express in `specs/**/*.md`. Shipping synthetic deltas (e.g. empty `## ADDED Requirements`) would poison future `openspec archive` merges. Alternative considered — one "MODIFIED" delta per spec with a placeholder requirement — rejected as invented content that could duplicate real requirements on archive.
2. **Flatten in place.** Each legacy fragment is already the de-facto current spec for its capability (validation treats it as one). `## ADDED` / `## MODIFIED Requirements` headers are dropped and their `### Requirement:` blocks move under a single `## Requirements`. Alternative considered — merging fragments into the capability that originally owned the requirement — rejected: requires requirement-level archaeology across archived changes and risks silently dropping or duplicating content; out of scope for a validation fix.
3. **Scripted batch transformation.** A Python script performs header surgery (detect delta headers, splice `## Requirements`, insert Purpose text from a per-spec map) with explicit UTF-8 handling and newline preservation, followed by `openspec validate --all` as the gate. Alternative considered — 40 manual edits — rejected as error-prone and non-reviewable as a whole.
4. **Purpose text: English, derived from the spec's own requirements.** Matches the language of all existing spec content. One to three sentences naming the capability's responsibility and the guarantees its requirements encode. No new SHALL statements in Purpose (Purpose is prose, not requirements).

## Risks / Trade-offs

- [Legacy "MODIFIED" fragments may overlap requirements that live in other spec files] → Flatten in place, run a duplicate-requirement-heading scan before/after, and report (not fix) any pre-existing duplication.
- [Encoding/newline corruption on Windows] → Script reads/writes with explicit UTF-8 and preserves the file's existing dominant line ending; validation + `git diff --stat` review after.
- [AI-written Purpose prose could misstate a capability] → Purposes are derived strictly from requirement titles/content of that spec; final gate is `openspec validate --all`, and the prose is easy to amend later without touching requirements.

## Migration Plan

Apply is idempotent per file: run transformation → `openspec validate --all` → expect 85/85. Rollback is `git checkout -- openspec/specs/` (files are tracked in git). No deployment step; the spec store is documentation.

## Open Questions

None.
