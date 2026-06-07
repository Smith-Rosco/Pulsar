# pki-runtime-architecture (delta)

## MODIFIED Requirements

### Requirement: PKI runtime treats SendKeys-first multi-field injection as the supported execution policy
The PKI runtime MUST use the SendKeys-based text injection path as the supported policy for multi-field credential injection. The PKI runtime SHALL NOT include, register, or depend on any UIA-first text injection pathway (`IUiaTextWriter`, `WindowsUiaTextWriter`, `IInputSimulator`, `WindowsInputSimulator`). The `useUiaFirst` setting SHALL be removed from the plugin schema and settings model.

#### Scenario: Account and password fill use SendKeys-based execution
- **WHEN** a secret contains both account and password values
- **THEN** the PKI runtime injects the account, sends Tab, and injects the password through the SendKeys-capable execution path via `ISendKeysWriter`

#### Scenario: Password-only secret skips account step
- **WHEN** a secret contains no account value
- **THEN** the PKI runtime omits the account and Tab steps and injects only the password

#### Scenario: No UIA-first types are registered in DI
- **WHEN** the application starts up and configures services
- **THEN** no `IInputSimulator`, `WindowsInputSimulator`, `IUiaTextWriter`, or `WindowsUiaTextWriter` types are registered in the DI container

#### Scenario: useUiaFirst setting does not exist
- **WHEN** a user or profile references plugin settings for `com.pulsar.pki`
- **THEN** the `useUiaFirst` property does not appear in the settings schema, metadata, or `PkiPluginSettings` model

## ADDED Requirements

### Requirement: PKI injection plan respects injectionDelay setting
The PKI runtime SHALL use the `injectionDelay` argument (milliseconds, 0–1000) as the inter-step delay between keystroke operations (account→TAB, TAB→password, password→ENTER) instead of a hardcoded value. The initial focus-stabilization delay (100ms after RestoreFocus) SHALL remain independent of `injectionDelay`.

#### Scenario: injectionDelay controls keystroke gap timing
- **WHEN** the PKI runtime builds an injection plan with `injectionDelay` set to 200
- **THEN** each `Delay` step between `SendText` and `SendKey` steps uses 200 milliseconds

#### Scenario: Default injectionDelay applies when not specified
- **WHEN** the PKI runtime builds an injection plan without an explicit `injectionDelay` value
- **THEN** the inter-keystroke delay defaults to 50 milliseconds

#### Scenario: injectionDelay zero means no delay between keystrokes
- **WHEN** the PKI runtime builds an injection plan with `injectionDelay` set to 0
- **THEN** no `Delay` steps are inserted between keystroke operations

### Requirement: SendKey steps route through ISendKeysWriter abstraction
The `SendKeysInjectionExecutor` SHALL route `SendKey` injection steps through the `ISendKeysWriter` interface rather than calling static `InputHelper` methods directly. The `ISendKeysWriter` interface SHALL expose a `SendKeyCombination(string key)` method.

#### Scenario: SendKey step calls ISendKeysWriter.SendKeyCombination
- **WHEN** the injection plan reaches a `SendKey` step with value `{TAB}`
- **THEN** the executor calls `ISendKeysWriter.SendKeyCombination("{TAB}")` instead of calling `InputHelper.GetNamedKey` or `InputHelper.SendKeyCombination` directly

#### Scenario: SendKeyCombination delegate can be mocked in tests
- **WHEN** a unit test configures a mock `ISendKeysWriter`
- **THEN** the test can verify that `SendKeyCombination` was called with the expected key string

### Requirement: PKI injection execution has an overall timeout
The `SendKeysInjectionExecutor` SHALL enforce an overall timeout for the injection sequence (default 15 seconds). If the sequence exceeds the timeout, the executor SHALL abort and return a failure result.

#### Scenario: Injection sequence completes within timeout
- **WHEN** the injection sequence completes all steps within the timeout period
- **THEN** the executor returns `PkiExecutionResult.Ok` with the completed plan

#### Scenario: Injection sequence aborts on timeout
- **WHEN** the injection sequence exceeds the timeout due to a hung focus operation or stalled delay
- **THEN** the executor returns `PkiExecutionResult.Fail(PkiExecutionStage.Injection, ...)` and the CancellationToken is cancelled

### Requirement: ISendKeysWriter method naming reflects actual behavior
The `ISendKeysWriter` method `EscapeForSendKeys` SHALL be renamed to `SanitizeInput` to accurately reflect that the underlying injection mechanism uses Unicode key events (`KEYEVENTF_UNICODE`) rather than .NET SendKeys format parsing.

#### Scenario: SanitizeInput does not perform SendKeys escaping
- **WHEN** `SanitizeInput` is called with a string containing SendKeys special characters (`+`, `^`, `%`, `~`, `(`, `)`, `{`, `}`)
- **THEN** the method returns the input unchanged (because `InputHelper.SendText` uses Unicode key events)

## REMOVED Requirements

### Requirement: UIA-first text injection pathway
**Reason**: The `IInputSimulator` / `WindowsInputSimulator` / `IUiaTextWriter` / `WindowsUiaTextWriter` types were registered in DI but never invoked by any production code path. The spec already mandates SendKeys-first as the supported policy.
**Migration**: No migration needed. These types had no production callers. Remove DI registrations and delete the source files.
