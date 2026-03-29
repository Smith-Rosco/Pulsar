# Pulsar Documentation Center

**Last Updated**: 2026-03-09  
**Documentation Version**: v4.2.0  
**Architecture**: AI-first, task-oriented navigation

---

## 🎯 Quick Start (AI Agents)

**New to Pulsar?** Start here:
- **[AGENTS.md](../AGENTS.md)** - AI Agent operational guide (always-attached context)
- **[ARCHITECTURE.md](../ARCHITECTURE.md)** - System architecture overview
- **[PLUGIN_DEVELOPMENT.md](../PLUGIN_DEVELOPMENT.md)** - Plugin development guide

---

## 🗺️ Task Router (Where to Look)

### I need to...

| Task | Document |
|------|----------|
| **Build or run the application** | [Docs/ops/BUILD_AND_RUN.md](./ops/BUILD_AND_RUN.md) |
| **Understand the architecture** | [ARCHITECTURE.md](../ARCHITECTURE.md) |
| **Add or modify a plugin** | [PLUGIN_DEVELOPMENT.md](../PLUGIN_DEVELOPMENT.md), [architecture/PLUGIN_SYSTEM.md](./architecture/PLUGIN_SYSTEM.md) |
| **Generate VBA scripts (AI)** | [guides/VBARUNNER_AI_SCRIPTING.md](./guides/VBARUNNER_AI_SCRIPTING.md) |
| **Add a dialog** | [architecture/DIALOG_SYSTEM.md](./architecture/DIALOG_SYSTEM.md) |
| **Modify UI (XAML)** | [guides/UI_BEST_PRACTICES.md](./guides/UI_BEST_PRACTICES.md) |
| **Use reusable components** | [guides/COMPONENT_LIBRARY.md](./guides/COMPONENT_LIBRARY.md) |
| **Implement input injection** | [architecture/INPUT_INJECTION.md](./architecture/INPUT_INJECTION.md) |
| **Fix WPF theme issues** | [lessons/WPF_THEME_INJECTION_PITFALLS.md](./lessons/WPF_THEME_INJECTION_PITFALLS.md) |
| **Fix button styling issues** | [lessons/WPFUI_BUTTON_PRIMARY_BUG.md](./lessons/WPFUI_BUTTON_PRIMARY_BUG.md) |
| **Fix UserControl binding issues** | [lessons/WPF_USERCONTROL_BINDING_BREAKS.md](./lessons/WPF_USERCONTROL_BINDING_BREAKS.md) |
| **Fix ContextMenu styling** | [lessons/CONTEXTMENU_RESOURCE_INHERITANCE.md](./lessons/CONTEXTMENU_RESOURCE_INHERITANCE.md) |
| **Understand architectural decisions** | [decisions/](./decisions/) |
| **Follow documentation standards** | [CONTRIBUTING.md](./CONTRIBUTING.md) |

---

## 📚 Documentation Structure

### Core Documents (Root Level)
- **[AGENTS.md](../AGENTS.md)** - AI Agent operational guide (always-attached)
- **[ARCHITECTURE.md](../ARCHITECTURE.md)** - System architecture design (v4.0.0)
- **[PLUGIN_DEVELOPMENT.md](../PLUGIN_DEVELOPMENT.md)** - Plugin development guide

### Architecture (Docs/architecture/)
Stable conceptual truths about system design:
- **[PLUGIN_SYSTEM.md](./architecture/PLUGIN_SYSTEM.md)** - Plugin architecture, tiers, Circuit Breaker
- **[DIALOG_SYSTEM.md](./architecture/DIALOG_SYSTEM.md)** - Unified dialog architecture
- **[INPUT_INJECTION.md](./architecture/INPUT_INJECTION.md)** - Text injection hierarchy (UIA, Clipboard, SendInput)

### Guides (Docs/guides/)
How-to playbooks for common tasks:
- **[UI_BEST_PRACTICES.md](./guides/UI_BEST_PRACTICES.md)** - UI/UX design patterns
- **[CREATE_SLOT_DIALOG_GUIDELINES.md](./guides/CREATE_SLOT_DIALOG_GUIDELINES.md)** - Create Slot dialog structure and validation rules
- **[COMPONENT_LIBRARY.md](./guides/COMPONENT_LIBRARY.md)** - Reusable UI components (ExpandableCard, JellyOrb)
- **[VBARUNNER_AI_SCRIPTING.md](./guides/VBARUNNER_AI_SCRIPTING.md)** - AI guide for generating VBA scripts with Smart Directives

### Lessons (Docs/lessons/)
Pain archive - known pitfalls and correct patterns:
- **[WPF_THEME_INJECTION_PITFALLS.md](./lessons/WPF_THEME_INJECTION_PITFALLS.md)** - Theme injection timing issues
- **[WPF_RESOURCES_HYGIENE.md](./lessons/WPF_RESOURCES_HYGIENE.md)** - XAMLParseException prevention
- **[WPFUI_BUTTON_PRIMARY_BUG.md](./lessons/WPFUI_BUTTON_PRIMARY_BUG.md)** - Button Appearance="Primary" bug
- **[WPF_USERCONTROL_BINDING_BREAKS.md](./lessons/WPF_USERCONTROL_BINDING_BREAKS.md)** - UserControl DataContext binding
- **[CONTEXTMENU_RESOURCE_INHERITANCE.md](./lessons/CONTEXTMENU_RESOURCE_INHERITANCE.md)** - ContextMenu styling
- **[WPF_SCROLLVIEWER_VISIBILITY.md](./lessons/WPF_SCROLLVIEWER_VISIBILITY.md)** - Hidden scrollbars workaround

