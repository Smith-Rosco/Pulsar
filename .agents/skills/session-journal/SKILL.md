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
duplicated facts); recorded in ADR-019.

## File layout

- One file per day: `Docs/journal/YYYY-MM-DD.md`
- Entries append chronologically; newest entry at the bottom
- Shared with the team / future sessions via git, like any other doc
- Body language: 中文 (working memory is exempt from CONTRIBUTING's English
  primary-language rule — see ADR-019)

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

## Step 1: Session start (orient)

Before starting real work, read the most recent journal:

1. List `Docs/journal/` (Glob or Bash `ls`).
2. Read the newest `*.md` file.
3. If the last entry has an **unfinished "Next steps"** list, summarize it to
   the user: "上次进行到 X，下一步是 Y（见 Docs/journal/<file>）". Do not start
   work that contradicts an unfinished entry without confirming.
4. If no journal exists yet, nothing to load — proceed normally.

Also peek at `openspec/changes/` (via the `openspec-workflow-state` plugin) for
an active change; mention it alongside the journal context when relevant.

**Concurrency hygiene** (multiple harnesses share the repo): before writing,
`git status` and confirm `Docs/journal/` has no uncommitted change from another
harness; pull if needed. Do not assume you are the only writer today.

## Step 2: Session end (record)

When the user asks to record / wrap up a session, or before finishing a
substantial task, append an entry:

1. File: `Docs/journal/YYYY-MM-DD.md` (create if missing; if the file already
   has an entry for the day, append a `---` separator and a new `## Session` block).
2. Format:

```markdown
## Session (HH:MM)

**做了什么**
- ...

**关键决策 / 坑**
- ...

**下一步**
- [ ] ...

**相关引用**
- 变更: openspec/changes/<name>/
- ADR: Docs/decisions/NNN-*.md   (仅当产生设计决策)
- Lesson: Docs/lessons/*.md      (仅当踩了可复用坑)
- 文件: <path>
```

3. Keep it tight — bullet fragments, not prose. This is working memory, not a
   report.
4. If an OpenSpec change was active, note it in 相关引用 so the next session can
   resume it.

## Rules

- Only append; never rewrite, reorder, or **delete** past entries or past
  journal files. Archive stale journal months to `Docs/archive/` per
  CONTRIBUTING instead — git history is the archive, never erase it. This
  overrides any host-side "distill/clean up old logs" routine.
- Do not auto-append silently — show the user what you wrote (the added block)
  and let them edit before you commit.
- If a "下一步" item from a previous session gets completed, strike it through
  (`~~...~~`) rather than deleting it, to preserve history.
- Commit the journal change (optionally together with the day's other doc
  changes); follow the repo's commit conventions.
