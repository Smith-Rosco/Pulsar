## Why

`openspec` CLI (1.12) validation requires every spec file to contain `## Purpose` and `## Requirements` sections. 40 of the 78 specs under `openspec/specs/` are legacy change-delta fragments — their files begin directly with `## ADDED Requirements` / `## MODIFIED Requirements` headers from archived changes and were never normalized into standalone spec format. As a result `openspec validate --all` currently reports 40 failures ("Spec must have a Purpose section").

## What Changes

- Add a `## Purpose` section to each of the 40 failing legacy specs, derived from that spec's own requirement content (no invented behavior).
- Flatten legacy `## ADDED Requirements` / `## MODIFIED Requirements` delta section headers into a single `## Requirements` section per spec, preserving every requirement and scenario verbatim.
- Add the `# <capability-name>` H1 title line where missing, matching the canonical format already used by the 38 passing specs.
- No requirement text, scenario content, or capability behavior is modified — this is a spec-store formatting normalization, not a behavior change.

## Capabilities

### New Capabilities

- None.

### Modified Capabilities

- None. Requirement content is unchanged; only file-level headers are added/normalized. Per the spec-driven contract this is a docs-normalization change, so the change declares `skip_specs: true` in `.openspec.yaml` instead of shipping zero-content delta files.

## Impact

- Files touched: 40 `openspec/specs/<capability>/spec.md` (list maintained in tasks.md).
- No runtime code, configuration, plugin behavior, or requirement semantics affected.
- `openspec validate --all` goes from 45 passed / 40 failed to 85 passed / 0 failed.
