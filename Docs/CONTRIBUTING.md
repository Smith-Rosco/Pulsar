# Documentation Contributing Guide

**Version**: v1.0.0  
**Last Updated**: 2026-03-01

---

## Overview

This guide defines standards and best practices for creating and maintaining Pulsar documentation.

---

## Document Lifecycle

### 1. Draft Phase
- **File Naming**: Add `.draft.md` suffix (e.g., `NEW_FEATURE.draft.md`)
- **Header**: Include "Status: Draft" in document header
- **Purpose**: Work in progress, not yet reviewed

### 2. Review Phase
- **File Naming**: Remove `.draft.md` suffix
- **Header**: Change to "Status: Under Review"
- **Purpose**: Awaiting team approval

### 3. Published Phase
- **File Naming**: Standard naming convention (see below)
- **Header**: "Status: Published" with version number
- **Purpose**: Active, maintained documentation

### 4. Archived Phase
- **Location**: Move to `Docs/archive/`
- **Header**: Add "⚠️ ARCHIVED DOCUMENT" warning at top
- **Purpose**: Historical reference, no longer updated

### 5. Deprecated Phase
- **Header**: Add "⚠️ DEPRECATED" warning
- **Retention**: Keep for 6 months, then delete
- **Purpose**: Obsolete content scheduled for removal

---

## Naming Conventions

### Core Documents (Root Level)
- **Format**: `UPPERCASE_WITH_UNDERSCORES.md`
- **Examples**: `ARCHITECTURE.md`, `PLUGIN_DEVELOPMENT.md`, `AGENTS.md`
- **Location**: Project root directory

### Guide Documents (docs/guides/)
- **Format**: `UPPERCASE_WITH_UNDERSCORES.md`
- **Examples**: `COMPONENT_LIBRARY.md`, `UI_BEST_PRACTICES.md`
- **Location**: `docs/guides/`

### Archive Documents (Docs/archive/)
- **Format**: `YYYY-MM-DD-DESCRIPTIVE_NAME.md`
- **Examples**: `2026-03-02-HANDOVER_V4.1.0.md`, `2026-03-09-ARCHITECTURE_FIX_SUMMARY.md`
- **Location**: `Docs/archive/`

### Architecture Decision Records (docs/decisions/)
- **Format**: `NNN-descriptive-title.md` (NNN = zero-padded number)
- **Examples**: `001-plugin-metadata-system.md`, `002-circuit-breaker-pattern.md`
- **Location**: `docs/decisions/`

---

## Language Standards

### Primary Language: English
- All documentation must be written in English
- Chinese is only permitted in code comments or inline examples
- Rationale: Ensures accessibility for international contributors and AI agents

### Exceptions
- User-facing UI text (handled separately in localization files)
- Historical documents (can remain in original language if archived)

---

## Document Template

```markdown
# Document Title

**Status**: Draft | Published | Archived | Deprecated  
**Version**: v1.0.0  
**Last Updated**: YYYY-MM-DD  
**Author**: [Your Name]  
**Related Documents**: [Link to related docs]

---

## Table of Contents

[Auto-generated or manual list]

---

## Overview

[1-2 paragraph introduction explaining the document's purpose and scope]

---

## [Main Section 1]

[Content]

### [Subsection 1.1]

[Content]

---

## [Main Section 2]

[Content]

---

## Related Documents

- [Document Name](./path/to/document.md) - Brief description

---

**Change History**:
- v1.0.0 (YYYY-MM-DD): Initial version
- v1.1.0 (YYYY-MM-DD): Added section X
```

---

## Content Guidelines

### Writing Style

1. **Be Concise**: Prioritize actionable information over verbose explanations
2. **Use Active Voice**: "The plugin executes" not "The plugin is executed"
3. **Avoid Jargon**: Explain technical terms on first use
4. **Use Examples**: Include code snippets and practical examples
5. **Structure Clearly**: Use headings, lists, and tables for readability

### Code Examples

