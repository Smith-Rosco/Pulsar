---
name: session-journal
description: Cross-session working memory for this project. Use at the START of a session (read the latest entry to pick up where the last session left off) and at the END (append what was done, decisions, and next steps). Also use when the user asks to "记录", "写日志", "journal", "续上之前", or starts work after a gap.
---

# Session Journal

Pulsar keeps a lightweight per-day working memory under `Docs/journal/` so a new
session — or a new AI agent — can resume without re-reading chat history. This
skill is the ritual for reading and appending it.

## File layout

- One file per day: `Docs/journal/YYYY-MM-DD.md`
- Entries append chronologically; newest entry at the bottom
- Shared with the team / future sessions via git, like any other doc

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
- 文件: <path>
```

3. Keep it tight — bullet fragments, not prose. This is working memory, not a
   report.
4. If an OpenSpec change was active, note it in 相关引用 so the next session can
   resume it.

## Rules

- Only append; never rewrite or reorder past entries.
- Never put secrets or personal data in the journal.
- Do not auto-append silently — show the user what you wrote (the added block)
  and let them edit before you commit.
- If a "下一步" item from a previous session gets completed, strike it through
  (`~~...~~`) rather than deleting it, to preserve history.
