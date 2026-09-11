# Pulsar Documentation Center

**Last Updated**: 2026-09-08
**Documentation Version**: v6.0.0
**Audience**: AI agents first, humans second
**Structure decision**: [ADR-027](./decisions/027-documentation-structure-v6-single-authoritative-sources.md) — one fact, one home; this index is the contract.

---

## Quick Start (AI Agents)

Always read these three first — they are the always-attached operational context:

- **[AGENTS.md](../AGENTS.md)** — AI Agent operational guide (invariants, pitfalls, task router)
- **[ARCHITECTURE.md](../ARCHITECTURE.md)** — system architecture overview (summaries; detail lives in `architecture/`)
- **[PLUGIN_DEVELOPMENT.md](../PLUGIN_DEVELOPMENT.md)** — plugin development guide (interface reference — authoritative)

Then follow the task router below.

---

## Directory Map (complete — every first-level entry is registered here)

| Entry | What goes here | Current truth? |
|---|---|---|
| **README.md** | This index — the only routing entry point | Yes |
| **[CONTRIBUTING.md](./CONTRIBUTING.md)** | Documentation rules: lifecycle, language policy, routing table, size/authority discipline | Yes |
| **[PLUGIN_QUICKSTART.md](./PLUGIN_QUICKSTART.md)** | Fast-path funnel into `../PLUGIN_DEVELOPMENT.md` (quick read → deep read) | Yes |
| **[plugin.manifest.example.json](./plugin.manifest.example.json)** | External plugin manifest template | Yes |
| **[architecture/](./architecture/)** | Stable conceptual truths + architecture diagrams (`*.architecture.html/json`) | Yes |
| **[guides/](./guides/)** | How-to playbooks for recurring tasks | Yes |
| **[lessons/](./lessons/)** | Reusable pitfalls: Symptom → Root cause → Correct pattern | Yes |
| **[decisions/](./decisions/)** | ADRs — immutable once accepted, only superseded | Yes |
| **[plugins/](./plugins/)** | Per-plugin user/developer docs | Yes |
| **[manual/](./manual/README.md)** | Task-oriented bilingual user manual (en/zh; release-gate link-checked) | Yes |
| **[ops/](./ops/)** | Commands, operational procedures, release checklist & notes template | Yes |
| **[planning/](./planning/)** | Roadmap entries, refactor proposals, exploratory analysis (not yet accepted) | In review |
| **[reports/](./reports/)** | Point-in-time research: market eval, competitive comparison (validity-dated; archive when stale) | Dated snapshots |
| **[agents/](./agents/)** | **Skill contracts — machine-consumed config, NOT reading material** (see warning below) | Config |
| **[journal/](./journal/)** | Cross-session working memory, one file per day (ADR-019 single source) | Yes |
| **[media/](./media/release/README.md)** | Tracked release/promotional media (`release/`); local-only captures stay gitignored | Yes |
| **[archive/](./archive/README.md)** | Historical snapshots, date-prefixed, bucketed by month | **No** |

Rules that keep this structure honest:

1. **One fact, one home.** Documents link to authoritative sources; they never
   restate them. The current authority map lives in
   [CONTRIBUTING.md](./CONTRIBUTING.md).
2. **A directory with one file is a smell.** Prefer adding to an existing
   category over creating a new one.
3. **Index sync is mandatory.** Any doc add/move/delete updates this file in
   the same change.

---

## Task Router

