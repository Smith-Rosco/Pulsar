---
name: session-journal
description: Cross-session working memory for this project. Use at the START of a session (read the latest entry to pick up where the last session left off) and at the END (append what was done, decisions, and next steps). Also use when the user asks to "记录", "写日志", "journal", "续上之前", or starts work after a gap.
---

# Session Journal

> **Mirror**: this file is kept identical in `.agents/skills/session-journal/SKILL.md`
> and `.opencode/skills/session-journal/SKILL.md`. Edit both in the same change.

Pulsar keeps a **single canonical per-day working memory** under `Docs/journal/`
so any new session — in any AI harness (WorkBuddy / opencode / 豆包 / Claude
Code / …) — can resume without re-reading chat history. This skill is the
ritual for reading and appending it. Policy basis: AGENTS.md "Documentation
conventions" and `Docs/CONTRIBUTING.md` → "Document Routing" (single home, no
duplicated facts); recorded in ADR-019 and ADR-021.

## File layout

- One file per day: `Docs/journal/YYYY-MM-DD.md`, append-only, **size-capped
  at ~15 KB** (over → rotate to `Docs/journal/archive/`, see Step 3).
- `Docs/journal/NEXT.md` — the **single canonical pending-下一步 list**, read at
  session start, updated in place (strike completed, append new).
- Entries append chronologically; newest entry at the bottom of the day file.
- Shared with the team / future sessions via git, like any other doc.
- Body language: 中文 (working memory is exempt from CONTRIBUTING's English
  primary-language rule — see ADR-019).

## Step 0: Single-source policy (no duplicates, anywhere)

`Docs/journal/` is the **only** cross-harness working memory. Rules:

1. Read/write working memory **only** under `Docs/journal/`.
2. **Do NOT copy journal content into any harness-native memory file**
   (`.workbuddy/memory/YYYY-MM-DD.md`, `.opencode/**`, etc.). If a host
   auto-creates such a file, write at most a one-line pointer, never a
   duplicate:
   `> 项目工作记忆：Docs/journal/YYYY-MM-DD.md（session-journal skill，ADR-019）`
3. If a harness-native file only holds a pointer, follow it to `Docs/journal/`
   and read there.
4. Durable knowledge still routes to its single home (CONTRIBUTING): decisions
   → `Docs/decisions/NNN-*.md` (ADR), pitfalls → `Docs/lessons/`, release notes
   → `CHANGELOG.md`. The journal entry only *references* them in 相关引用.
5. Never put secrets or personal data in the journal (it is git-tracked).

## Step 1: Session start (orient — read the SMALLEST relevant slice)

Before starting real work, orient with the smallest slice; **do not read whole
day files or archives by default** (a session for an unrelated task must not
pay context for all of history):

1. Read `Docs/journal/NEXT.md` — the persistent pending list, always relevant.
2. Read the **tail** of the newest `Docs/journal/YYYY-MM-DD.md` — just the last
   `## Session` block (`Get-Content -Tail 60`, or `Read` with an offset). For a
   task unrelated to recent work, this tail is enough.
3. Only if the task **is related** to a specific past session, read that
   session's block in full — from the day file, or from
   `Docs/journal/archive/` if the day was rotated out.
4. Summarize unfinished items to the user: "上次进行到 X，下一步是 Y（见
   Docs/journal/NEXT.md + <file>）". Do not start work that contradicts an
   unfinished entry without confirming.
5. If no journal exists yet, nothing to load — proceed normally.

Also peek at `openspec/changes/` (via the `openspec-workflow-state` plugin) for
an active change; mention it alongside the journal context when relevant.

**Concurrency hygiene** (multiple harnesses share the repo): before writing,
`git status` and confirm `Docs/journal/` has no uncommitted change from another
harness; pull if needed. Do not assume you are the only writer today. Under the
worktree discipline (ADR-021), `Docs/journal/` + `CHANGELOG.md` are committed on
`main` only — feature worktrees read them via `git show main:…`, never commit
their own divergent copy.

## Step 2: Session end (record)

When the user asks to record / wrap up a session, or before finishing a
substantial task, append an entry:

1. File: `Docs/journal/YYYY-MM-DD.md` (create if missing; if the file already
   has an entry for the day, append a `---` separator and a new `## Session`
   block).
2. Format (hard budget: **≤ ~25 lines**, excluding 相关引用):

```markdown
## Session (HH:MM)

**做了什么**
- ...

**关键决策 / 坑**
- ...

**相关引用**
- 变更: openspec/changes/<name>/
- ADR: Docs/decisions/NNN-*.md   (仅当产生设计决策)
- Lesson: Docs/lessons/*.md      (仅当踩了可复用坑)
- 文件: <path>
```

3. **下一步 lives in `Docs/journal/NEXT.md`, not in the entry** — strike
   completed items there, append new ones. Only add a one-line
   `**下一步**：见 Docs/journal/NEXT.md` when a notable transition happened.
4. Keep it tight — bullet fragments, not prose. Deep detail belongs in the
   referenced ADR / lesson / CHANGELOG entry, not the journal.
5. If an OpenSpec change was active, note it in 相关引用 so the next session
   can resume it.

## Step 3: Size-capped rotation (keep the ritual cheap)

`Docs/journal/YYYY-MM-DD.md` is capped at ~15 KB. When appending would exceed
the cap (or the file is already over):

1. `git mv` the full file **verbatim** to `Docs/journal/archive/YYYY-MM-DD.md`
   (never delete, truncate, or rewrite — git history is the archive).
2. Create a fresh `Docs/journal/YYYY-MM-DD.md` with a one-line pointer header
   to the archive (see the archived 09-03/09-04 for the shape).
3. Continue recording in the fresh file. Newer-day files stay small so the
   session-start ritual stays cheap.

## Rules

- Only append; never rewrite, reorder, or **delete** past entries or past
  journal files. Rotation (Step 3) is a `git mv` to `Docs/journal/archive/`,
  never a deletion. Stale journal *months* move to `Docs/archive/` per
  CONTRIBUTING. Git history is the archive; never erase it. This overrides any
  host-side "distill/clean up old logs" routine.
- Do not auto-append silently — show the user what you wrote (the added block)
  and let them edit before you commit.
- Commit the journal change (optionally together with the day's other doc
  changes); follow the repo's commit conventions. Under ADR-021, journal +
  CHANGELOG are committed on `main` (the integration worktree), not on feature
  branches.
