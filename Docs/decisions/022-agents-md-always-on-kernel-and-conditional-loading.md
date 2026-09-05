# ADR-022: AGENTS.md as always-on kernel + conditional loading

**Status**: Accepted
**Date**: 2026-09-05
**Deciders**: Project owner (milo)

---

## Context

`AGENTS.md` is injected into every agent session (always-on). It grew to 248
lines / 24.4 KB across v2 → v3.3.0 (pitfalls table, workflows, style, ritual,
worktree discipline). External research on AGENTS.md effectiveness (SRI study
via juejin, 2026) shows models reliably follow only ~150–200 independent
instructions and degrade past that ("selective forgetting"); GitHub's 2500+
AGENTS.md analysis recommends keeping rules tight with code examples over
long explanations. A 19-row pitfalls table duplicated content whose single
home already exists in `Docs/lessons/` (one file per pitfall), and
scenario-specific instructions (test / release / openspec) were inlined in
the always-on file despite existing conditional channels (`.opencode/commands`
slash commands, skills, `scripts/dev.ps1`).

## Decision

1. **`AGENTS.md` is a slim always-on kernel** (~190 lines, down from 248).
   Keep: snapshot, invariants (§2), a Top-5 pitfalls table + pointer to
   `Docs/lessons/` (§3), task router (§4), condensed style (§5), workflow
   pointers (§6), error handling (§7), behavior rules + session ritual (§8),
   dev.ps1 quick commands (§9), worktree discipline (§10), agent-skill
   contracts. The full pitfalls table is not inlined; each pitfall keeps its
   single home in `Docs/lessons/` (ADR-019/021 "one home per fact" pattern
   extended to rules content).
2. **Conditional-loading principle**: scenario-specific instructions
   (test / release / openspec) belong in skills or `.opencode/commands`
   slash-commands, never in the always-on file (stated inline in §6).
3. **Verification**: `scripts/dev.ps1 verify-rules` checks that every path
   referenced by AGENTS.md resolves and key files exist — rules references
   become machine-checkable (institutionalizes the G4 session-journal
   empty-promise lesson).
4. **Harness reachability** is documented in `Docs/agents/harness-matrix.md`
   (which harness reads which rules/skills; import-bridge rule for future
   harnesses).

## Rationale

- A 248-line always-on file costs every session context and pushes past the
  ~150–200 instruction reliability limit (SRI); slimming restores budget for
  the instructions that must always be present.
- Pitfalls are scenario knowledge — only relevant when touching the related
  code — so they belong in conditional/on-demand storage (`Docs/lessons/`),
  not always-on.
- `dev.ps1 verify-rules` converts "AGENTS.md references real files" from a
  human habit into a failing check.

## Consequences

### Positive
- Always-on token budget roughly halved; higher-fidelity adherence to the
  instructions that remain.
- One home per fact extended to rules content: pitfalls live only in
  `Docs/lessons/`.
- Rules references become verifiable by `dev.ps1 verify-rules`.
- Harness reachability is explicit, so a new harness doesn't silently lose
  rules/skills.

### Negative
- Agents must do one hop to `Docs/lessons/` when a non-Top-5 pitfall bites
  (small lookup cost, mitigated by the router row + "All lessons" pointer).
- Slimming is a judgment call on what stays inline; future additions must
  respect the conditional-loading rule to avoid re-bloat.
- Achieved ~190 lines rather than the ~160 target: further cuts would remove
  invariant / router / ritual depth; accepted as the better trade-off.

### Neutral
- AGENTS.md version bumped 3.3.0 → 4.0.0 to mark the structural change.

---

## Related Decisions

- ADR-019 — single-source working memory; the "one home per fact" rule
  extends to rules content here.
- ADR-021 — journal size-capping + worktree discipline; keeps the always-on
  file lean and the session ritual cheap.
- `Docs/lessons/` — single home for pitfall knowledge (referenced, not copied).

## Change History

- 2026-09-05: Accepted and implemented (AGENTS.md v4.0.0 + `scripts/dev.ps1
  verify-rules` + `Docs/agents/harness-matrix.md`).