| I need to... | Document |
|---|---|
| Build or run the application | [ops/BUILD_AND_RUN.md](./ops/BUILD_AND_RUN.md) |
| Understand the architecture | [../ARCHITECTURE.md](../ARCHITECTURE.md) — then [architecture/PLUGIN_SYSTEM.md](./architecture/PLUGIN_SYSTEM.md) for plugin detail |
| Add or modify a plugin | [../PLUGIN_DEVELOPMENT.md](../PLUGIN_DEVELOPMENT.md), [architecture/PLUGIN_SYSTEM.md](./architecture/PLUGIN_SYSTEM.md); quickstart: [PLUGIN_QUICKSTART.md](./PLUGIN_QUICKSTART.md) |
| Understand plugin concepts (tiers, breaker, lifecycle) | [architecture/PLUGIN_SYSTEM.md](./architecture/PLUGIN_SYSTEM.md) — **authoritative** |
| Generate VBA scripts (AI) | [guides/VBARUNNER_AI_SCRIPTING.md](./guides/VBARUNNER_AI_SCRIPTING.md), [guides/AI_PROMPT_VBA_RUNNER.md](./guides/AI_PROMPT_VBA_RUNNER.md) |
| Script legacy web pages (AI) | [guides/AI_PROMPT_BOOKMARKLET.md](./guides/AI_PROMPT_BOOKMARKLET.md) |
| Find plugin test/demo scripts | READMEs in `Pulsar/Pulsar/Plugins/Extensions/*/TestScripts\|DemoScripts/` and `Pulsar/Samples/` |
| Add a dialog | [architecture/DIALOG_SYSTEM.md](./architecture/DIALOG_SYSTEM.md) — **specify DialogSizeConstraints AND register the DataTemplate** |
| Create or edit the slot dialog | [guides/CREATE_SLOT_DIALOG_GUIDELINES.md](./guides/CREATE_SLOT_DIALOG_GUIDELINES.md) |
| Modify UI (XAML) | [guides/UI_BEST_PRACTICES.md](./guides/UI_BEST_PRACTICES.md), [guides/COMPONENT_LIBRARY.md](./guides/COMPONENT_LIBRARY.md) |
| Implement input injection | [architecture/INPUT_INJECTION.md](./architecture/INPUT_INJECTION.md) |
| Back up / restore configuration | [guides/CONFIG_BACKUP_AND_RESTORE.md](./guides/CONFIG_BACKUP_AND_RESTORE.md) |
| Add logging | [guides/LOGGING_GUIDELINES.md](./guides/LOGGING_GUIDELINES.md) |
| Migrate a legacy plugin | [guides/PLUGIN_MIGRATION_GUIDE.md](./guides/PLUGIN_MIGRATION_GUIDE.md) |
| Understand window switching | [decisions/010-window-service-deepening.md](./decisions/010-window-service-deepening.md) |
| Understand product positioning & market | [reports/](./reports/) — dated snapshots (2026-09) |
| Check roadmap / proposals status | [planning/](./planning/) |
| Fix a WPF issue | [lessons/](./lessons/) — start with the table in [AGENTS.md](../AGENTS.md) §3 |
| Propose or track a spec change | [../openspec/](../openspec/) |
| Find historical context | [archive/README.md](./archive/README.md) |
| View the plugin-system architecture diagram | [architecture/pulsar-plugin-system.architecture.html](./architecture/pulsar-plugin-system.architecture.html) |

---

## Architecture (`architecture/`)

- **[PLUGIN_SYSTEM.md](./architecture/PLUGIN_SYSTEM.md)** — **authoritative** plugin concepts: tiers, Circuit Breaker (incl. observation seam), runtime kernel & three seams, lifecycle, `PulsarContext`
- **[DIALOG_SYSTEM.md](./architecture/DIALOG_SYSTEM.md)** — unified dialog architecture
- **[INPUT_INJECTION.md](./architecture/INPUT_INJECTION.md)** — text injection hierarchy (UIA → Clipboard → SendInput)
- **[pulsar-plugin-system.architecture.html](./architecture/pulsar-plugin-system.architecture.html)** (+ `.json` spec) — generated architecture diagram

---

## Guides (`guides/`)

