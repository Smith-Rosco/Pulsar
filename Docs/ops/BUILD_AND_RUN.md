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

Release artifacts are published into `Artifacts/` and packaged as a zip. The version
is taken from `<Version>` in `Pulsar/Pulsar/Pulsar.csproj` (e.g. `1.4.1`). Bump the
version there before publishing a new release.

Publish parameters are captured in
`Pulsar/Pulsar/Properties/PublishProfiles/FolderProfile.pubxml`
(**Release / win-x64 / SelfContained / PublishSingleFile / PublishReadyToRun**).

### 1. Publish

```powershell
$v = "<Version>"  # e.g. 1.4.1 (matches <Version> in Pulsar.csproj)
$root = "E:\8_Project\10_C#\Pulsar_Project"  # repo root
Remove-Item "$root\Artifacts\publish\v$v\*" -Recurse -Force -ErrorAction SilentlyContinue
dotnet publish Pulsar/Pulsar/Pulsar.csproj -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:PublishReadyToRun=true `
  -p:PublishDir="$root\Artifacts\publish\v$v\"
```

**Output structure** — `Artifacts\publish\v{Version}\`:

| Entry | Purpose |
|-------|---------|
| `Pulsar.exe` | Self-contained single-file app (ReadyToRun) |
| `Pulsar.pdb` | Debug symbols |
| `*_cor3.dll` | Native WPF runtime libs (**required** — do NOT strip; without them the self-contained app won't launch) |
| `Assets/` | Tutorial steps + demo scripts (copied from project resources) |

### 2. Package

Zip the **folder contents** (not the folder itself), matching existing artifacts:

```powershell
Compress-Archive -Path "Artifacts\publish\v$v\*" `
  -DestinationPath "Artifacts\Pulsar-v$v.zip" -CompressionLevel Optimal -Force
```

**Result** — `Artifacts\Pulsar-v{Version}.zip` (e.g. `Artifacts\Pulsar-v1.4.1.zip`),
flat structure with `Pulsar.exe`, `Pulsar.pdb`, the `*_cor3.dll` runtime libs and
`Assets/`.

> ⚠️ The `*_cor3.dll` native libs are part of the self-contained WPF single-file
> output and must ship alongside `Pulsar.exe`. Earlier artifacts omitted them and
> the published build could not start.

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
