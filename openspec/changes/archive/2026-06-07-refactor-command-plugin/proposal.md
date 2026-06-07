## Why

The SimpleCommand plugin (`com.pulsar.command`) has been migrated to `PluginBase<T>` + constructor injection, but its internal architecture remains a monolithic, untestable implementation: an 80-line `ParseAndSendKeys` method with deeply nested logic, zero execution-level tests, hardcoded English strings violating localization rules, and direct OS side-effect calls bypassing DI. This refactoring brings the plugin up to Pulsar's AI Programming Triangle standards (mockable side-effects, testable logic, headless verifiability).

## What Changes

- **BREAKING**: Rename class from `SimpleCommandPlugin` to `CommandPlugin`; namespace from `Pulsar.Plugins.Extensions.BasicCommand` to `Pulsar.Plugins.Extensions.Command`
- **BREAKING**: Delete `SimpleCommandPlugin.Refactored.cs` — dead reference code in production path
- Split `ParseAndSendKeys` into `KeysLexer` (pure tokenizer) + `KeyExecutor` (injected `IKeySender` interface)
- Introduce `IProcessLauncher` to wrap `Process.Start`, enabling mock verification in tests
- Extract `GetMetadata()` into `CommandPluginMetadata` for single-responsibility adherence
- Inject `ILocalizationService`; migrate all 12+ hardcoded user-facing strings to `Strings.resx`
- Propagate `CancellationToken` through the key-sending pipeline
- Add comprehensive xUnit tests for tokenizer output and plugin execution paths
- Update DI registration in `App.xaml.cs`

## Capabilities

### New Capabilities

- `command-key-parsing`: Tokenize SendKeys-style strings into a typed `KeyInstruction` sequence without side-effects, enabling pure unit testing of the parser.
- `command-process-launch`: Abstract `Process.Start` behind `IProcessLauncher` for testable, mockable command execution.

### Modified Capabilities

- `localization-infrastructure`: Command plugin user-facing strings (error messages, success notifications, metadata labels/descriptions) are now sourced from `Strings.resx` via `ILocalizationService`.

## Impact

- **Affected code**: `SimpleCommandPlugin.cs` → `CommandPlugin.cs`, `SimpleCommandSettings.cs` → `CommandPluginSettings.cs`, `SimpleCommandPlugin.Refactored.cs` (deleted), `BuiltInPluginMetadataTests.cs`, `App.xaml.cs` (DI registration)
- **New files**: `KeysLexer.cs`, `KeyInstruction.cs`, `IKeySender.cs`, `KeySender.cs`, `IProcessLauncher.cs`, `ProcessLauncher.cs`, `CommandPluginMetadata.cs`, `CommandPluginTests.cs`
- **Resources**: `Strings.resx` and `Strings.zh-CN.resx` — add ~15 new entries
- **No API changes**: Plugin wire protocol (`ExecuteAsync` signature, metadata shape, settings contract) remains backward-compatible
