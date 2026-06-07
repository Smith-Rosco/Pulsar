## Context

The `SimpleCommandPlugin` (ID: `com.pulsar.command`) is an extension-tier plugin providing two actions: `run` (process/file launch) and `sendkeys` (keyboard sequence injection). It was recently migrated from Service Locator to `PluginBase<T>` + constructor injection, but the internal execution logic remains architecturally flawed: direct static calls to `InputHelper` and `Process.Start`, no cancellation support, hardcoded English strings, and zero execution-level tests.

This design addresses all quality gaps identified in the plugin review while maintaining full wire-protocol backward compatibility (same metadata shape, same `ExecuteAsync` signature, same settings contract).

## Goals / Non-Goals

**Goals:**
- Make `ParseAndSendKeys` testable by splitting into a pure `KeysLexer` (tokenizer) and injectable `IKeySender` (executor)
- Abstract `Process.Start` behind `IProcessLauncher` for mock verifiability
- Normalize plugin naming: `SimpleCommandPlugin` → `CommandPlugin`, drop `Refactored.cs` dead code
- Extract `GetMetadata()` into `CommandPluginMetadata` for single-responsibility
- Inject `ILocalizationService` and migrate all 12+ user-facing strings to `Strings.resx`
- Propagate `CancellationToken` through the key-sending pipeline
- Add xUnit tests covering tokenizer output and both execution paths (with mocks)

**Non-Goals:**
- No changes to the public `ExecuteAsync` signature or metadata contract
- No changes to settings schema (`defaultDelay` integer)
- No new actions or parameters
- No changes to `InputHelper` itself (low-level native wrapper)
- No extraction to a separate C# project — in-process plugin only

## Decisions

### 1. KeysLexer: Pure function returning `IReadOnlyList<KeyInstruction>`

**Decision**: Extract tokenization into a static/stateless `KeysLexer.Parse(string keys)` that returns an `IReadOnlyList<KeyInstruction>`. The `KeyInstruction` is a discriminated union (closures via subtypes or a record struct with a tag enum).

```
KeyInstruction → TextInstruction(string text) | KeyPressInstruction(ushort vk) | KeyCombinationInstruction(IReadOnlyList<ushort> modifiers)
```

**Rationale**: The current 80-line method mixes tokenization and execution. Separating them makes tokenization trivially testable (`Assert.Equal(expectedInstructions, KeysLexer.Parse("^c{ENTER}"))`) and lets us inject the executor. This follows the "Isolate Side-Effects" rule from AI Programming Triangle.

**Alternative considered**: Keep tokenization inline but accept an `Action<KeyInstruction>` callback. Rejected because it doesn't enable easy assertion on the full instruction stream.

### 2. `IKeySender` interface wrapping `InputHelper`

**Decision**: Define `IKeySender` with methods `SendText(string)`, `SendKeyCombination(params ushort[])`, and `GetNamedKey(string)`. Register `KeySender` as a transient implementation that delegates to `InputHelper`.

**Rationale**: Allows tests to mock key sending and verify the correct instruction sequence was dispatched. Follows the same pattern as existing `IPkiExecutionService` for PKI.

### 3. `IProcessLauncher` interface wrapping `Process.Start`

**Decision**: Define `IProcessLauncher` with a single `void Launch(ProcessStartInfo startInfo)` method. Register `ProcessLauncher` as transient.

**Rationale**: Enables tests to verify the correct `FileName`, `Arguments`, and `WorkingDirectory` are set without actually spawning processes. Also provides a seam for future timeout/exit-code tracking.

### 4. Naming normalization: `CommandPlugin` in `Pulsar.Plugins.Extensions.Command`

**Decision**: Rename the class from `SimpleCommandPlugin` to `CommandPlugin`, the directory from `BasicCommand/` to `Command/`, the namespace to `Pulsar.Plugins.Extensions.Command`, and the settings class to `CommandPluginSettings`.

**Rationale**: User's explicit request to normalize naming. The "Simple" prefix was a legacy qualifier that no longer applies. `Command` is cleaner and matches the plugin's canonical ID (`com.pulsar.command`).

### 5. Metadata extraction pattern

**Decision**: Move `GetMetadata()` body into a static method `CommandPluginMetadata.Create(string id, ...)` that the plugin calls. Not a separate DI service — just a data factory.

**Rationale**: Reduces the plugin file from ~430 lines to ~200 lines without adding DI complexity. The metadata is pure data with no dependencies; a static factory is sufficient. Future enhancement could use attribute-driven metadata generation.

### 6. Localization key naming convention

**Decision**: Use the prefix `Plugin.Command.*` for resource keys. Error messages get keys like `Plugin.Command.Error.ExecutionFailed`; metadata labels use the existing convention (`SlotParam.*`, `SlotAction.*`).

**Rationale**: Consistent with existing module-prefix convention (`Settings.*`, `SlotParam.*`, `SlotAction.*`). Existing convention-based localization for slot metadata still works; we add explicit `_loc["..."]` calls for runtime strings.

### 7. Delete `.Refactored.cs` entirely

**Decision**: Remove `SimpleCommandPlugin.Refactored.cs` from the codebase. Not archive — delete.

**Rationale**: It is dead code in the production source path with identical `ParseAndSendKeys` logic to the main plugin. It serves as documentation at best, but AGENTS.md already references it. Historical value is preserved in git history.

## Risks / Trade-offs

- **[Risk] Plugin ID change?** → We keep `Id = "com.pulsar.command"` unchanged. Only class/namespace/display change — no profile migration needed.
- **[Risk] `InputHelper` still has static methods** → `IKeySender` isolates all call sites; `InputHelper` itself is not refactored. This is an acceptable first step.
- **[Risk] Rename breaks existing DI registration references** → `App.xaml.cs` reference will be updated atomically in the same change.
- **[Risk] Loc strings reference wrong keys in tests** → Tests will inject `ILocalizationService` mock or use real `LocalizationService` with hardcoded resource dictionary for deterministic assertions.
