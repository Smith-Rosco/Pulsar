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

**Current Status**: No test projects are currently configured.

### Future Test Setup

If adding tests in the future:

1. Create a new xUnit project:
   ```bash
   dotnet new xunit -o Pulsar/Pulsar.Tests
   ```

2. Add reference:
   ```bash
   dotnet add Pulsar/Pulsar.Tests reference Pulsar/Pulsar/Pulsar.csproj
   ```

3. Run tests:
   ```bash
   dotnet test Pulsar/Pulsar.Tests
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
