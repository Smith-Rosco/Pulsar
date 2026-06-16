## Why

The Pulsar project has accumulated significant structural debt over months of active development. Root-level task documents from completed features remain scattered, the Docs directory has bloated with 24+ historical handoff/report files, scripts are split across root `Scripts/` and `Assets/Scripts/Demo/` without clear ownership, the Tutorial subsystem is fragmented across 6 directories (Helpers, Models, Views, Services, Resources, Assets), plugin metadata is split between `Core/Plugin/Metadata/` and `Core/Plugin/Versioning/`, and `Core/Plugin/Dependencies/` contains example/docs code mixed with runtime code. This structural fragmentation increases onboarding friction, slows navigation, and creates ambiguity about where new code should go.

## What Changes

- **Phase 1 — Safe Cleanup**: Move 3 stale root-level `.md` files to `Docs/archive/`; delete `Scripts/__pycache__/`; delete `NativeMethods.txt`; merge `Assets/TutorialSteps.*.json` into `Resources/Tutorial/`
- **Phase 2 — Docs & Scripts Restructuring**: Flatten `Docs/archive/2026-01/` and `Docs/archive/2026-03/` into single `Docs/archive/`; archive 7 TUTORIAL_SYSTEM* files from `Docs/architecture/`; merge `Docs/architecture/pki/` into other PKI docs; move `PHASE2_PROGRESS_REPORT.md` to archive; move `Scripts/Bookmarklet/` and `Scripts/VBA/` into respective plugin directories; move `make_pulsar_ico.py` to `Assets/Brand/`; move `Assets/Scripts/Demo/` to plugin directories; move `PluginTemplate/` to `Pulsar/PluginTemplate/`; update `AGENTS.md` with new paths
- **Phase 3 — Code-Level Restructuring**: Consolidate Tutorial system into `Features/Tutorial/`; merge `Core/Plugin/Versioning/` into `Core/Plugin/Metadata/`; delete `Core/Plugin/Dependencies/README.md` and `DependencyIsolationExample.cs`; clean up `openspec/changes/archive/` of completed changes older than 2026-06

## Capabilities

### New Capabilities
- `safe-cleanup`: Remove stale root-level docs, Python cache, NativeMethods.txt, and duplicate TutorialSteps JSON files
- `docs-restructuring`: Reorganize Docs/ directory with consolidated archive, architected TUTORIAL_SYSTEM docs, and merged PKI documentation
- `scripts-reorganization`: Move test/demo scripts from root Scripts/ and Assets/ into plugin-owned directories
- `tutorial-consolidation`: Merge the fragmentated Tutorial subsystem (Helpers, Models, Views, Services, Resources, Assets) into a single `Features/Tutorial/` directory
- `plugin-infrastructure-cleanup`: Merge `Core/Plugin/Versioning/` into `Core/Plugin/Metadata/`, remove doc/example code from `Core/Plugin/Dependencies/`, update `AGENTS.md` references
- `openspec-archive-cleanup`: Remove completed openspec changes from archive that are older than 2026-06

### Modified Capabilities
*(No existing spec-level capability requirements are changing — this is a structural/infrastructure change only.)*

## Impact

- **Code**: Tutorial namespace changes in Phase 3 will require updating all `using` statements across 60+ files; Plugin metadata namespace merge will affect ~12 files
- **Build**: No runtime impact — all changes are file moves, deletions, and namespace updates
- **Docs**: `AGENTS.md` path references must be updated; `Docs/README.md` may need updates
- **Git history**: File moves will break `git blame` continuity for moved files; recommend using `git mv` and annotating moves in commit messages
