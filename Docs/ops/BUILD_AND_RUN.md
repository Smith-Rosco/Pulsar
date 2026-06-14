# Build and Run Commands

**Status**: Published  
**Scope**: Operations  
**Applies To**: All developers and AI agents  
**Last Updated**: 2026-03-03

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

## Test Commands

**Current Status**: xUnit test project at `Pulsar/Pulsar.Tests/` (331+ tests).

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
