# Pulsar Documentation Center

**Last Updated**: 2026-09-02
**Documentation Version**: v5.0.0
**Audience**: AI agents first, humans second

---

## Quick Start (AI Agents)

Always read these three first — they are the always-attached operational context:

- **[AGENTS.md](../AGENTS.md)** — AI Agent operational guide (invariants, pitfalls, task router)
- **[ARCHITECTURE.md](../ARCHITECTURE.md)** — system architecture overview
- **[PLUGIN_DEVELOPMENT.md](../PLUGIN_DEVELOPMENT.md)** — plugin development guide

Then follow the task router below.

---

## Directory Map

| Directory | What goes here | Count | Current truth? |
|---|---|---|---|
| **[architecture/](./architecture/)** | Stable conceptual truths about system design | 5 | Yes |
| **[guides/](./guides/)** | How-to playbooks for recurring tasks | 8 | Yes |
| **[lessons/](./lessons/)** | Reusable pitfalls: Symptom → Root cause → Correct pattern | 18 | Yes |
| **[decisions/](./decisions/)** | ADRs — immutable once accepted, only superseded | 10 | Yes |
| **[Plugins/](./Plugins/)** | Per-plugin user/developer docs | 8 | Yes |
| **[ops/](./ops/)** | Commands and operational procedures | 1 | Yes |
| **[proposals/](./proposals/)** | Design proposals and refactor plans not yet accepted | 2 | In review |
| **[roadmap/](./roadmap/)** | Roadmap and exploratory analysis | 2 | In review |
| **[agents/](./agents/)** | **Skill contracts — machine-consumed config, NOT reading material** | 2 | Config |
| **[archive/](./archive/)** | Historical snapshots, date-prefixed | 56 | **No** |

Two rules keep this structure honest:

1. **`lessons/` is for reusable pitfalls, not for reports.** A lesson answers "if you see symptom X, do Y". A document that narrates "on 2026-03-18 we found and fixed bug Z" belongs in `archive/` with a `YYYY-MM-DD-` prefix.
2. **A directory with one file is a smell.** Prefer adding to an existing category over creating a new one.

---

## Task Router

| I need to... | Document |
|---|---|
| Build or run the application | [ops/BUILD_AND_RUN.md](./ops/BUILD_AND_RUN.md) |
| Understand the architecture | [../ARCHITECTURE.md](../ARCHITECTURE.md) |
| Add or modify a plugin | [../PLUGIN_DEVELOPMENT.md](../PLUGIN_DEVELOPMENT.md), [architecture/PLUGIN_SYSTEM.md](./architecture/PLUGIN_SYSTEM.md) |
| Generate VBA scripts (AI) | [guides/VBARUNNER_AI_SCRIPTING.md](./guides/VBARUNNER_AI_SCRIPTING.md) |
| Add a dialog | [architecture/DIALOG_SYSTEM.md](./architecture/DIALOG_SYSTEM.md) — **specify DialogSizeConstraints AND register the DataTemplate** |
| Create or edit the slot dialog | [guides/CREATE_SLOT_DIALOG_GUIDELINES.md](./guides/CREATE_SLOT_DIALOG_GUIDELINES.md) |
| Modify UI (XAML) | [guides/UI_BEST_PRACTICES.md](./guides/UI_BEST_PRACTICES.md), [guides/COMPONENT_LIBRARY.md](./guides/COMPONENT_LIBRARY.md) |
| Implement input injection | [architecture/INPUT_INJECTION.md](./architecture/INPUT_INJECTION.md) |
| Back up / restore configuration | [guides/CONFIG_BACKUP_AND_RESTORE.md](./guides/CONFIG_BACKUP_AND_RESTORE.md) |
| Add logging | [guides/LOGGING_GUIDELINES.md](./guides/LOGGING_GUIDELINES.md) |
| Migrate a legacy plugin | [guides/PLUGIN_MIGRATION_GUIDE.md](./guides/PLUGIN_MIGRATION_GUIDE.md) |
| Understand window switching | [guides/WINDOW_SWITCHING_REFACTORING.md](./guides/WINDOW_SWITCHING_REFACTORING.md) |
| Fix a WPF issue | [lessons/](./lessons/) — start with the table in [AGENTS.md](../AGENTS.md) §3 |
| Propose or track a spec change | [../openspec/](../openspec/) |
| Find historical context | [archive/](./archive/) |

