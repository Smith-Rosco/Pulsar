# Build and Run Commands

**Status**: Published  
**Scope**: Operations  
**Applies To**: All developers and AI agents  
**Last Updated**: 2026-08-17

---

## Overview

The solution contains a single project. Run all commands from the repository root.

---

## Build Commands

### Restore Dependencies

```bash
dotnet restore Pulsar/Pulsar/Pulsar.csproj
```

### Build (Debug)

```bash
dotnet build Pulsar/Pulsar/Pulsar.csproj
```

### Build (Release)

```bash
dotnet build Pulsar/Pulsar/Pulsar.csproj -c Release
```

### Run Application

```bash
dotnet run --project Pulsar/Pulsar/Pulsar.csproj
```

---

## Publish a Release (Artifact Convention)

**Primary path**: the publish skill (`.agents/skills/publish/SKILL.md`) — say "发布一个本地版本"
or run `/publish`. The AI orchestrates stage scripts; the sections below document the underlying
conventions for manual operation or troubleshooting.

Release artifacts are published into `Artifacts/` as **two zips** per version:

| Artifact | Contents |
|----------|----------|
| `Artifacts\Pulsar-v{Version}-full.zip` | Self-contained single-file app (ReadyToRun), includes `*_cor3.dll` native WPF runtime libs |
| `Artifacts\Pulsar-v{Version}-portable.zip` | Framework-dependent single-file app (needs .NET 8 Desktop Runtime), **no** `*_cor3.dll` |

Both publish dirs also contain `Pulsar.pdb` and `Assets/` (tutorial steps + demo scripts).

The version lives in `<Version>` in `Pulsar/Pulsar/Pulsar.csproj` (e.g. `1.4.1`); `<FileVersion>`
and `<AssemblyVersion>` follow it as `x.y.z.0`. Bump all three before publishing a new release
(`Set-ProjectVersion.ps1` does this automatically).

### Manual publish (escape hatch)

```powershell
$v = "1.9.1"  # matches <Version> in Pulsar.csproj
$root = "E:\8_Project\10_C#\Pulsar_Project"  # repo root
# full: self-contained
dotnet publish Pulsar/Pulsar/Pulsar.csproj -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:PublishReadyToRun=true `
  -p:PublishDir="$root\Artifacts\publish\v$v\full\"
# portable: framework-dependent
dotnet publish Pulsar/Pulsar/Pulsar.csproj -c Release -r win-x64 --self-contained false `
  -p:PublishSingleFile=true `
  -p:PublishDir="$root\Artifacts\publish\v$v\portable\"
```

Package with the skill scripts (they verify the `PK` zip magic and required entries):

```powershell
pwsh .agents/skills/publish/scripts/Pack-Zips.ps1 -Version $v
```

> ⚠️ The `*_cor3.dll` native libs are part of the self-contained WPF single-file output and must
> ship alongside `Pulsar.exe` in the **full** zip. Earlier artifacts omitted them and the published
> build could not start.

---

## Publish Automation (pi Extension + Skill)

The release ritual is automated by a pi coding-agent extension (`.pi/extensions/publish-local/`)
that delegates to the publish skill (`.agents/skills/publish/SKILL.md` + `scripts/`):

| Command | Behavior |
|---------|----------|
| `/publish` | Local release: version suggestion → confirm → build full+portable → verify → zip (local-version mode) |
| `/publish gh` | Release mode: version + notes → CHANGELOG → commit/tag (notes in tag message) → push → CI builds and creates the GitHub Release |
| `/publish gh-only` | Skip build; use the existing `Artifacts/Pulsar-v{ver}.zip` to publish to GitHub |
| `/publish minor` / `/publish 1.6.0` | Explicit bump type or full version |

Key behaviors:

- **Three modes** (`local-artifact` / `local-version` / `release`) — see SKILL.md §0. Default for
  "发布一个本地版本" is `local-version`.
- Every destructive step (clearing `Artifacts/publish/`, overwriting an existing zip, pushing to
  origin) asks for confirmation.
- **GitHub publishing is opt-in per invocation and defaults to off.** In release mode the GitHub
  Release itself is created by CI (`.github/workflows/release.yml`) from the pushed `v*` tag;
  release notes are the **tag message** and can be edited before publishing.
- Tags are created as **annotated tags** with `core.commentChar=§` so `###` section headers survive
  and CI's `--notes-from-tag` carries full notes.
- The csproj version bump is rolled back automatically if publish/verify/zip fails before the commit
  step.
- GitHub publishing requires `gh` (`winget install GitHub.cli` + `gh auth login`).
- CI note: `.github/workflows/release.yml` skips release creation when the release already exists,
  so the CI-created release (from the locally verified tag) is never overwritten.

Usage from inside pi: `/reload` (first install), then `/publish` or ask the agent "发布本地版本".

---

## Test Commands

**Current Status**: xUnit test project at `Pulsar/Pulsar.Tests/` (410+ tests).

### Run Tests

```bash
dotnet test Pulsar/Pulsar.Tests/Pulsar.Tests.csproj
```

---

## Validation After Changes

After making code changes, always run:

```bash
dotnet build Pulsar/Pulsar/Pulsar.csproj
```

This ensures no compilation errors were introduced.

---

## Related Documents

- [AGENTS.md](../../AGENTS.md) - AI agent operational guide
- [CONTRIBUTING.md](../CONTRIBUTING.md) - Documentation standards

---

**Change History**:
- v1.0.0 (2026-03-03): Initial extraction from AGENTS.md
