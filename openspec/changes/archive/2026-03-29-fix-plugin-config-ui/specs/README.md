# Specs

This change is a refactoring effort that does not introduce new spec-level requirements.

## Rationale

The change:
1. Unifies existing configuration paths (no new capabilities)
2. Fixes bugs in existing implementations (PkiPlugin, WinSwitcher)
3. Adds basic settings to extension plugins (enhancement, not new requirements)

No new spec files are required as:
- All existing plugin behavior is preserved
- No new user-facing capabilities are added
- Changes are purely internal architecture
