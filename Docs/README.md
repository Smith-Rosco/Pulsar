# Pulsar Documentation Center

**Last Updated**: 2026-03-02  
**Documentation Version**: v4.1.0  
**Current Phase**: Phase 2 - Plugin System Modernization

---

## 🚀 Quick Start

**New to Pulsar?** Start here:
- **[HANDOVER.md](HANDOVER.md)** - Project handover document with current status and quick start guide
- **[AGENTS.md](../AGENTS.md)** - AI Agent operational guide and coding conventions
- **[PLUGIN_QUICKSTART.md](PLUGIN_QUICKSTART.md)** - Plugin development quick start

---

## 📚 Core Documentation

Essential documents for understanding and working with Pulsar:

- **[HANDOVER.md](HANDOVER.md)** - Current project status and handover guide ✨ NEW
- **[ARCHITECTURE.md](../ARCHITECTURE.md)** - System architecture design (v4.0.0)
- **[PLUGIN_DEVELOPMENT.md](../PLUGIN_DEVELOPMENT.md)** - Plugin development guide
- **[AGENTS.md](../AGENTS.md)** - AI Agent operational guide and coding conventions

---

## 🎯 Phase 2: Plugin System Modernization

Current development phase focused on enterprise-grade plugin infrastructure:

- **[PHASE2_TASKS.md](PHASE2_TASKS.md)** - Task list and progress tracking
- **[PHASE2_TASK2_COMPLETION_REPORT.md](PHASE2_TASK2_COMPLETION_REPORT.md)** - Permission system completion report ✅

**Progress**: 2/6 tasks completed (33%)

| Task | Status | Completion Date |
|------|--------|----------------|
| 1. Hot Reload Manager | ✅ Completed | 2026-03-02 |
| 2. Permission System | ✅ Completed | 2026-03-02 |
| 3. Dependency Isolation | 🔴 Pending | - |
| 4. Plugin Package Manager | 🔴 Pending | - |
| 5. Unit Tests | 🟡 In Progress | - |
| 6. Plugin Market UI | 🔴 Pending | - |

---

## 🛠️ Development Guides

Practical guides for common development tasks:

- **[PLUGIN_QUICKSTART.md](PLUGIN_QUICKSTART.md)** - Plugin development quick start
- **[Component Library](guides/COMPONENT_LIBRARY.md)** - Reusable UI components (ExpandableCard, JellyOrb)
- **[UI Best Practices](guides/UI_BEST_PRACTICES.md)** - UI/UX design patterns and guidelines
- **[Contributing Guide](CONTRIBUTING.md)** - Documentation standards and best practices

---

## 📦 Plugin Documentation

Documentation for built-in plugins:

- **[WinSwitcher](Plugins/WinSwitcher.md)** - Window switching and application launching
- **[PkiPlugin](Plugins/PkiPlugin.md)** - PKI credentials management (Core plugin)
- **[BasicCommand](Plugins/BasicCommand.md)** - Basic command execution
- **[VbaRunner](Plugins/VbaRunner.md)** - VBA script runner
- **[BookmarkletRunner](Plugins/BookmarkletRunner.md)** - Bookmarklet runner
- **[SystemCommand](Plugins/SystemCommand.md)** - System command execution

---

## 📦 Historical Archives

Archived documents for reference (completed features and planning documents):

### 2026-03 Archives
- **[Phase 2 Task 1](archive/2026-03/phase2-task1/)** - Hot reload manager implementation ✨ NEW
- **[Refactoring Report](archive/2026-03/REFACTORING_REPORT.md)** - Plugin configuration architecture refactoring (Phase 1 & 2)
- **[Plugin Settings Migration](archive/2026-03/PLUGIN_SETTINGS_MIGRATION.md)** - Plugin dashboard enhancement proposal (planning)

### 2026-01 Archives
- **[PKI Implementation](archive/2026-01/PKI_IMPLEMENTATION.md)** - Pulsar Key Injector (PKI) module implementation details

---

## 🏛️ Architecture Decision Records (ADR)

Documents recording important architectural decisions:

> **Note**: ADR section is currently empty. Future architectural decisions will be documented here.

---

## 📖 Documentation Guidelines

### Document Lifecycle

1. **Draft** (`.draft.md` suffix) - Work in progress
2. **Review** ("Status: Under Review" header) - Awaiting approval
3. **Published** (no suffix, version number) - Active documentation
4. **Archived** (moved to `archive/YYYY-MM/`) - Historical reference
5. **Deprecated** ("⚠️ DEPRECATED" header) - Obsolete, to be removed after 6 months

### Naming Conventions

- **Core Documents**: `UPPERCASE_WITH_UNDERSCORES.md` (e.g., `ARCHITECTURE.md`)
- **Guides**: `UPPERCASE_WITH_UNDERSCORES.md` (e.g., `COMPONENT_LIBRARY.md`)
- **Archives**: `YYYY-MM-DESCRIPTIVE_NAME.md` (e.g., `2026-03-REFACTORING_REPORT.md`)
- **Language**: English (Chinese only in comments)

### Document Template

```markdown
# Document Title

**Status**: Draft | Published | Archived  
**Version**: v1.0.0  
**Last Updated**: YYYY-MM-DD  
**Author**: [Your Name]  
**Related Documents**: [Links]

---

## Table of Contents
[Auto-generated]

## Overview
[1-2 paragraph introduction]

## [Main Sections]
...

---

**Change History**:
- v1.0.0 (2026-03-01): Initial version
```

---

## 🔍 Quick Reference

### Finding Information

| I want to... | Read this document |
|--------------|-------------------|
| Understand the system architecture | [ARCHITECTURE.md](../ARCHITECTURE.md) |
| Develop a new plugin | [PLUGIN_DEVELOPMENT.md](../PLUGIN_DEVELOPMENT.md) |
| Work with AI agents | [AGENTS.md](../AGENTS.md) |
| Use reusable UI components | [Component Library](guides/COMPONENT_LIBRARY.md) |
| Follow UI design patterns | [UI Best Practices](guides/UI_BEST_PRACTICES.md) |
| Learn about PKI implementation | [PKI Implementation](archive/2026-01/PKI_IMPLEMENTATION.md) |

### Document Status Legend

- ✅ **Published** - Current, actively maintained
- 📦 **Archived** - Historical reference, no longer updated
- ⚠️ **Deprecated** - Obsolete, scheduled for removal
- 🚧 **Draft** - Work in progress

---

## 📞 Contributing to Documentation

When creating or updating documentation:

1. **Read existing docs** to avoid duplication
2. **Follow the template** for consistency
3. **Use clear, concise language** - prioritize actionable information
4. **Include code examples** where applicable
5. **Link related documents** for context
6. **Update this index** when adding new documents

---

**Maintained by**: Pulsar Development Team  
**Questions?** Check [AGENTS.md](../AGENTS.md) for development guidelines