- **[UI_BEST_PRACTICES.md](./guides/UI_BEST_PRACTICES.md)** — UI/UX design patterns
- **[CREATE_SLOT_DIALOG_GUIDELINES.md](./guides/CREATE_SLOT_DIALOG_GUIDELINES.md)** — slot dialog structure and validation rules
- **[COMPONENT_LIBRARY.md](./guides/COMPONENT_LIBRARY.md)** — reusable UI components (ExpandableCard, JellyOrb)
- **[VBARUNNER_AI_SCRIPTING.md](./guides/VBARUNNER_AI_SCRIPTING.md)** — AI guide for generating VBA with Smart Directives
- **[AI_PROMPT_VBA_RUNNER.md](./guides/AI_PROMPT_VBA_RUNNER.md)** — copy-paste AI prompt for `.bas` scripts (README-linked)
- **[AI_PROMPT_BOOKMARKLET.md](./guides/AI_PROMPT_BOOKMARKLET.md)** — copy-paste AI prompt for legacy-web bookmarklet scripts (README-linked)
- **[CONFIG_BACKUP_AND_RESTORE.md](./guides/CONFIG_BACKUP_AND_RESTORE.md)** — configuration backup and restore
- **[LOGGING_GUIDELINES.md](./guides/LOGGING_GUIDELINES.md)** — structured logging conventions
- **[PLUGIN_MIGRATION_GUIDE.md](./guides/PLUGIN_MIGRATION_GUIDE.md)** — migrating legacy plugins to the modern model

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
- [WPF_COMBOBOX_UIELEMENT_SELECTION_CENTERED.md](./lessons/WPF_COMBOBOX_UIELEMENT_SELECTION_CENTERED.md) — ComboBox selection visual alignment
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
- [GITIGNORE_DEBUG_DIR_SILENT_EXCLUDE.md](./lessons/GITIGNORE_DEBUG_DIR_SILENT_EXCLUDE.md) — debug dir silently excluded by gitignore patterns
- [XUNIT_APPLICATION_CURRENT_DEADLOCK.md](./lessons/XUNIT_APPLICATION_CURRENT_DEADLOCK.md) — xUnit deadlock from touching `Application.Current` on a non-UI thread

**E2E testing**
- [E2E_SCREENSHOT_RUNID_SUFFIX.md](./lessons/E2E_SCREENSHOT_RUNID_SUFFIX.md) — screenshot run-id suffix handling
- [E2E_SETTINGS_WINDOW_ASSERTION_ANCHOR.md](./lessons/E2E_SETTINGS_WINDOW_ASSERTION_ANCHOR.md) — stable assertion anchors in settings E2E
- [E2E_STATS_FIXTURE_CAMELCASE_ARRAY.md](./lessons/E2E_STATS_FIXTURE_CAMELCASE_ARRAY.md) — stats fixture camelCase array contract

**Plugin runtime**
- [PLUGIN_RUNTIME_INSTALL_UNINSTALL_PITFALLS.md](./lessons/PLUGIN_RUNTIME_INSTALL_UNINSTALL_PITFALLS.md) — runtime install/uninstall: activation, ALC unload, GC-driven teardown
- [PLUGIN_LOCALIZATION_SILENT_ENGLISH_FALLBACK.md](./lessons/PLUGIN_LOCALIZATION_SILENT_ENGLISH_FALLBACK.md) — silent English fallback when a plugin localization key is missing