- **Always include language identifier** in code blocks:
  ```csharp
  public class Example { }
  ```
- **Keep examples minimal**: Show only relevant code
- **Add comments**: Explain non-obvious logic
- **Test examples**: Ensure code compiles and runs

### Links

- **Use relative paths** for internal documents:
  ```markdown
  [ARCHITECTURE.md](../ARCHITECTURE.md)
  ```
- **Use descriptive text** for links:
  ```markdown
  ✅ See [Plugin Development Guide](./PLUGIN_DEVELOPMENT.md)
  ❌ See [here](./PLUGIN_DEVELOPMENT.md)
  ```

---

## Document Types

### Architecture Documents
- **Purpose**: Describe system design and technical decisions
- **Audience**: Developers, architects
- **Update Frequency**: When architecture changes
- **Examples**: `ARCHITECTURE.md`

### Development Guides
- **Purpose**: Teach how to implement features
- **Audience**: Developers
- **Update Frequency**: When APIs or patterns change
- **Examples**: `PLUGIN_DEVELOPMENT.md`, `COMPONENT_LIBRARY.md`

### Operational Guides
- **Purpose**: Explain workflows and conventions
- **Audience**: AI agents, developers
- **Update Frequency**: When processes change
- **Examples**: `AGENTS.md`

### Architecture Decision Records (ADR)
- **Purpose**: Document important architectural decisions
- **Audience**: Future developers, architects
- **Update Frequency**: Immutable (never updated, only superseded)
- **Format**: See [ADR Template](#adr-template)

---

## ADR Template

```markdown
# ADR-NNN: [Decision Title]

**Status**: Proposed | Accepted | Deprecated | Superseded by ADR-XXX  
**Date**: YYYY-MM-DD  
**Deciders**: [List of people involved]

---

## Context

[Describe the issue or problem that requires a decision]

## Decision

[Describe the decision that was made]

## Rationale

[Explain why this decision was made, including alternatives considered]

## Consequences

### Positive
- [Benefit 1]
- [Benefit 2]

### Negative
- [Drawback 1]
- [Drawback 2]

### Neutral
- [Impact 1]
- [Impact 2]

---

## Related Decisions

- [ADR-XXX: Related Decision](./XXX-related-decision.md)
```

---

## Maintenance

### Regular Reviews
- **Quarterly**: Review all Published documents for accuracy
- **After Major Changes**: Update affected documentation immediately
- **Archive Old Content**: Move outdated docs to archive/ with proper markers

### Updating Documents
1. Read the existing document completely
2. Make changes while preserving structure
3. Update "Last Updated" date
4. Increment version number if significant changes
5. Add entry to "Change History"
6. Update related documents if necessary

### Deprecating Documents
1. Add "⚠️ DEPRECATED" warning at top
2. Explain why deprecated and link to replacement
3. Set removal date (6 months from deprecation)
4. Update docs/README.md to reflect status

---

## Quality Checklist

Before publishing a document, verify:

- [ ] Document follows naming conventions
- [ ] Header includes all required fields (Status, Version, Date)
- [ ] Content is written in English
- [ ] Code examples are tested and working
- [ ] Links are valid (no broken links)
- [ ] Related documents are cross-referenced
- [ ] Table of contents is accurate
- [ ] Spelling and grammar are correct
- [ ] Document is added to docs/README.md index

---

## Tools

### Recommended Markdown Editors
- Visual Studio Code with Markdown extensions
- Typora
- MarkText

### Linting
- Use markdownlint for consistency
- Configure `.markdownlint.json` in project root

### Link Checking
- Use markdown-link-check to verify links
- Run before committing documentation changes

---

## Questions?

- Check [docs/README.md](./README.md) for documentation index
- Review [AGENTS.md](../AGENTS.md) for development guidelines
- Ask in team chat or create an issue

---

**Maintained by**: Pulsar Documentation Team  
**Feedback**: Submit issues or pull requests to improve this guide
