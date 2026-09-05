# Harness × Rules/Skills Loading Matrix

> Living reference (ADR-022 companion): how each AI harness in use reaches the
> repo's rules and skills. Update this row when a harness or a loading
> mechanism changes. Single-source rules: `AGENTS.md` (always-on kernel) +
> `Docs/lessons|decisions` (pointer targets) + skills (conditional loading).
> Working memory: `Docs/journal/` only (ADR-019).

## In use

| Harness | Rules entry | Skills dir | Conditional channel | Notes |
|---|---|---|---|---|
| opencode | `AGENTS.md` (cwd + parents, native) | `.opencode/skills/` | `.opencode/commands/` slash commands | slash commands are the "conditional loading" channel (openspec opsx-*) |
| 豆包 / Doubao | `AGENTS.md` (project root) | injected list may miss `.agents/skills/` → read by path (AGENTS.md §8) | — | `session-journal` not always surfaced; locate at `.agents/skills/session-journal/SKILL.md` |
| WorkBuddy | `AGENTS.md` (project root) | `.agents/skills/` | — | `.workbuddy/memory/` = one-line pointer only (ADR-019), never journal body |

## Not yet wired (bridge rules when added)

- **Claude Code**: create `CLAUDE.md` with `@AGENTS.md` import — never copy content.
- **Cursor**: reads `AGENTS.md` natively; only glob-typed rules go in `.cursor/rules/`.
- **GitHub Copilot / Codex CLI**: read `AGENTS.md` natively at repo root.

## Adding a harness

1. Add a row with its rules entry + skills dir.
2. Cannot read `AGENTS.md` natively → bridge by import (`@AGENTS.md`), never duplicate.
3. Mirror new skills into the dirs that harness scans; keep `session-journal` mirrored at `.agents/skills/` + `.opencode/skills/`.
4. Verify reachability with `scripts/dev.ps1 verify-rules`.
