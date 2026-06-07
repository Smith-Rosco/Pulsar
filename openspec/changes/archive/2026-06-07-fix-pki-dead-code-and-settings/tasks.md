## 1. Remove dead IInputSimulator / UIA pathway

- [x] 1.1 Delete `WindowsInputSimulator.cs`, `IInputSimulator.cs`, `WindowsUiaTextWriter.cs`, `IUiaTextWriter.cs` from `Plugins/Core/Pki/Services/Input/`
- [x] 1.2 Delete `WindowsInputSimulatorTests.cs` from `Pulsar.Tests/Plugins/Core/Pki/`
- [x] 1.3 Remove `IInputSimulator` and `IUiaTextWriter` DI registrations from `App.xaml.cs`
- [x] 1.4 Remove `IInputSimulator` and `IUiaTextWriter` DI registrations from `Pulsar.Simulator/Program.cs`
- [x] 1.5 Run `dotnet build` to confirm no remaining references

## 2. Remove useUiaFirst setting

- [x] 2.1 Remove `UseUiaFirst` property from `PkiPluginSettings.cs`
- [x] 2.2 Remove `useUiaFirst` from `PkiPlugin.GetMetadata()` schema properties
- [x] 2.3 Remove `useUiaFirst` enrichment logic from `PkiPlugin.ExecuteAsync()`
- [x] 2.4 Remove `useUiaFirst` handling from `PkiPlugin.UpdateSettings()`

## 3. Wire injectionDelay into BuildPlan

- [x] 3.1 Modify `PkiExecutionService.BuildPlan` to accept and use `injectionDelay` parameter (replace hardcoded 10ms with configured delay)
- [x] 3.2 Pass `injectionDelay` from args through `PkiExecutionService.ExecuteAsync` to `BuildPlan`
- [x] 3.3 Update `PkiExecutionServiceTests` to verify `injectionDelay` controls inter-step timing
- [x] 3.4 Default `injectionDelay` to 50ms (to match schema default, replacing hardcoded 10ms)

## 4. Route SendKey through ISendKeysWriter

- [x] 4.1 Add `void SendKeyCombination(string key)` method to `ISendKeysWriter` interface
- [x] 4.2 Implement `SendKeyCombination` in `WindowsSendKeysWriter` (delegate to `InputHelper.SendKeyCombination` or `InputHelper.SendText`)
- [x] 4.3 Rename `EscapeForSendKeys` → `SanitizeInput` in `ISendKeysWriter` and `WindowsSendKeysWriter`
- [x] 4.4 Update `SendKeysInjectionExecutor.ExecuteSendKey` to call `_sendKeysWriter.SendKeyCombination()` instead of static `InputHelper` methods
- [x] 4.5 Update `SendKeysInjectionExecutorTests` to verify `SendKeyCombination` is called via mock
- [x] 4.6 Update existing tests referencing `EscapeForSendKeys` (`WindowsSendKeysWriterTests.cs`, `WindowsInputSimulatorTests.cs` — the latter is deleted in 1.2)

## 5. Add timeout to SendKeysInjectionExecutor

- [x] 5.1 Add timeout field (15s default) to `SendKeysInjectionExecutor`
- [x] 5.2 Create linked `CancellationTokenSource` in `ExecuteAsync` with the timeout
- [x] 5.3 Pass cancellation token to all async operations in the step loop
- [x] 5.4 Add test verifying timeout returns `PkiExecutionStage.Injection` failure

## 6. Fix CredentialsManager logging

- [x] 6.1 Add `ILogger<CredentialsManager>` field (constructor injection)
- [x] 6.2 Log exception type and message in `Decrypt` catch block before returning empty string
- [x] 6.3 Ensure log does NOT include encrypted Base64 or plaintext data

## 7. Fix SecretRepository retry logic

- [x] 7.1 Fix `LoadAsync`: remove unreachable `return new Dictionary<...>()` after throw; ensure throw path is clean
- [x] 7.2 Fix `SaveAsync`: ensure rethrow on final retry failure instead of silent return
- [x] 7.3 Add or update unit tests for retry exhaustion behavior in `SecretRepositoryTests`

## 8. Verification

- [x] 8.1 Run `dotnet build Pulsar/Pulsar/Pulsar.csproj` — must succeed
- [x] 8.2 Run `dotnet test Pulsar/Pulsar.Tests/Pulsar.Tests.csproj` — all PKI tests must pass
- [x] 8.3 Run `dotnet run --project Pulsar/Pulsar.Simulator/Pulsar.Simulator.csproj -- --plugin "com.pulsar.pki"` — simulator must succeed
