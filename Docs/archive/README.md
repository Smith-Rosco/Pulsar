# Archive Index

**Archive Period**: 2026-03-01 to 2026-08-29
**Status**: Historical reference only, no longer maintained

---

## Overview

Historical snapshots of Pulsar development, covering:
- Plugin System Modernization (Phase 1 & Phase 2)
- Bookmarklet Runner v2.0.0 Development
- Project Handover Documents
- Historical bug-fix reports (migrated from `lessons/` on 2026-09-02)
- UX refactoring records

Files are named `YYYY-MM-DD-DESCRIPTIVE_NAME.md`. Nothing here is current truth — see [Docs/README.md](../../README.md) for living documentation.

---

## Document Categories

### 1. Project Handover Documents

| Document | Date | Description |
|----------|------|-------------|
| [2026-03-02-HANDOVER_V4.1.0.md](./2026-03-02-HANDOVER_V4.1.0.md) | 2026-03-02 | Final handover document (v4.1.0) - Superseded by [AGENTS.md](../../AGENTS.md) |
| [2026-03-02-HANDOVER.md](./2026-03-02-HANDOVER.md) | 2026-03-02 | Intermediate handover (Phase 2 Task 2) |
| [2026-03-02-HANDOVER_PHASE1.md](./2026-03-02-HANDOVER_PHASE1.md) | 2026-03-02 | Phase 1 completion handover |
| [2026-03-02-HANDOVER_PHASE2.md](./2026-03-02-HANDOVER_PHASE2.md) | 2026-03-02 | Phase 2 planning handover |

### 2. Bookmarklet Runner v2.0.0 (2026-03-04)

| Document | Purpose |
|----------|---------|
| [2026-03-04-BOOKMARKLET_V2_IMPLEMENTATION_SUMMARY.md](./2026-03-04-BOOKMARKLET_V2_IMPLEMENTATION_SUMMARY.md) | Complete implementation summary |
| [2026-03-04-BOOKMARKLET_V2_GIT_COMMIT_SUMMARY.md](./2026-03-04-BOOKMARKLET_V2_GIT_COMMIT_SUMMARY.md) | Git commit report (commits: 62ecf39, 097ec30) |
| [2026-03-04-BOOKMARKLET_V2_QUICK_REFERENCE.md](./2026-03-04-BOOKMARKLET_V2_QUICK_REFERENCE.md) | Quick reference guide |
| [2026-03-04-BOOKMARKLET_V2_TESTING_GUIDE.md](./2026-03-04-BOOKMARKLET_V2_TESTING_GUIDE.md) | Testing procedures |
| [2026-03-04-BOOKMARKLET_V2_TEST_ERROR_CHECKLIST.md](./2026-03-04-BOOKMARKLET_V2_TEST_ERROR_CHECKLIST.md) | Error testing checklist |
| [2026-03-04-BOOKMARKLET_V2_FINAL_DIAGNOSIS.md](./2026-03-04-BOOKMARKLET_V2_FINAL_DIAGNOSIS.md) | Issue diagnosis report |

**Current Documentation**: See [Docs/Plugins/BookmarkletRunner.md](../Plugins/BookmarkletRunner.md)

### 3. Plugin System Modernization

| Document | Date | Description |
|----------|------|-------------|
| [2026-03-02-PLUGIN_SYSTEM_MODERNIZATION_PHASE1.md](./2026-03-02-PLUGIN_SYSTEM_MODERNIZATION_PHASE1.md) | 2026-03-02 | Phase 1 completion report |
| [2026-03-01-PLUGIN_SETTINGS_MIGRATION.md](./2026-03-01-PLUGIN_SETTINGS_MIGRATION.md) | 2026-03-01 | Settings migration guide |
| [2026-03-01-REFACTORING_REPORT.md](./2026-03-01-REFACTORING_REPORT.md) | 2026-03-01 | Refactoring summary |

### 4. Phase 2 Task Reports

| Document | Task | Description |
|----------|------|-------------|
| [2026-03-02-PHASE2_TASKS.md](./2026-03-02-PHASE2_TASKS.md) | Overview | Phase 2 task planning |
| [2026-03-02-HANDOVER_PHASE2_TASK1.md](./2026-03-02-HANDOVER_PHASE2_TASK1.md) | Task 1 | Task 1 handover |
| [2026-03-02-PHASE2_TASK1_COMPLETION_REPORT.md](./2026-03-02-PHASE2_TASK1_COMPLETION_REPORT.md) | Task 1 | Task 1 completion |
| [2026-03-02-PHASE2_TASK2_COMPLETION_REPORT.md](./2026-03-02-PHASE2_TASK2_COMPLETION_REPORT.md) | Task 2 | Permission system completion |
| [2026-03-02-PHASE2_TASK3_SUMMARY.md](./2026-03-02-PHASE2_TASK3_SUMMARY.md) | Task 3 | Task 3 summary |
| [2026-03-02-PHASE2_TASK4_COMPLETION_REPORT.md](./2026-03-02-PHASE2_TASK4_COMPLETION_REPORT.md) | Task 4 | Task 4 completion |
| [2026-03-02-PHASE2_TASK5_COMPLETION_SUMMARY.md](./2026-03-02-PHASE2_TASK5_COMPLETION_SUMMARY.md) | Task 5 | Task 5 summary |
| [2026-03-02-PHASE2_TASK5_TEST_REPORT.md](./2026-03-02-PHASE2_TASK5_TEST_REPORT.md) | Task 5 | Task 5 test report |

### 5. Historical Bug-Fix Reports