**Script authoring & demo assets**
- [VBA_INJECT_ATTRIBUTE_LINE_BREAKS_COMPILE.md](./lessons/VBA_INJECT_ATTRIBUTE_LINE_BREAKS_COMPILE.md) — `.bas` with `Attribute VB_Name` header: procedure registers but full compile fails (0x800A03EC); bisect-based debugging method
- [PKI_FILL_SENDKEYS_FOCUS_CONTRACT.md](./lessons/PKI_FILL_SENDKEYS_FOCUS_CONTRACT.md) — `pki/fill` is keyboard injection: target pages must honour the autofocus/form-submit contract
- [DEMO_SCRIPT_AUTHORING_PITFALLS.md](./lessons/DEMO_SCRIPT_AUTHORING_PITFALLS.md) — bookmarklet pacing via `setTimeout`, CSV+UTF-8 BOM downloads, mojibake, DOM contracts, literal slot args

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
| [011](./decisions/011-cascade-submenu-layout-and-paging.md) | Cascade submenu Ring/Fan layout and paging semantics |
| [012](./decisions/012-plugin-runtime-three-seams.md) | Plugin runtime three seams (`IPluginRegistry` / `IPluginExecutor` / `IPluginRuntimeOps`) |
| [013](./decisions/013-circuit-breaker-observation-seam.md) | Circuit breaker observation seam |
| [014](./decisions/014-manifest-file-resolution-single-source.md) | Manifest file resolution single source |
| [015](./decisions/015-card-capabilities-in-metadata.md) | Card capabilities in metadata |
| [016](./decisions/016-state-store-pure-reads.md) | State store pure reads |
| [017](./decisions/017-app-startup-coordinator-hybrid-injection.md) | App startup coordinator hybrid injection |
| [018](./decisions/018-first-launch-decision-uses-onboarding-state-service.md) | First-launch decision uses onboarding state service |
| [019](./decisions/019-cross-harness-working-memory-single-source.md) | Cross-harness working memory single source |
| [020](./decisions/020-right-drag-gesture-orchestration-in-menu-session.md) | Right-drag gesture orchestration in MenuSession |
| [021](./decisions/021-journal-rotation-and-parallel-worktree-discipline.md) | Journal rotation and parallel worktree discipline |
| [022](./decisions/022-agents-md-always-on-kernel-and-conditional-loading.md) | AGENTS.md always-on kernel and conditional loading |
| [023](./decisions/023-page-provider-factory-seam.md) | Page provider factory seam |
| [024](./decisions/024-cascade-submenu-geometry-and-viewport.md) | Cascade submenu geometry and viewport |
| [025](./decisions/025-in-app-auto-update-three-tier-check.md) | In-app auto-update three-tier check |
| [026](./decisions/026-installer-inno-setup-no-trim.md) | Installer (Inno Setup) — no IL trimming |
| [027](./decisions/027-documentation-structure-v6-single-authoritative-sources.md) | Documentation structure v6 — single authoritative sources, full index registry |
| [028](./decisions/028-window-history-single-authority.md) | Window history single authority — MenuPrevious + MRU stack in one module |
| [029](./decisions/029-settings-transient-pages.md) | Settings transient pages — per-type singleton, clean-leave auto-recycle |
| [030](./decisions/030-wheel-radius-single-formula.md) | Wheel radius single formula — `CalculateOptimalLayout` derives radius from `CalculateOptimalSlotSize(N)` |
| [031](./decisions/031-secret-fill-rename-plugin-id-frozen.md) | PKI module renamed to Secret Fill — symbols only, plugin id `com.pulsar.pki` frozen |
| [032](./decisions/032-secret-fill-plugin-id-migration.md) | Secret Fill plugin id unfrozen — renamed to `com.pulsar.secretfill` with load-time config + usage-stats migration |
| [033](./decisions/033-dialog-catalog-single-registration-surface.md) | Dialog catalog — one registration surface owns each dialog's title key, size preset, buttons and theme |

---

## Plugin Documentation (`plugins/`)

- **[WinSwitcher](./plugins/WinSwitcher.md)** — window switching and application launching
- **[SecretFillPlugin](./plugins/SecretFillPlugin.md)** — Secret Fill credential management (Core plugin)
- **[BasicCommand](./plugins/BasicCommand.md)** — basic command execution
- **[SystemCommand](./plugins/SystemCommand.md)** — system command execution
- **[BookmarkletRunner](./plugins/BookmarkletRunner.md)** — bookmarklet runner
- **[VbaRunner](./plugins/VbaRunner.md)** — VBA script runner
  - [VbaRunner_Directives](./plugins/VbaRunner_Directives.md) — directive reference
  - [VbaRunner_SmartDirectives_Implementation](./plugins/VbaRunner_SmartDirectives_Implementation.md) — architecture details

Manifest template: **[plugin.manifest.example.json](./plugin.manifest.example.json)**
In-repo plugin script assets: see the `README.md` inside each `TestScripts/` / `DemoScripts/` under `Pulsar/Pulsar/Plugins/Extensions/*/` and `Pulsar/Samples/`.

---

## Planning (`planning/`)

Not accepted yet — treat as in-review, not as truth.

