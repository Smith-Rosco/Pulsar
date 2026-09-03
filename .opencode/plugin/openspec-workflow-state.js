/* global process */
/**
 * Pulsar OpenSpec Workflow-State Injection Plugin
 *
 * Every model request, inject a compact <openspec-state> breadcrumb into the
 * in-memory copy of the latest user message via
 * `experimental.chat.messages.transform`. Stored history and the TUI are not
 * modified (same technique Trellis uses).
 *
 * The state is derived from OpenSpec as the single source of truth — the most
 * recently modified active change under `openspec/changes/` (excluding
 * `archive/`): its name, which planning artifacts already exist (proposal,
 * design, specs/, tasks, qa-checklist), and an inferred current phase.
 *
 * No npm dependencies. Plain ESM, mirrors Trellis's plugin shape so opencode
 * auto-discovers it from `.opencode/plugin/`.
 */

import { appendFileSync, existsSync, readdirSync, statSync } from "fs"
import { join } from "path"

const ARCHIVE_DIR = "archive"
const MARKERS = [
  ["proposal.md", "proposal"],
  ["specs", "specs"],
  ["design.md", "design"],
  ["tasks.md", "tasks"],
  ["qa-checklist.md", "qa"],
]

function debugLog(prefix, ...args) {
  if (process.env.TRELLIS_HOOKS === "0" || process.env.TRELLIS_DISABLE_HOOKS === "1") return
  if (process.env.OPENCODE_NON_INTERACTIVE === "1") return
  if (process.env.OPENSPEC_PLUGIN_DEBUG !== "1") return
  try {
    appendFileSync(
      join(process.env.OPENSPEC_PLUGIN_LOG || process.cwd(), "openspec-plugin-debug.log"),
      `[${new Date().toISOString()}] [${prefix}] ${args.join(" ")}\n`,
    )
  } catch {
    /* ignore */
  }
}

function isDirectoryPath(rel) {
  return rel.endsWith("/") || rel === "specs"
}

function artifactExists(changeDir, rel) {
  const full = join(changeDir, rel)
  if (isDirectoryPath(rel)) {
    try {
      if (!statSync(full).isDirectory()) return false
      return readdirSync(full).length > 0
    } catch {
      return false
    }
  }
  return existsSync(full)
}

function findLatestUserMessageIndex(messages) {
  if (!Array.isArray(messages)) return -1
  for (let i = messages.length - 1; i >= 0; i--) {
    if (messages[i]?.info?.role === "user") return i
  }
  return -1
}

function findUserTextPart(parts) {
  if (!Array.isArray(parts)) return undefined
  return parts.find(
    (part) => part?.type === "text" && part.synthetic !== true && part.text !== undefined,
  )
}

function prependEphemeralText(messages, text) {
  if (!Array.isArray(messages) || typeof text !== "string") return false
  const index = findLatestUserMessageIndex(messages)
  if (index < 0) return false
  const original = messages[index]
  const parts = Array.isArray(original.parts) ? original.parts.slice() : []
  parts.unshift({ type: "text", text, synthetic: true })
  messages[index] = { ...original, parts }
  return true
}

function activeChange(directory) {
  const changesDir = join(directory, "openspec", "changes")
  if (!existsSync(changesDir)) return null
  let entries
  try {
    entries = readdirSync(changesDir, { withFileTypes: true })
  } catch {
    return null
  }
  const changes = entries
    .filter((e) => e.isDirectory() && e.name !== ARCHIVE_DIR && !e.name.startsWith("."))
    .map((e) => {
      let mtime = 0
      try {
        mtime = statSync(join(changesDir, e.name)).mtimeMs
      } catch {
        /* ignore */
      }
      return { name: e.name, mtime }
    })
    .sort((a, b) => b.mtime - a.mtime)
  return changes[0] || null
}

function inferPhase(done) {
  if (done.includes("qa")) return "review"
  if (done.includes("tasks")) return "implement"
  if (done.includes("proposal") || done.includes("design") || done.includes("specs")) return "plan"
  return "triaging"
}

function buildBreadcrumb(directory) {
  const change = activeChange(directory)
  if (!change) {
    return `<openspec-state>\nNo active OpenSpec change under openspec/changes/. Start one with /opsx-propose "<idea>".\n</openspec-state>`
  }
  const changeDir = join(directory, "openspec", "changes", change.name)
  const done = []
  const missing = []
  for (const [rel, label] of MARKERS) {
    ;(artifactExists(changeDir, rel) ? done : missing).push(label)
  }
  const phase = inferPhase(done)
  return [
    "<openspec-state>",
    `Active change: ${change.name} (phase: ${phase})`,
    `Artifacts done: ${done.length ? done.join(", ") : "(none)"}`,
    `Artifacts missing: ${missing.length ? missing.join(", ") : "(none)"}`,
    "Track progress with `openspec status --change <name>`. Planning = /opsx-propose, implementation = /opsx-apply.",
    "</openspec-state>",
  ].join("\n")
}

export default async ({ directory }) => {
  return {
    "experimental.chat.messages.transform": async (_input, output) => {
      try {
        if (!existsSync(join(directory, "openspec"))) return
        const messages = output?.messages
        if (!Array.isArray(messages)) return
        prependEphemeralText(messages, buildBreadcrumb(directory))
      } catch (error) {
        debugLog("openspec-state", "Error in messages.transform:", error.message)
      }
    },
  }
}