---

## Architecture (`architecture/`)

- **[PLUGIN_SYSTEM.md](./architecture/PLUGIN_SYSTEM.md)** — plugin architecture, tiers, Circuit Breaker
- **[DIALOG_SYSTEM.md](./architecture/DIALOG_SYSTEM.md)** — unified dialog architecture
- **[INPUT_INJECTION.md](./architecture/INPUT_INJECTION.md)** — text injection hierarchy (UIA → Clipboard → SendInput)
- **[PLUGIN_OPTIMIZATION_RECOMMENDATIONS.md](./architecture/PLUGIN_OPTIMIZATION_RECOMMENDATIONS.md)** — performance recommendations (standing advice, still open)
- **[PLUGIN_SYSTEM_REFACTORING_REPORT.md](./architecture/PLUGIN_SYSTEM_REFACTORING_REPORT.md)** — retrospective report on the plugin system refactor

---

## Guides (`guides/`)

- **[UI_BEST_PRACTICES.md](./guides/UI_BEST_PRACTICES.md)** — UI/UX design patterns
- **[CREATE_SLOT_DIALOG_GUIDELINES.md](./guides/CREATE_SLOT_DIALOG_GUIDELINES.md)** — slot dialog structure and validation rules
- **[COMPONENT_LIBRARY.md](./guides/COMPONENT_LIBRARY.md)** — reusable UI components (ExpandableCard, JellyOrb)
- **[VBARUNNER_AI_SCRIPTING.md](./guides/VBARUNNER_AI_SCRIPTING.md)** — AI guide for generating VBA with Smart Directives
- **[CONFIG_BACKUP_AND_RESTORE.md](./guides/CONFIG_BACKUP_AND_RESTORE.md)** — configuration backup and restore
- **[LOGGING_GUIDELINES.md](./guides/LOGGING_GUIDELINES.md)** — structured logging conventions
- **[PLUGIN_MIGRATION_GUIDE.md](./guides/PLUGIN_MIGRATION_GUIDE.md)** — migrating legacy plugins to the modern model
- **[WINDOW_SWITCHING_REFACTORING.md](./guides/WINDOW_SWITCHING_REFACTORING.md)** — window switching architecture and behavior

---

## Lessons (`lessons/`)

Reusable pitfalls. Each follows `Rule (TL;DR)` → `Symptom` → `Root cause` → `Correct / Incorrect pattern`.

**WPF**
- [WPF_THEME_INJECTION_PITFALLS.md](./lessons/WPF_THEME_INJECTION_PITFALLS.md) — theme injection timing; `ApplyTheme()` must run after `InitializeComponent()`
- [WPF_FLUENT_ACCENT_TOKENS_UNRESOLVED.md](./lessons/WPF_FLUENT_ACCENT_TOKENS_UNRESOLVED.md) — `Accent*` tokens not resolving; accent-on-accent text
- [WPFUI_BUTTON_PRIMARY_BUG.md](./lessons/WPFUI_BUTTON_PRIMARY_BUG.md) — never use `Appearance="Primary"`
- [WPF_BUTTON_TEMPLATE_FROZEN_FOREGROUND.md](./lessons/WPF_BUTTON_TEMPLATE_FROZEN_FOREGROUND.md) — button text frozen black on accent fill
- [WPF_RESOURCES_HYGIENE.md](./lessons/WPF_RESOURCES_HYGIENE.md) — XAMLParseException "Resources property can only be set once"
- [WPF_USERCONTROL_BINDING_BREAKS.md](./lessons/WPF_USERCONTROL_BINDING_BREAKS.md) — UserControl breaks `RelativeSource` bindings
- [WPF_SCROLLVIEWER_VISIBILITY.md](./lessons/WPF_SCROLLVIEWER_VISIBILITY.md) — hidden scrollbars workaround
- [CONTEXTMENU_RESOURCE_INHERITANCE.md](./lessons/CONTEXTMENU_RESOURCE_INHERITANCE.md) — ContextMenu renders in a separate visual tree
- [WPF_COMBOBOX_SELECTEDVALUE_ONEWAY_BLANK.md](./lessons/WPF_COMBOBOX_SELECTEDVALUE_ONEWAY_BLANK.md) — never `Mode=OneWay` on `SelectedValue` with a replaced collection
- [WPF_RADIOBUTTON_PROPERTYCHANGED_FEEDBACK_LOOP.md](./lessons/WPF_RADIOBUTTON_PROPERTYCHANGED_FEEDBACK_LOOP.md) — never two-way bind `IsChecked` while handling `Checked`