- **[ROADMAP_INDEX.md](./planning/ROADMAP_INDEX.md)** — roadmap index
- **[RIGHT_DRAG_GESTURE_ANALYSIS.md](./planning/RIGHT_DRAG_GESTURE_ANALYSIS.md)** — right-drag gesture analysis
- **[IMPLEMENTATION_VERIFICATION.md](./planning/IMPLEMENTATION_VERIFICATION.md)** — roadmap 条目 × openspec 变更 × 代码实证的逐条对照
- **[UX_REFACTOR_PROPOSAL.md](./planning/UX_REFACTOR_PROPOSAL.md)** — UX review and optimization proposal (Impeccable v2.0.0 framework, 2026-09-01)
- **[WINSWITCHER_REFACTOR.md](./planning/WINSWITCHER_REFACTOR.md)** — WinSwitcher refactor plan
- **[ANALYTICS_P4_EXPLORATION.md](./planning/ANALYTICS_P4_EXPLORATION.md)** — analytics phase-4 exploration

---

## Agents (`agents/`)

> **These are machine-consumed skill contracts, not documentation.** They are written and read by the vendored skills under `.agents/skills/` — `setup-matt-pocock-skills` writes them, and the installed `domain-modeling` / `grill-with-docs` / `improve-codebase-architecture` read them. The paths are hardcoded in those skills, so **do not relocate or delete these files**; re-running `setup-matt-pocock-skills` will recreate them.

- **[domain.md](./agents/domain.md)** — which domain docs skills must read before exploring the codebase
- **[harness-matrix.md](./agents/harness-matrix.md)** — AI harness capability/coverage matrix
- **[issue-tracker.md](./agents/issue-tracker.md)** — `gh` CLI conventions for skill-driven issue operations

---

## Specs (`../openspec/`)

Pulsar tracks behavioral specs with [OpenSpec](../openspec/):

```
openspec/
├── specs/              # merged truth — one directory per capability
├── changes/            # active work in progress
│   └── archive/        # completed changes (38)
└── config.yaml
```

Lifecycle: a change is proposed in `changes/<name>/` (`proposal.md` → `design.md` → `specs/` → `tasks.md`), and on completion its specs are merged into `specs/<capability>/spec.md`. **When behavior changes, update `openspec/specs/`, not just the code.**

Drive OpenSpec from opencode via slash commands (`/opsx-propose`, `/opsx-apply`, `/opsx-archive`, …) — see `.opencode/commands/`. The `.opencode/plugin/openspec-workflow-state.js` plugin auto-injects the active change + phase into every model turn.

---

## Working Memory (`journal/`) — ADR-019

`Docs/journal/` is the **single canonical cross-harness working-memory store**:
one `YYYY-MM-DD.md` per day, plus `NEXT.md` for pending items and an `archive/`
for rotated days (ADR-021). AI harnesses with local memory files must keep at
most a one-line pointer — never copy journal content.

---

## Documentation Standards

See **[CONTRIBUTING.md](./CONTRIBUTING.md)** — lifecycle, language policy,
naming, routing table (specs vs ADR vs lessons vs journal vs report), and the
size/authority discipline.

---

**Change History**:
- v6.0.0 (2026-09-08): Structure overhaul per ADR-027 and the 2026-09-07 audit ([report](./reports/2026-09-07-DOC_STRUCTURE_AUDIT.html)) — every first-level entry registered; `roadmap/`+`proposals/` → `planning/`; `diagrams/` → `architecture/`; `Plugins/` → `plugins/`; `archive/` month-bucketed; orphan `screenshots/` deleted; retired `demo/`; ADR table extended to 027; plugin script directories (in source tree) indexed
- v5.2.0 (2026-09-04): Index resync — ADR table extended to 019, lessons to 21, reports to 6, openspec changes/archive to 38; archived 3 historical docs to `archive/`
- v5.1.0 (2026-09-02): Added repository health review snapshot to `archive/` (2026-09-02-REPO_HEALTH_REVIEW.md)
- v5.0.0 (2026-09-02): Full index resync after reorganization — 12 historical fix reports moved `lessons/` → `archive/`; `handoff/` folded into `archive/`; `design/` merged into `proposals/`
- v4.2.0 (2026-03-09): Added VbaRunner Smart Directive System documentation
- v4.1.0 (2026-03-03): Refactored for AI-first architecture with task-oriented navigation
- v4.0.0 (2026-03-01): Initial documentation center
