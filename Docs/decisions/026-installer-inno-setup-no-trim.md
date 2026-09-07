# ADR-026: Installer & Portable Packaging — Inno Setup over WiX, No Trim

**Status**: Accepted
**Date**: 2026-09-06
**Deciders**: Project owner (milo)
**Related**: ADR-025 (update handoff target), openspec change `2026-09-05-installer-and-portable-packaging`

---

## Context

Pulsar ships to end users in two forms today (full/portable zips via the publish
skill), but there is no real installer: no Program Files layout, no start-menu
entry, no uninstaller, no optional autostart. The in-app auto-update (ADR-025)
also needs a handoff target ("run the downloaded installer"), which implies a
setup artifact. Additionally, a standalone (self-contained, unzip-and-run)
distribution must boot on machines without the .NET 8 Desktop Runtime.

## Decision

1. **Installer technology: Inno Setup 6, not WiX.**
   - Script size and iteration speed: one declarative `.iss` file (~60 lines) vs
     WiX's XML component authoring; the owner's release cadence favors edit-run loops.
   - Feature fit: Program Files install, per-user autostart task, uninstall with
     data-retention messaging are all first-class in Inno; WiX gains nothing here
     for a single-exe payload.
   - No MSI/enterprise-deployment requirement today — the main reason to prefer
     WiX does not apply. Re-evaluate if enterprise GPO/MSI distribution becomes real.
2. **Standalone distribution: self-contained single-file, win-x64, no trim.**
   - `PublishSingleFile` + `SelfContained` + `IncludeNativeLibrariesForSelfExtract`
     (the `*_cor3.dll` native WPF runtime libs are embedded — an earlier release
     shipped without them and could not start).
   - `PublishTrimmed=false` is a hard rule: the plugin system discovers and loads
     plugin assemblies via reflection (collectible ALC); trimming breaks
     reflection-based discovery. Not a trade-off — it is incompatible.
   - All publish-only properties are conditioned on `RuntimeIdentifier` in
     `Pulsar.csproj` so day-to-day builds are untouched.
3. **Artifact naming (supersedes full/portable):**
   - `Pulsar-v{Version}-Setup.exe` (installer)
   - `Pulsar-v{Version}-Standalone-win-x64.zip` (portable, runtime-free)
   - `SHA256SUMS.txt` manifest covering both (consumed by the release checklist
     and the update service's integrity verification).
4. **Upgrade/uninstall semantics (user-data contract):**
   - User data lives in `%AppData%\Pulsar`, outside `{app}` — overwrite-installs
     never touch it.
   - Uninstall keeps user data by default and says so in the uninstall
     confirmation; a full wipe is a documented manual step.
5. **Automation home: `dev.ps1 publish`** — one command produces
   publish → Standalone zip → Setup.exe (when Inno's ISCC is installed; a
   warning, not a failure, when absent) → SHA256 manifest. Idempotent: the stage
   directory is wiped per run.

## Alternatives Considered

- **WiX v4/v5 (MSI)**: rejected for now — heavyweight authoring for a single-exe
  payload, no MSI requirement exists; revisit for enterprise channels.
- **MSIX**: rejected — sideload/store friction and containerization semantics
  complicate the plugin system's file-based ALC loading.
- **Self-contained + trimming**: rejected — breaks plugin reflection loading.
- **zip-only, no installer**: rejected — the update flow needs a handoff target
  and non-technical users need a standard install/uninstall experience.

## Consequences

- `artifacts/publish/` becomes the release-asset source of truth for the
  checklist (`Docs/ops/RELEASE_CHECKLIST.md`).
- ISCC absence degrades gracefully (zip-only) but release gating requires the
  Setup.exe — recorded in the checklist, not silently skipped.
- The update service's "ReadyToInstall → run the downloaded installer" handoff
  targets `Pulsar-v{Version}-Setup.exe` for installer-based installs and the
  Standalone zip for portable ones.

---

## 修订（2026-09-07，append-only）：本地产物收敛为两个 ZIP

原 ADR 声明的「本地 publish 产出 Setup.exe + Standalone zip + SHA256SUMS」调整为 **CI-only 资产**（`release.yml` 继续产出并上传 GitHub Release，本节不改变其语义）。变更动机：实测一次本地发布会把同一份 ~80MB full Pulsar.exe 落 4 处（full/、full.zip、Standalone.zip、stage/standalone-stage 残留）、Setup.exe 落 2 处，单次约 330M 冗余，多版本累积后 `artifacts/` 达 966M。

- **本地终态**：`artifacts/` 根仅保留 `Pulsar-$version-{full,portable}.zip`；`publish/v<ver>/` 产物目录在打包校验成功后由 Pack-Zips 自动删除（`-KeepPublishDirs` 可保留）。
- **回退路径**（CI 不可用、手动上传）：`Build-Publish.ps1` 与 `Pack-Zips.ps1` 均加 `-WithInstaller` 显式产出 installer 三件套；stage/publish 内 Setup 副本用后即删（不再双份）。
- GitHub Release 资产清单（Setup/Standalone/SHA256SUMS）与更新服务的安装器路径不变。
- 承载实现：`.agents/skills/publish/scripts/`（Build-Publish.ps1 / Pack-Zips.ps1）。