**Configuration**
- [CONFIG_EDIT_SESSION_STALE_REVISION.md](./lessons/CONFIG_EDIT_SESSION_STALE_REVISION.md) — settings save fails on consecutive saves
- [HOTKEY_SERVICE_STALE_CONFIG_OVERWRITE.md](./lessons/HOTKEY_SERVICE_STALE_CONFIG_OVERWRITE.md) — `Profiles.json` reverts after edits; use `RebuildCache()`

**Window management**
- [WINDOW_ELIGIBILITY_PHYSICAL_RULE.md](./lessons/WINDOW_ELIGIBILITY_PHYSICAL_RULE.md) — phantom windows; general physical-validity rule beats class-name patches
- [FOREGROUND_WINDOW_ACTIVATION_RELIABILITY.md](./lessons/FOREGROUND_WINDOW_ACTIVATION_RELIABILITY.md) — reliable foreground activation
- [SENDINPUT_FOREGROUND_ACTIVATION.md](./lessons/SENDINPUT_FOREGROUND_ACTIVATION.md) — SendInput-based activation caveats

**Lifecycle & tooling**
- [ASYNC_SHUTDOWN_DEADLOCK.md](./lessons/ASYNC_SHUTDOWN_DEADLOCK.md) — shutdown deadlocks from blocking async waits
- [POWERSHELL_5_1_COMPRESS_ARCHIVE_BROKEN.md](./lessons/POWERSHELL_5_1_COMPRESS_ARCHIVE_BROKEN.md) — `Compress-Archive` limitations on PS 5.1
- [GH_CLI_HASH_PATH_BUG.md](./lessons/GH_CLI_HASH_PATH_BUG.md) — `gh` CLI misinterprets paths containing `#`

---

## Decisions (`decisions/`)

ADRs are immutable once accepted; supersede rather than edit.

| ADR | Title |
|---|---|
| [001](./decisions/001-plugin-metadata-system.md) | Plugin metadata system |
| [002](./decisions/002-circuit-breaker-for-extension-plugins.md) | Circuit breaker for extension plugins |
| [003](./decisions/003-bookmarklet-nuglify-refactor.md) | Bookmarklet NUglify refactor |
| [004](./decisions/004-window-history-stack.md) | Window history stack |
| [005](./decisions/005-config-single-writer.md) | Config single writer |
| [006](./decisions/006-plugin-runtime-execution-hardening.md) | Plugin runtime execution hardening |
| [007](./decisions/007-external-plugin-permission-consent.md) | External plugin permission consent |
| [008](./decisions/008-menu-session-refactor.md) | Menu session refactor |
| [009](./decisions/009-config-snapshot-seam.md) | Config snapshot seam |
| [010](./decisions/010-window-service-deepening.md) | Window service deepening |

---

## Plugin Documentation (`Plugins/`)

- **[WinSwitcher](./Plugins/WinSwitcher.md)** — window switching and application launching
- **[PkiPlugin](./Plugins/PkiPlugin.md)** — PKI credential management (Core plugin)
- **[BasicCommand](./Plugins/BasicCommand.md)** — basic command execution
- **[SystemCommand](./Plugins/SystemCommand.md)** — system command execution
- **[BookmarkletRunner](./Plugins/BookmarkletRunner.md)** — bookmarklet runner
- **[VbaRunner](./Plugins/VbaRunner.md)** — VBA script runner
  - [VbaRunner_Directives](./Plugins/VbaRunner_Directives.md) — directive reference
  - [VbaRunner_SmartDirectives_Implementation](./Plugins/VbaRunner_SmartDirectives_Implementation.md) — architecture details
  - [VBARUNNER_AI_SCRIPTING](./guides/VBARUNNER_AI_SCRIPTING.md) — AI scripting guide

