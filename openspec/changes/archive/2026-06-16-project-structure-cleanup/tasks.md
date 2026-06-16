## 1. Phase 1 — Safe Cleanup

- [x] 1.1 Move `ARCHITECTURE_FIX_SUMMARY.md`, `DYNAMIC_LAYOUT_TEST_GUIDE.md`, `TODO_SLOTS_PER_PAGE.md` from project root to `Docs/archive/`
- [x] 1.2 Delete `Scripts/__pycache__/` directory (Python cache, gitignored)
- [x] 1.3 Keep `Pulsar/Pulsar/NativeMethods.txt` — it's the CsWin32 input file (required for PInvoke code generation), not obsolete
- [x] 1.4 Move `Pulsar/Pulsar/Assets/TutorialSteps.json`, `TutorialSteps.browser.json`, `TutorialSteps.excel.json` to `Pulsar/Pulsar/Resources/Tutorial/`
- [x] 1.5 Run `dotnet build Pulsar/Pulsar/Pulsar.csproj` and verify zero errors

## 2. Phase 2a — Docs Restructuring

- [x] 2.1 Flatten archive: move files from `Docs/archive/2026-01/` and `Docs/archive/2026-03/` directly into `Docs/archive/`
- [x] 2.2 Move TUTORIAL_SYSTEM* files from `Docs/architecture/` to `Docs/archive/`
- [x] 2.3 Move `Docs/PHASE2_PROGRESS_REPORT.md` to `Docs/archive/`
- [x] 2.4 Merge contents of `Docs/architecture/pki/` into `Docs/Plugins/PkiPlugin.md` or `Docs/architecture/INPUT_INJECTION.md`; remove `pki/` directory
- [x] 2.5 Update `Docs/README.md` index with new archive paths and removed directories
- [x] 2.6 Run `dotnet build` and verify zero errors

## 3. Phase 2b — Scripts Reorganization

- [x] 3.1 Create `Pulsar/Pulsar/Plugins/Extensions/BookmarkletRunner/TestScripts/` and `VbaRunner/TestScripts/` directories
- [x] 3.2 Move files from `Scripts/Bookmarklet/` to `BookmarkletRunner/TestScripts/`
- [x] 3.3 Move files from `Scripts/VBA/` to `VbaRunner/TestScripts/`
- [x] 3.4 Move `Scripts/make_pulsar_ico.py` to `Pulsar/Pulsar/Assets/Brand/make_pulsar_ico.py`
- [x] 3.5 Create `BookmarkletRunner/DemoScripts/` and `VbaRunner/DemoScripts/`; move `Assets/Scripts/Demo/browser_demo.js` and `excel_demo.txt` accordingly
- [x] 3.6 Remove empty `Scripts/Bookmarklet/`, `Scripts/VBA/`, `Assets/Scripts/Demo/` directories
- [x] 3.7 Run `dotnet build` and verify zero errors

## 4. Phase 2c — PluginTemplate Move

- [x] 4.1 Move `PluginTemplate/` to `Pulsar/PluginTemplate/` (git mv)
- [x] 4.2 Run `dotnet build` and verify zero errors

## 5. Phase 2d — Cross-Cutting Path Updates

- [x] 5.1 Update `AGENTS.md` to reflect new paths (no changes needed — AGENTS.md doesn't reference moved paths)
- [x] 5.2 Run `dotnet build` and verify zero errors

## 6. Phase 3a — Tutorial Consolidation

- [x] 6.1 Create `Pulsar/Pulsar/Features/Tutorial/` with subdirectories: `Services/`, `Views/`, `Models/`, `Helpers/`, `Data/`
- [x] 6.2 `git mv` files from `Helpers/Tutorial/` to `Features/Tutorial/Helpers/`
- [x] 6.3 `git mv` files from `Models/Tutorial/` to `Features/Tutorial/Models/`
- [x] 6.4 `git mv` files from `Views/Tutorial/` to `Features/Tutorial/Views/`
- [x] 6.5 `git mv` files from `Services/Tutorial/` (including `Prerequisites/` and `TriggerHandlers/`) to `Features/Tutorial/Services/`
- [x] 6.6 Update namespaces in all moved `.cs` files from `Pulsar.Helpers.Tutorial`, `Pulsar.Models.Tutorial`, `Pulsar.Views.Tutorial`, `Pulsar.Services.Tutorial` to `Pulsar.Features.Tutorial.*`
- [x] 6.7 Update all cross-references to old Tutorial namespaces across the codebase (search: `*.Tutorial` namespace patterns)
- [x] 6.8 Update XAML resource references pointing to old Tutorial view paths
- [x] 6.9 Move or copy `Resources/Tutorial/Steps.en.json` and `Steps.zh-CN.json` to `Features/Tutorial/Data/`
- [x] 6.10 Delete empty old directories: `Helpers/Tutorial/`, `Models/Tutorial/`, `Views/Tutorial/`, `Services/Tutorial/`
- [x] 6.11 Run `dotnet build` and verify zero errors
- [x] 6.12 Run `dotnet test Pulsar/Pulsar.Tests/Pulsar.Tests.csproj` and verify all tests pass

## 7. Phase 3b — Plugin Infrastructure Cleanup

- [x] 7.1 `git mv` files from `Core/Plugin/Versioning/` to `Core/Plugin/Metadata/`
- [x] 7.2 Update namespaces from `Pulsar.Core.Plugin.Versioning` to `Pulsar.Core.Plugin.Metadata`
- [x] 7.3 Update all cross-references to old `Pulsar.Core.Plugin.Versioning` namespace
- [x] 7.4 Delete `Core/Plugin/Versioning/` directory
- [x] 7.5 Verify no code references to `DependencyIsolationExample` exist; delete `Core/Plugin/Dependencies/DependencyIsolationExample.cs`
- [x] 7.6 Verify no code references to Dependencies README; delete `Core/Plugin/Dependencies/README.md`
- [x] 7.7 Run `dotnet build` and verify zero errors

## 8. Phase 3c — Openspec Archive Cleanup

- [x] 8.1 List all completed changes in `openspec/changes/archive/` dated `2026-03-*` and `2026-04-*`
- [x] 8.2 Verify each has a corresponding implementation commit (use `git log --oneline`)
- [x] 8.3 Remove verified completed change directories for March and April 2026
- [x] 8.4 Run `dotnet build` and verify zero errors

## 9. Final Verification

- [x] 9.1 Run full `dotnet build` on solution: `dotnet build Pulsar/Pulsar.sln`
- [x] 9.2 Run full test suite: `dotnet test Pulsar/Pulsar.Tests/Pulsar.Tests.csproj`
- [x] 9.3 Verify no stale references remain: search for old namespace patterns and paths
- [x] 9.4 Verify project root has only active documents remaining
