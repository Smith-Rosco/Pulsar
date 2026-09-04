# Domain Docs

How the engineering skills should consume this repo's domain documentation when exploring the codebase.

## Before exploring, read these

- **`CONTEXT.md`** at the repo root, or
- **`CONTEXT-MAP.md`** at the repo root if it exists — it points at one `CONTEXT.md` per context. Read each one relevant to the topic.
- **`Docs/decisions/`** — ADRs live here, named `NNN-kebab-case-title.md` (e.g. `012-plugin-runtime-three-seams.md`). Read the ones touching the area you're about to work in.

> **Path convention differs from the default skill text.** The mattpocock skills assume lowercase `docs/adr/`; this repo uses **`Docs/decisions/`**. Always verify the ADR directory by listing it before concluding "no ADRs exist" — silently missing 16 accepted decisions will make an architecture review re-propose things that are already settled.

If any of these files don't exist, **proceed silently**. Don't flag their absence; don't suggest creating them upfront. The `/domain-modeling` skill (reached via `/grill-with-docs` and `/improve-codebase-architecture`) creates them lazily when terms or decisions actually get resolved.

## File structure

Single-context repo (this repo):

```
/
├── CONTEXT.md
├── Docs/
│   └── decisions/
│       ├── 001-plugin-metadata-system.md
│       └── 002-circuit-breaker-for-extension-plugins.md
└── Pulsar/
```

Multi-context repo (presence of `CONTEXT-MAP.md` at the root):

```
/
├── CONTEXT-MAP.md
├── docs/adr/                          ← system-wide decisions
└── src/
    ├── ordering/
    │   ├── CONTEXT.md
    │   └── docs/adr/                  ← context-specific decisions
    └── billing/
        ├── CONTEXT.md
        └── docs/adr/
```

## Use the glossary's vocabulary

When your output names a domain concept (in an issue title, a refactor proposal, a hypothesis, a test name), use the term as defined in `CONTEXT.md`. Don't drift to synonyms the glossary explicitly avoids.

If the concept you need isn't in the glossary yet, that's a signal — either you're inventing language the project doesn't use (reconsider) or there's a real gap (note it for `/domain-modeling`).

## Flag ADR conflicts

If your output contradicts an existing ADR, surface it explicitly rather than silently overriding. Name the ADR by its file in `Docs/decisions/`:

> _Contradicts ADR-0007 (`Docs/decisions/007-external-plugin-permission-consent.md`) — but worth reopening because…_
