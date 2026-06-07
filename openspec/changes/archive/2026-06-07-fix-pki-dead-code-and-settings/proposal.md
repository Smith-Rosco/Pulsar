## Why

Deep inspection of the PKI plugin revealed that `IInputSimulator`/`WindowsInputSimulator` is dead code (DI-registered but never invoked), `injectionDelay` and `useUiaFirst` settings are accepted but silently ignored by the execution path, `CredentialsManager.Decrypt` swallows all exceptions without logging, and the `SecretRepository` retry logic contains a silent-save-failure path. This cleans up dead weight and closes the gap between declared settings schema and actual runtime behavior.

## What Changes

- Remove `IInputSimulator` / `WindowsInputSimulator` / `IUiaTextWriter` / `WindowsUiaTextWriter` dead code and DI registrations (spec already mandates SendKeys-first policy; UIA-first code is unused)
- Remove `useUiaFirst` setting from `PkiPlugin` schema, `PkiPluginSettings`, and metadata (was never consumed)
- Wire `injectionDelay` from args into `BuildPlan` so it controls inter-step delay instead of hardcoded 10ms
- Route `SendKey` steps through `ISendKeysWriter` instead of bypassing the abstraction with static `InputHelper` calls
- Rename `EscapeForSendKeys` → `SanitizeInput` to reflect that no SendKeys-format escaping is needed (underlying API uses Unicode key events)
- Add exception logging to `CredentialsManager.Decrypt` catch block
- Fix `SecretRepository` retry bug: `LoadAsync` unreachable return after throw; `SaveAsync` silent failure after all retries exhausted
- Add configurable overall timeout to `SendKeysInjectionExecutor.ExecuteAsync` (default 15s)
- **BREAKING**: `useUiaFirst` setting removed from plugin schema — existing profiles referencing it will silently drop the setting (no runtime effect since it was never consumed)

## Capabilities

### Modified Capabilities
- `pki-runtime-architecture`: Remove UIA-first execution pathway residue; wire injectionDelay into plan building; route SendKey through DI abstraction; add execution timeout
- `pki-secret-management-services`: Add logging to CredentialsManager.Decrypt; fix SecretRepository retry logic silent-failure paths

## Impact

- `Pulsar/Pulsar/Plugins/Core/Pki/Services/Input/WindowsInputSimulator.cs` — DELETE
- `Pulsar/Pulsar/Plugins/Core/Pki/Services/Input/IInputSimulator.cs` — DELETE
- `Pulsar/Pulsar/Plugins/Core/Pki/Services/Input/WindowsUiaTextWriter.cs` — DELETE
- `Pulsar/Pulsar/Plugins/Core/Pki/Services/Input/IUiaTextWriter.cs` — DELETE
- `Pulsar/Pulsar.Tests/Plugins/Core/Pki/WindowsInputSimulatorTests.cs` — DELETE
- `Pulsar/Pulsar/App.xaml.cs` — remove `IInputSimulator` and `IUiaTextWriter` DI registrations
- `Pulsar/Pulsar.Simulator/Program.cs` — remove `IInputSimulator` and `IUiaTextWriter` DI registrations
- `Pulsar/Pulsar/Plugins/Core/Pki/PkiPlugin.cs` — remove `useUiaFirst` setting; ensure `injectionDelay` flows through
- `Pulsar/Pulsar/Plugins/Core/Pki/Models/PkiPluginSettings.cs` — remove `UseUiaFirst` property
- `Pulsar/Pulsar/Plugins/Core/Pki/Services/PkiExecutionService.cs` — wire `injectionDelay` into `BuildPlan`
- `Pulsar/Pulsar/Plugins/Core/Pki/Services/SendKeysInjectionExecutor.cs` — route `SendKey` through `ISendKeysWriter`; add timeout
- `Pulsar/Pulsar/Plugins/Core/Pki/Services/Input/WindowsSendKeysWriter.cs` — rename `EscapeForSendKeys`; add `SendKeyCombination`
- `Pulsar/Pulsar/Plugins/Core/Pki/Services/Input/ISendKeysWriter.cs` — rename method; add `SendKeyCombination`
- `Pulsar/Pulsar/Plugins/Core/Pki/Services/CredentialsManager.cs` — add logging to Decrypt catch
- `Pulsar/Pulsar/Plugins/Core/Pki/Services/SecretRepository.cs` — fix retry logic dead code and silent failure