### Operations (Docs/ops/)
Commands and operational procedures:
- **[BUILD_AND_RUN.md](./ops/BUILD_AND_RUN.md)** - Build, run, and test commands

### Decisions (Docs/decisions/)
Architecture Decision Records (ADRs):
- **[001-plugin-metadata-system.md](./decisions/001-plugin-metadata-system.md)** - Plugin metadata design
- **[002-circuit-breaker-for-extension-plugins.md](./decisions/002-circuit-breaker-for-extension-plugins.md)** - Circuit breaker pattern

### Archive (Docs/archive/)
Historical documents (not current truth):
- **[2026-03/](./archive/2026-03/)** - Phase 2 completion reports, handovers
- **[2026-01/](./archive/2026-01/)** - PKI implementation details

---

## 🔍 Plugin Documentation

Documentation for built-in plugins:

- **[WinSwitcher](./Plugins/WinSwitcher.md)** - Window switching and application launching
- **[PkiPlugin](./Plugins/PkiPlugin.md)** - PKI credentials management (Core plugin)
- **[BasicCommand](./Plugins/BasicCommand.md)** - Basic command execution
- **[VbaRunner](./Plugins/VbaRunner.md)** - VBA script runner
  - **[VbaRunner Directives](./Plugins/VbaRunner_Directives.md)** - Directive reference
  - **[VbaRunner AI Scripting](./guides/VBARUNNER_AI_SCRIPTING.md)** - AI scripting guide
  - **[Smart Directives Implementation](./Plugins/VbaRunner_SmartDirectives_Implementation.md)** - Architecture details
- **[BookmarkletRunner](./Plugins/BookmarkletRunner.md)** - Bookmarklet runner
- **[SystemCommand](./Plugins/SystemCommand.md)** - System command execution

---

## 📖 Documentation Standards

### Document Types

| Type | Purpose | Location | Update Frequency |
|------|---------|----------|------------------|
| **Architecture** | System design and technical decisions | `Docs/architecture/` | When architecture changes |
| **Guides** | How-to playbooks | `Docs/guides/` | When APIs or patterns change |
| **Lessons** | Known pitfalls and correct patterns | `Docs/lessons/` | When new pitfalls discovered |
| **Operations** | Commands and procedures | `Docs/ops/` | When processes change |
| **Decisions** | ADRs (immutable) | `Docs/decisions/` | Never updated, only superseded |

### Document Lifecycle

1. **Draft** (`.draft.md` suffix) - Work in progress
2. **Review** ("Status: Under Review" header) - Awaiting approval
3. **Published** (no suffix, version number) - Active documentation
4. **Archived** (moved to `archive/YYYY-MM/`) - Historical reference
5. **Deprecated** ("⚠️ DEPRECATED" header) - Obsolete, to be removed after 6 months

### Naming Conventions

- **Core Documents**: `UPPERCASE_WITH_UNDERSCORES.md` (e.g., `ARCHITECTURE.md`)
- **Guides/Lessons/Ops**: `UPPERCASE_WITH_UNDERSCORES.md` (e.g., `UI_BEST_PRACTICES.md`)
- **Archives**: `YYYY-MM-DESCRIPTIVE_NAME.md` (e.g., `2026-03-REFACTORING_REPORT.md`)
- **ADRs**: `NNN-descriptive-title.md` (e.g., `001-plugin-metadata-system.md`)
- **Language**: English only (audience is AI agents)

---

## 🎨 AI-Optimized Writing Rules

Documentation is written for AI consumption:

- Use "If/Then" and "Rule/Reason/Example" blocks
- Put "Decision / Constraint" near the top; details later
- Use consistent keywords for grep/search:
  - `Symptom:`
  - `Root cause:`
  - `Correct pattern:`
  - `Incorrect pattern:`
  - `Do:` / `Don't:`
  - `Applies to:`
- Keep code samples minimal and canonical
- One file = one topic
- Prefer tables for comparisons

---

## 📞 Contributing to Documentation

When creating or updating documentation:

1. **Read existing docs** to avoid duplication
2. **Follow the template** for consistency (see [CONTRIBUTING.md](./CONTRIBUTING.md))
3. **Use clear, concise language** - prioritize actionable information
4. **Include code examples** where applicable
5. **Link related documents** for context
6. **Update this index** when adding new documents

---

## 🚨 Document Status Legend

- ✅ **Published** - Current, actively maintained
- 📦 **Archived** - Historical reference, no longer updated
- ⚠️ **Deprecated** - Obsolete, scheduled for removal
- 🚧 **Draft** - Work in progress

---

**Maintained by**: Pulsar Development Team  
**Questions?** Check [AGENTS.md](../AGENTS.md) for development guidelines

---

**Change History**:
- v4.2.0 (2026-03-09): Added VbaRunner Smart Directive System documentation
- v4.1.0 (2026-03-03): Refactored for AI-first architecture with task-oriented navigation
- v4.0.0 (2026-03-01): Initial documentation center
