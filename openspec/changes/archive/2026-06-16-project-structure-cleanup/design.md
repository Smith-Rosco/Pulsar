## Context

The Pulsar project has grown organically over 6+ months of active development, accumulating structural debt across multiple dimensions:

1. **Root-level document creep**: 3 completed-feature task documents (`ARCHITECTURE_FIX_SUMMARY.md`, `DYNAMIC_LAYOUT_TEST_GUIDE.md`, `TODO_SLOTS_PER_PAGE.md`) remain at the project root, creating noise for new contributors.

2. **Docs/ directory bloat**: 24+ historical handoff/report files split across `archive/2026-01/` and `archive/2026-03/`; 7 TUTORIAL_SYSTEM design documents in `architecture/` describe a now-complete feature; `architecture/pki/` is a single-topic subdirectory that can be merged.

3. **Scripts fragmentation**: Test scripts live in root `Scripts/Bookmarklet/` and `Scripts/VBA/` instead of their respective plugin directories; demo scripts live in `Assets/Scripts/Demo/`; Python cache exists in `__pycache__/`.

4. **Tutorial system fragmentation**: The tutorial subsystem (40+ files) is scattered across 6 directories: `Helpers/Tutorial/`, `Models/Tutorial/`, `Views/Tutorial/`, `Services/Tutorial/`, `Resources/Tutorial/`, `Assets/TutorialSteps.*.json`.

5. **Plugin infrastructure sprawl**: Plugin metadata is split between `Core/Plugin/Metadata/` and `Core/Plugin/Versioning/`; `Core/Plugin/Dependencies/` contains a README.md and example code mixed with runtime code.

6. **openspec archive growth**: 50+ completed openspec change records in `openspec/changes/archive/`.

## Goals / Non-Goals

**Goals:**
- Remove all stale/completed task documents from the project root
- Consolidate Docs/archive from a date-split structure to a flat structure
- Archive completed design documents (TUTORIAL_SYSTEM) from Docs/architecture/
- Move test/demo scripts into plugin-owned directories
- Consolidate the Tutorial subsystem into a single `Features/Tutorial/` directory
- Merge `Core/Plugin/Versioning/` into `Core/Plugin/Metadata/`
- Remove doc/example code from `Core/Plugin/Dependencies/`
- Clean up openspec archive of completed changes older than 2026-06
- Update `AGENTS.md` and `Docs/README.md` to reflect new paths
- Ensure `dotnet build` passes at every phase boundary

**Non-Goals:**
- No runtime behavior changes — this is purely structural
- No API/interface changes
- No plugin logic modifications
- No dependency upgrades
- No feature additions or removals
- No changes to the radial menu, settings UI, or user-facing functionality

## Decisions

### Decision 1: Use `git mv` for all file moves (not copy + delete)
- **Rationale**: Preserves git history/blame continuity for all moved files
- **Alternative considered**: Copy + delete would create cleaner diffs but lose history
- **Chosen approach**: `git mv` for every file move; document moves in commit messages

### Decision 2: Flatten archive directories instead of re-dating
- **Rationale**: Files already have date-prefixed names (e.g., `2026-03-02-HANDOVER_V4.1.0.md`). Removing the month subdirectories simplifies navigation without losing chronological information.
- **Alternative considered**: Renaming all files to a unified scheme — unnecessary when dates are already embedded
- **Chosen approach**: Single `Docs/archive/` directory; no subdirectory splitting

### Decision 3: Tutorial consolidation into `Features/Tutorial/` (not `Services/Tutorial/`)
- **Rationale**: The tutorial is a full feature with Models, Views, Services, Helpers, and Resources. Placing it under `Features/` follows a feature-based organization pattern that groups all layers together, which is a common .NET pattern for larger subsystems. `Services/` should remain for cross-cutting services only.
- **Alternative considered**: Keep in `Services/Tutorial/` — this would continue the fragmentation since Views/Models/Helpers don't belong in Services.
- **Chosen approach**: `Pulsar/Pulsar/Features/Tutorial/` with subdirectories: `Services/`, `Views/`, `Models/`, `Helpers/`, `Data/`

### Decision 4: Merge `Core/Plugin/Versioning/` into `Core/Plugin/Metadata/`
- **Rationale**: Versioning (PluginManifest, PluginManifestLoader, PluginVersionResolver) is metadata about plugins. Keeping it separate from PluginMetadata, SlotActionMetadata, etc. creates unnecessary navigation overhead. The merged namespace includes both structural and versioning metadata.
- **Alternative considered**: Keep separate — justified only if versioning logic grows independently, but currently it's 3 small files.
- **Chosen approach**: Move `Versioning/` files into `Core/Plugin/Metadata/` and update namespaces

### Decision 5: Delete (not archive) `Core/Plugin/Dependencies/README.md` and `DependencyIsolationExample.cs`
- **Rationale**: The README describes an implemented system — its content is either redundant with architecture docs or outdated. The Example.cs is demo code that doesn't belong in the runtime source tree. Neither is referenced by any production code.
- **Alternative considered**: Move to `Docs/examples/` — would add maintenance burden for code that may not even compile independently
- **Chosen approach**: Delete both files

### Decision 6: Openspec archive cleanup — delete only 2026-03 and 2026-04 completed changes
- **Rationale**: Recent changes (2026-06) may still be referenced for context during implementation. Older changes from March/April are fully completed and their specs are either merged into `openspec/specs/` or stale.
- **Alternative considered**: Delete all archives — too aggressive; keep all — unnecessary bloat
- **Chosen approach**: Remove `openspec/changes/archive/` directories dated 2026-03 and 2026-04; verify each has a corresponding implementation commit before deletion

## Risks / Trade-offs

| Risk | Likelihood | Impact | Mitigation |
|------|-----------|--------|------------|
| Tutorial namespace change breaks build | High | High (60+ files affected) | Run `dotnet build` after each file group; use global search-and-replace for old namespace |
| Git blame confusion from moved files | Medium | Low | Use `git mv`; annotate in commit messages |
| AGENTS.md link rot if Docs path changes not propagated | Medium | Medium | Search `AGENTS.md` for all affected paths before finalizing |
| Broken references in Doc links between files | Medium | Low | Update Docs/README.md and AGENTS.md in same pass |
| DependencyIsolationExample.cs actually being referenced | Low | Medium | Use `git grep -r DependencyIsolationExample` before deletion |
| TutorialSteps renames break loading | Low | High | Trace the loading path in `TutorialStepLoader.cs` and `OnboardingTemplateService.cs` before changing paths |