Migrated from `Docs/lessons/` on 2026-09-02. These narrate a specific fix on a specific date and are kept for historical context — the reusable rules extracted from them live in [../lessons/](../lessons/).

| Document | Date | Area |
|----------|------|------|
| [2026-03-08-QUICK_SWITCH_FIX_SUMMARY.md](./2026-03-08-QUICK_SWITCH_FIX_SUMMARY.md) | 2026-03-08 | Quick Switch extraction into `QuickSwitchEngine` |
| [2026-03-08-QUICK_SWITCH_REMOTE_DESKTOP_FIX.md](./2026-03-08-QUICK_SWITCH_REMOTE_DESKTOP_FIX.md) | 2026-03-08 | Quick Switch failure over fullscreen RDP |
| [2026-03-08-REMOTE_DESKTOP_FOCUS_FIX_REPORT.md](./2026-03-08-REMOTE_DESKTOP_FOCUS_FIX_REPORT.md) | 2026-03-08 | Focus handling over remote desktop |
| [2026-03-09-ARCHITECTURE_FIX_SUMMARY.md](./2026-03-09-ARCHITECTURE_FIX_SUMMARY.md) | 2026-03-09 | Slots-per-page architecture fix summary |
| [2026-03-09-DYNAMIC_ADAPTIVE_LAYOUT.md](./2026-03-09-DYNAMIC_ADAPTIVE_LAYOUT.md) | 2026-03-09 | Adaptive layout / visual density |
| [2026-03-09-DYNAMIC_SLOTS_ARCHITECTURE_FIX.md](./2026-03-09-DYNAMIC_SLOTS_ARCHITECTURE_FIX.md) | 2026-03-09 | Slot overlap beyond 8 slots per page |
| [2026-03-09-DYNAMIC_SLOTS_PER_PAGE_IMPLEMENTATION.md](./2026-03-09-DYNAMIC_SLOTS_PER_PAGE_IMPLEMENTATION.md) | 2026-03-09 | Configurable slots-per-page implementation |
| [2026-03-09-DYNAMIC_LAYOUT_TEST_GUIDE.md](./2026-03-09-DYNAMIC_LAYOUT_TEST_GUIDE.md) | 2026-03-09 | Layout testing procedures |
| [2026-03-09-TODO_SLOTS_PER_PAGE.md](./2026-03-09-TODO_SLOTS_PER_PAGE.md) | 2026-03-09 | Outstanding work for slots per page |
| [2026-03-09-RDP_MODIFIER_KEY_STUCK.md](./2026-03-09-RDP_MODIFIER_KEY_STUCK.md) | 2026-03-09 | Stuck modifier keys over RDP |
| [2026-03-15-TUTORIAL_ARCHITECTURE_FIX.md](./2026-03-15-TUTORIAL_ARCHITECTURE_FIX.md) | 2026-03-15 | Tutorial vs. actual architecture mismatch |
| [2026-03-15-TUTORIAL_FIX_FINAL.md](./2026-03-15-TUTORIAL_FIX_FINAL.md) | 2026-03-15 | Final tutorial architecture fix |
| [2026-03-15-TUTORIAL_REFACTORING.md](./2026-03-15-TUTORIAL_REFACTORING.md) | 2026-03-15 | Tutorial system refactoring |
| [2026-03-18-MULTI_WINDOW_SWITCHING_LOGIC.md](./2026-03-18-MULTI_WINDOW_SWITCHING_LOGIC.md) | 2026-03-18 | Multi-window process switching |
| [2026-03-18-SUBMENU_SLOT_OPACITY_BUG.md](./2026-03-18-SUBMENU_SLOT_OPACITY_BUG.md) | 2026-03-18 | Sub-radial slots greyed out |
| [2026-03-18-WINEVENT_HOOK_THREAD_CONTEXT.md](./2026-03-18-WINEVENT_HOOK_THREAD_CONTEXT.md) | 2026-03-18 | WinEvent hook never firing |
| [2026-03-23-SLOT_DIALOG_REFACTOR_HANDOFF.md](./2026-03-23-SLOT_DIALOG_REFACTOR_HANDOFF.md) | 2026-03-23 | Slot dialog refactor handover |
| [2026-08-29-PULSAR_HANDOFF.md](./2026-08-29-PULSAR_HANDOFF.md) | 2026-08-29 | Project handover |

---

## Key Milestones

### Phase 1 (Completed 2026-03-02)
- ✅ Plugin metadata system
- ✅ Circuit breaker pattern
- ✅ PulsarContext lazy loading
- ✅ Dialog system unification

### Phase 2 (Completed 2026-03-02)
- ✅ Permission system
- ✅ Settings page refactoring
- ✅ External plugins management
- ✅ Plugin package manager simplification

### Bookmarklet v2.0.0 (Completed 2026-03-04)
- ✅ NUglify engine integration
- ✅ Syntax validation
- ✅ ES6+ support
- ✅ Dual fallback mechanism

---

## Current Documentation

For up-to-date documentation, refer to:

- **[AGENTS.md](../../AGENTS.md)** - AI agent operational guide
- **[ARCHITECTURE.md](../../ARCHITECTURE.md)** - System architecture
- **[PLUGIN_DEVELOPMENT.md](../../PLUGIN_DEVELOPMENT.md)** - Plugin development guide
- **[Docs/README.md](../../README.md)** - Documentation index

---

## Archive Policy

According to [Docs/CONTRIBUTING.md](../CONTRIBUTING.md):

- **Status**: Archived
- **Retention**: Indefinite (historical reference)
- **Updates**: No longer maintained
- **Purpose**: Historical record of development process

---

**Last Updated**: 2026-09-02
**Archived By**: Documentation maintenance process
