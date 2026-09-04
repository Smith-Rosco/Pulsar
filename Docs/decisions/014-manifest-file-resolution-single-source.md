# ADR-014: Single-Source the Manifest File Resolution and Parse

**Status**: Accepted (2026-09-04)
**Date**: 2026-09-04
**Deciders**: Pulsar Development Team
**Related**: architecture review 2026-09-04 (candidate C, "worth exploring"); ADR-012 (narrow runtime seams — same review)
**Implementation**: `Core/Plugin/Metadata/PluginManifestReader.cs` (new, static), consumers `Core/Plugin/PluginLoader.cs` (`TryReadExternalManifest`), `Services/LocalPluginScanner.cs` (`ScanInstalledPlugins`), `Services/PluginPackageManager.cs` (`HasValidManifest`, `ReadAndValidateManifest`), tests in `Pulsar.Tests/Plugin/PluginManifestReaderTests.cs`

---

## Context

The external-plugin manifest convention — file name `plugin.manifest.json` with a legacy `manifest.json` fallback, plus case-insensitive JSON deserialization — was implemented as **inline copies in four places**:

- `PluginLoader.TryReadExternalManifest` — external discovery.
- `LocalPluginScanner.ScanInstalledPlugins` — installed-package scanning.
- `PluginPackageManager.HasValidManifest` — existence check before install.
- `PluginPackageManager.ReadAndValidateManifest` — pre-install inspection.

Each copy repeated the `Path.Combine` + `File.Exists` probe sequence and constructed its own `new JsonSerializerOptions { PropertyNameCaseInsensitive = true }`. The architecture review (candidate C) flagged this as copy-amplified drift: the convention has no single owner, so adding a third manifest name or tightening parse options would require coordinated edits across four call sites — and any missed site silently diverges (e.g. one path accepting a file another rejects).

The consolidation must *not* smuggle semantics upward: content validation (Id presence, permission tokens, version compatibility) and per-caller failure messages are load-bearing error contracts that differ by call site.

## Decision

1. **New `PluginManifestReader` (static) in `Pulsar.Core.Plugin.Metadata`** is the single source for exactly two invariants:
   - `TryResolveManifestPath(string pluginFolderPath)` — resolve `plugin.manifest.json` first, fall back to `manifest.json`, return `null` when neither exists. File-name constants (`ManifestFileName`, `LegacyManifestFileName`) live here.
   - `Parse(string manifestJson)` — deserialize with `PropertyNameCaseInsensitive = true`; a literal JSON `null` returns `null`; malformed JSON propagates `JsonException`.
2. **All four call sites delegate to the reader.** `PluginLoader` and `LocalPluginScanner` keep their `File.ReadAllText`, so IO errors surface at the same layer as before; `PluginPackageManager` keeps the read inside its existing try block.
3. **Content validation stays at the caller's error layer, byte-for-byte unchanged.** Id-emptiness checks, permission-token validation, version-compatibility checks, and each call site's failure messages (`"Invalid plugin package: manifest.json not found"` etc.) are untouched. The reader explicitly documents that it does *not* validate content.
4. **The reader is static and pure** — no state, no DI registration. It is a formatting/location utility, not a service; registering it would be ceremony.

## Considered Options

- **Keep four inline copies** — rejected: the review's finding is exactly that copy-amplified conventions drift; there is no behavioral payoff to the duplication.
- **Extract an `IManifestReader` service with DI** — rejected: the reader is stateless and parameterized entirely by its input; a container-registered abstraction would add a seam without a second implementation or a test double to justify it. Static method group keeps the utility composable and trivially testable.
- **Move content validation (Id/version/permissions) into the reader too** — rejected: each caller applies different validation with different error contracts (scanner tolerates and logs, package manager rejects the whole package). Unifying them would force callers to translate reader errors into their own messages — net new indirection, no invariant gained.
- **Merge `TryResolveManifestPath` and `Parse` into one `TryReadManifest(folder)` that also does IO** — rejected: callers read from different abstractions (`File.ReadAllText` in their own scopes) and want to distinguish "no manifest file" from "manifest malformed"; splitting locate/parse keeps those failure modes separable at each call site.

## Consequences

- **The manifest-file convention now has one owner.** Adding a third manifest name or changing parse options is a one-file edit; every consumer follows automatically.
- **Callers keep their error semantics.** Four call sites still decide their own Id/validation/failure handling; the diff removed duplicated mechanics without altering any behavior or message.
- **Test surface is small and direct**: `PluginManifestReaderTests` (6 cases) pins preferred-name resolution, legacy fallback, no-file → `null`, case-insensitive property mapping, literal-`null` → `null`, and malformed JSON → `JsonException`. Full suite: 1002/1002 (996 prior + 6); `dotnet build` at 0 warnings / 0 errors.
- **No behavioral drift risk beyond the moved lines** — resolution order, fallback semantics, and deserialization options are identical to the pre-change inline code.

---

**Change History**:
- v1.0.0 (2026-09-04): Initial version — implements architecture-review candidate C (manifest file-name resolution + case-insensitive parse duplicated across `PluginLoader` / `LocalPluginScanner` / `PluginPackageManager`; consolidated into `PluginManifestReader`).