Manifest template: **[plugin.manifest.example.json](./plugin.manifest.example.json)**

---

## Proposals & Roadmap

Not accepted yet — treat as in-review, not as truth.

- **[proposals/UX_REFACTOR_PROPOSAL.md](./proposals/UX_REFACTOR_PROPOSAL.md)** — UX review and optimization proposal (Impeccable v2.0.0 framework, 2026-09-01)
- **[proposals/WINSWITCHER_REFACTOR.md](./proposals/WINSWITCHER_REFACTOR.md)** — WinSwitcher refactor plan
- **[roadmap/README.md](./roadmap/README.md)** — roadmap index
- **[roadmap/RIGHT_DRAG_GESTURE_ANALYSIS.md](./roadmap/RIGHT_DRAG_GESTURE_ANALYSIS.md)** — right-drag gesture analysis

---

## Agents (`agents/`)

> **These are machine-consumed skill contracts, not documentation.** They are written and read by the vendored skills under `.agents/skills/` — `setup-matt-pocock-skills` writes them, and the installed `domain-modeling` / `grill-with-docs` / `improve-codebase-architecture` read them. The paths are hardcoded in those skills, so **do not relocate or delete these files**; re-running `setup-matt-pocock-skills` will recreate them.

- **[domain.md](./agents/domain.md)** — which domain docs skills must read before exploring the codebase
- **[issue-tracker.md](./agents/issue-tracker.md)** — `gh` CLI conventions for skill-driven issue operations

---

## Specs (`../openspec/`)

Pulsar tracks behavioral specs with [OpenSpec](../openspec/):

```
openspec/
├── specs/              # merged truth — one directory per capability
├── changes/            # active work in progress
│   └── archive/        # completed changes (23)
└── config.yaml
```

Lifecycle: a change is proposed in `changes/<name>/` (`proposal.md` → `design.md` → `specs/` → `tasks.md`), and on completion its specs are merged into `specs/<capability>/spec.md`. **When behavior changes, update `openspec/specs/`, not just the code.**

---

## Archive (`archive/`)

Historical snapshots — **not current truth**. Useful for understanding why a decision was made; never cite as current behavior.

- Naming: `YYYY-MM-DD-DESCRIPTIVE_NAME.md`, flat directory
- Contains: phase completion reports, handovers, PKI implementation notes, TUTORIAL_SYSTEM design docs, and historical fix reports migrated from `lessons/` on 2026-09-02
- See [archive/README.md](./archive/README.md)

---

## Documentation Standards

### Lifecycle

1. **Draft** — `.draft.md` suffix
2. **Review** — `Status: Under Review` header
3. **Published** — no suffix, version number
4. **Archived** — moved to `archive/` with a date prefix
5. **Deprecated** — `⚠️ DEPRECATED` header, removed after 6 months

### Naming

- Core documents: `UPPERCASE_WITH_UNDERSCORES.md`
- Guides / lessons / ops: `UPPERCASE_WITH_UNDERSCORES.md`
- Archives: `YYYY-MM-DD-DESCRIPTIVE_NAME.md`
- ADRs: `NNN-descriptive-title.md`

### AI-Optimized Writing Rules

- One file = one topic
- Put the decision or constraint near the top; details later
- Use consistent grep-able keywords: `Symptom:` · `Root cause:` · `Correct pattern:` · `Incorrect pattern:` · `Applies to:` · `Rule (TL;DR):`
- Prefer tables for comparisons
- Keep code samples minimal and canonical
- Update this index whenever a document is added, moved, or deleted

---

**Change History**:
- v5.0.0 (2026-09-02): Full index resync after reorganization — 12 historical fix reports moved `lessons/` → `archive/`; `handoff/` folded into `archive/`; `design/` merged into `proposals/`; `Plugins/test_error_explanation.md` removed; added `openspec/`, `roadmap/`, `proposals/`, `agents/` sections; documented the lessons-vs-archive criterion
- v4.2.0 (2026-03-09): Added VbaRunner Smart Directive System documentation
- v4.1.0 (2026-03-03): Refactored for AI-first architecture with task-oriented navigation
- v4.0.0 (2026-03-01): Initial documentation center
