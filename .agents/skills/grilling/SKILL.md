---
name: grilling
description: Stress-test a plan, decision, or idea as a design tree until you reach a shared understanding. Use when the user wants to grill their thinking or uses any 'grill' trigger phrases. Supports three participation modes — interactive (confirm every answer), auto-with-guardrails (accept recommendations by default, pause only on high-stakes questions), full-auto (accept everything, audit at the end) — for when the user cannot or does not want to decide every node.
---

Interview the user relentlessly until you reach a shared understanding. Map this as a **design tree**: every decision branches into the decisions that hang off it.

Work the tree in **rounds**. The **frontier** is every decision whose prerequisites are already settled: the questions you can ask _now_ without guessing at answers you haven't heard yet. Ask the whole frontier in one round: number each question and give your recommended answer.

Each question should be formatted like so:

```
❓ **Q1** - **<question title>**: <question body, might be multiple paragraphs, including multiple choices>

➡️ <your recommended answer>
```

Each round the user answers reshapes the tree: settled decisions push the frontier outward and unblock questions that depended on them. Recompute the frontier and ask the next round. A question whose answer depends on another question still open in this round belongs to a _later_ round, not this one.

Finding _facts_ is your job, never the user's. When a frontier question needs a fact from the environment (filesystem, tools, etc.), dispatch a sub-agent to find it; don't ask the user for anything you could look up yourself. Don't block on it: a running exploration is an unsettled prerequisite, so only the questions downstream of it wait for the sub-agent to report; ask the rest of the frontier now. The _decisions_ are the user's — unless the user has delegated them.

## Participation modes

Confirm the mode at the start of the session, or infer it from the user's opening words (e.g. "全部按你的建议来", "自动执行", "不用问我" → auto-with-guardrails). Say the chosen mode back so the user can correct you.

- **interactive** (default): ask each question and wait for the user's answer before the next round. Use when the user wants to drive or veto every node.
- **auto-with-guardrails**: accept your recommended answer and move on without waiting, **except** when the question hits a guardrail (below) — then pause and ask. Use when the user trusts the recommendations but wants to keep the veto on high-stakes decisions.
- **full-auto**: accept every recommendation without asking; run the whole tree to the empty frontier, then hand over the audit log. Use when the user explicitly delegates the entire design.

In any auto mode, keep generating the tree, the questions and the recommendations exactly as in interactive mode — nothing about the reasoning changes, only the waiting. Every question is still asked (in the log), still has a recommendation, still gets a decision.

## Guardrails (auto-with-guardrails only)

Pause and ask the user when a frontier question hits any of these. If none hit, accept the recommendation and continue.

1. **External contract**: changes a public or cross-module interface, a persisted format (config / files / schemas), or behaviour other systems depend on.
2. **Hard to reverse**: migration, deletion of data or assets, or any change whose undo cost is meaningful.
3. **User's stated preference**: conflicts with something the user explicitly chose earlier — this session, or recorded in the project's CONTEXT.md / ADRs.
4. **Recorded invariant**: touches a security boundary, a fail-closed policy, or an invariant an ADR records (e.g. single-writer config, permission fail-closed).

When pausing, ask in the standard question format and wait. Also flag (but do not necessarily pause on) **high-leverage** questions whose answer reshapes the tree — range/scope forks where picking wrong wastes real work. Mark them `⚠ high-leverage` in the audit log so the user knows which rows deserve a look.

## Audit log (auto modes)

At the end of the session, deliver an audit log covering every question:

- Q number + title
- The recommendation that was accepted
- One-line rationale
- Mode decision: `auto-accepted` or `paused → user answered`; plus `⚠ high-leverage` when applicable

Present it as a table. Invite the user to veto any row and re-run that branch. Only act on the design once the user confirms — in interactive mode by explicit confirmation, in auto modes by "audit reviewed, no vetoes raised". Shared understanding is not complete until the log has been reviewed.
