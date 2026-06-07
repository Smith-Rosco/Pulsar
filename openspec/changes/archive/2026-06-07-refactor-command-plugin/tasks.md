## 1. Naming Normalization & Cleanup

- [x] 1.1 Delete `SimpleCommandPlugin.Refactored.cs`
- [x] 1.2 Rename `SimpleCommandPlugin.cs` → `CommandPlugin.cs`, class `SimpleCommandPlugin` → `CommandPlugin`, namespace `Pulsar.Plugins.Extensions.BasicCommand` → `Pulsar.Plugins.Extensions.Command`
- [x] 1.3 Rename `SimpleCommandSettings.cs` → `CommandPluginSettings.cs`, class `SimpleCommandSettings` → `CommandPluginSettings`
- [x] 1.4 Rename directory `Plugins/Extensions/BasicCommand/` → `Plugins/Extensions/Command/`
- [x] 1.5 Update `App.xaml.cs` DI registration from `SimpleCommandPlugin` to `CommandPlugin`
- [x] 1.6 Run `dotnet build Pulsar/Pulsar/Pulsar.csproj` to verify naming changes compile

## 2. Key Parsing Abstraction (command-key-parsing spec)

- [x] 2.1 Create `Core/Plugin/KeyInstruction.cs` — discriminated record types (`TextInstruction`, `KeyPressInstruction`, `KeyCombinationInstruction`)
- [x] 2.2 Create `Plugins/Extensions/Command/KeysLexer.cs` — implement `KeysLexer.Parse(string keys) → IReadOnlyList<KeyInstruction>` by extracting tokenization logic from `ParseAndSendKeys`
- [x] 2.3 Create `Core/Plugin/IKeySender.cs` — interface with `SendText(string)`, `SendKeyCombination(params ushort[])`, `GetNamedKey(string)`
- [x] 2.4 Create `Plugins/Extensions/Command/KeySender.cs` — `KeySender : IKeySender` delegating to `InputHelper`
- [x] 2.5 Register `IKeySender` → `KeySender` as transient in `App.xaml.cs`
- [x] 2.6 Rewrite `CommandPlugin.SendKeysAsync` to use `KeysLexer.Parse()` + `IKeySender.Execute()` with `CancellationToken` support
- [x] 2.7 Run `dotnet build Pulsar/Pulsar/Pulsar.csproj` to verify key parsing compiles

## 3. Process Launching Abstraction (command-process-launch spec)

- [x] 3.1 Create `Core/Plugin/IProcessLauncher.cs` — interface with `void Launch(ProcessStartInfo info)`
- [x] 3.2 Create `Plugins/Extensions/Command/ProcessLauncher.cs` — `ProcessLauncher : IProcessLauncher` delegating to `Process.Start`
- [x] 3.3 Register `IProcessLauncher` → `ProcessLauncher` as transient in `App.xaml.cs`
- [x] 3.4 Update `CommandPlugin.RunCommandAsync` to use `IProcessLauncher` via constructor injection
- [x] 3.5 Run `dotnet build Pulsar/Pulsar/Pulsar.csproj` to verify process launching compiles

## 4. Metadata Extraction

- [x] 4.1 Create `Plugins/Extensions/Command/CommandPluginMetadata.cs` — static `Create(...)` method containing the extracted `GetMetadata()` body
- [x] 4.2 Update `CommandPlugin.GetMetadata()` to delegate to `CommandPluginMetadata.Create(...)`
- [x] 4.3 Run `dotnet build Pulsar/Pulsar/Pulsar.csproj` to verify metadata compiles

## 5. Localization Integration (localization-infrastructure spec)

- [x] 5.1 Inject `ILocalizationService` into `CommandPlugin` constructor
- [x] 5.2 Add `Plugin.Command.*` resource keys to `Resources/Strings.resx` (English)
- [x] 5.3 Add `Plugin.Command.*` resource keys to `Resources/Strings.zh-CN.resx` (Chinese)
- [x] 5.4 Replace all hardcoded strings in `CommandPlugin` (error messages, success messages, unknown action) with `_loc["..."]` calls
- [x] 5.5 Replace metadata label/description string literals in `CommandPluginMetadata` with localized strings where applicable
- [x] 5.6 Run `dotnet build Pulsar/Pulsar/Pulsar.csproj` to verify localization compiles

## 6. Tests

- [x] 6.1 Create `Pulsar.Tests/Plugins/Command/KeysLexerTests.cs` — test all tokenization scenarios from `command-key-parsing` spec
- [x] 6.2 Create `Pulsar.Tests/Plugins/Command/CommandPluginTests.cs` — test `RunCommandAsync` and `SendKeysAsync` with mocked `IProcessLauncher`, `IKeySender`, and `ILocalizationService`
- [x] 6.3 Update `Pulsar.Tests/Plugins/Core/BuiltInPluginMetadataTests.cs` — update `CommandRunner` test to reference `CommandPlugin`
- [x] 6.4 Run `dotnet test Pulsar/Pulsar.Tests/Pulsar.Tests.csproj` to verify all tests pass

## 7. Final Verification

- [x] 7.1 Run `dotnet build Pulsar/Pulsar/Pulsar.csproj` for final compilation check
- [x] 7.2 Run `dotnet test Pulsar/Pulsar.Tests/Pulsar.Tests.csproj` for all tests passing
- [x] 7.3 Verify plugin still appears in slot editor with same actions (`run`, `sendkeys`) and parameters
