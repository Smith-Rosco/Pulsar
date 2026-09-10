# pki-runtime-architecture

## Purpose
Define the Secret Fill runtime execution model, validation flow, and failure boundaries for credential fill operations.
## Requirements
### Requirement: Secret Fill plugin delegates runtime execution to a dedicated service
The Secret Fill plugin SHALL act as a thin adapter that preserves Pulsar plugin metadata and action compatibility while delegating credential-fill execution to a dedicated Secret Fill application service.

#### Scenario: Fill action uses application service
- **WHEN** Pulsar executes `com.pulsar.pki` with the `fill` action
- **THEN** the plugin delegates the request to the Secret Fill execution service instead of directly loading secrets, decrypting data, or performing keystroke injection inside the plugin class

#### Scenario: Inject alias remains supported
- **WHEN** Pulsar executes `com.pulsar.pki` with the legacy `inject` action
- **THEN** the plugin maps the request to the same Secret Fill execution flow used by `fill`

### Requirement: Secret Fill execution flow is expressed as a validated request and deterministic plan
The Secret Fill runtime SHALL translate slot arguments and `PulsarContext` into a validated execution request and a deterministic injection plan before side effects occur.

#### Scenario: Missing secret identifier is rejected before execution
- **WHEN** the Secret Fill runtime receives a fill request without a valid `secretId`
- **THEN** it returns a recoverable error result before attempting secret lookup, focus restoration, or input injection

#### Scenario: Valid request produces ordered execution steps
- **WHEN** the Secret Fill runtime receives a valid secret reference and target window context
- **THEN** it produces an ordered plan that includes hiding the launcher, restoring focus, waiting for stabilization, injecting the account when present, injecting the password, and optionally pressing Enter

### Requirement: Secret Fill runtime treats SendKeys-first multi-field injection as the supported execution policy
The Secret Fill runtime MUST use the SendKeys-based text injection path as the supported policy for multi-field credential injection. The Secret Fill runtime SHALL NOT include, register, or depend on any UIA-first text injection pathway (`IUiaTextWriter`, `WindowsUiaTextWriter`, `IInputSimulator`, `WindowsInputSimulator`). The `useUiaFirst` setting SHALL be removed from the plugin schema and settings model.

#### Scenario: Account and password fill use SendKeys-based execution
- **WHEN** a secret contains both account and password values
- **THEN** the Secret Fill runtime injects the account, sends Tab, and injects the password through the SendKeys-capable execution path via `ISendKeysWriter`

#### Scenario: Password-only secret skips account step
- **WHEN** a secret contains no account value
- **THEN** the Secret Fill runtime omits the account and Tab steps and injects only the password

#### Scenario: No UIA-first types are registered in DI
- **WHEN** the application starts up and configures services
- **THEN** no `IInputSimulator`, `WindowsInputSimulator`, `IUiaTextWriter`, or `WindowsUiaTextWriter` types are registered in the DI container

#### Scenario: useUiaFirst setting does not exist
- **WHEN** a user or profile references plugin settings for `com.pulsar.pki`
- **THEN** the `useUiaFirst` property does not appear in the settings schema, metadata, or `SecretFillPluginSettings` model

### Requirement: Secret Fill runtime surfaces structured execution outcomes across failure boundaries
The Secret Fill runtime SHALL distinguish validation, secret lookup, decryption, focus restoration, and injection-execution failures so tests and logs can identify the failing stage without exposing plaintext secrets.

#### Scenario: Secret lookup failure stops execution before focus change
- **WHEN** the requested secret does not exist in the secret store
- **THEN** the Secret Fill runtime returns an error result and does not hide the launcher or attempt input injection

#### Scenario: Injection execution failure is reported without leaking secret material
- **WHEN** the injection executor throws during credential fill
- **THEN** the Secret Fill runtime returns an error result that identifies the execution stage without including plaintext account or password values in logs or messages

### Requirement: Secret Fill focus restoration SHALL use IFocusManager with verification
The Secret Fill injection executor SHALL restore focus to the target window through `IFocusManager.ActivateWindowAsync()` with `VerifyAfterActivation = true` rather than through the removed `IFocusRestorer`/`IWindowFocusSimulator` chain. Focus verification SHALL be performed before any credential text is injected.

#### Scenario: Secret Fill RestoreFocus step uses IFocusManager
- **WHEN** the Secret Fill injection plan reaches the `RestoreFocus` step
- **THEN** the executor SHALL call `IFocusManager.ActivateWindowAsync(step.TargetWindowHandle)` with verification enabled

#### Scenario: Secret Fill injection aborts if focus verification fails
- **WHEN** `IFocusManager.ActivateWindowAsync` returns a failed verification result during Secret Fill injection
- **THEN** the executor SHALL return `SecretFillExecutionResult.Fail(SecretFillExecutionStage.FocusRestore, ...)` and SHALL NOT proceed to inject credentials

#### Scenario: IFocusRestorer and IWindowFocusSimulator are removed
- **WHEN** the Secret Fill subsystem resolves its focus dependencies
- **THEN** it SHALL depend on `IFocusManager` directly, and the `IFocusRestorer` and `IWindowFocusSimulator` interfaces and their implementations SHALL be removed from the codebase

### Requirement: Secret Fill injection plan respects injectionDelay setting
The Secret Fill runtime SHALL use the `injectionDelay` argument (milliseconds, 0–1000) as the inter-step delay between keystroke operations (account→TAB, TAB→password, password→ENTER) instead of a hardcoded value. The initial focus-stabilization delay (100ms after RestoreFocus) SHALL remain independent of `injectionDelay`.

#### Scenario: injectionDelay controls keystroke gap timing
- **WHEN** the Secret Fill runtime builds an injection plan with `injectionDelay` set to 200
- **THEN** each `Delay` step between `SendText` and `SendKey` steps uses 200 milliseconds

#### Scenario: Default injectionDelay applies when not specified
- **WHEN** the Secret Fill runtime builds an injection plan without an explicit `injectionDelay` value
- **THEN** the inter-keystroke delay defaults to 50 milliseconds

#### Scenario: injectionDelay zero means no delay between keystrokes
- **WHEN** the Secret Fill runtime builds an injection plan with `injectionDelay` set to 0
- **THEN** no `Delay` steps are inserted between keystroke operations

### Requirement: SendKey steps route through ISendKeysWriter abstraction
The `SendKeysInjectionExecutor` SHALL route `SendKey` injection steps through the `ISendKeysWriter` interface rather than calling static `InputHelper` methods directly. The `ISendKeysWriter` interface SHALL expose a `SendKeyCombination(string key)` method.

#### Scenario: SendKey step calls ISendKeysWriter.SendKeyCombination
- **WHEN** the injection plan reaches a `SendKey` step with value `{TAB}`
- **THEN** the executor calls `ISendKeysWriter.SendKeyCombination("{TAB}")` instead of calling `InputHelper.GetNamedKey` or `InputHelper.SendKeyCombination` directly

### Requirement: Secret Fill injection execution has an overall timeout
The `SendKeysInjectionExecutor` SHALL enforce an overall timeout for the injection sequence (default 15 seconds). If the sequence exceeds the timeout, the executor SHALL abort and return a failure result.

#### Scenario: Injection sequence completes within timeout
- **WHEN** the injection sequence completes all steps within the timeout period
- **THEN** the executor returns `SecretFillExecutionResult.Ok` with the completed plan

#### Scenario: Injection sequence aborts on timeout
- **WHEN** the injection sequence exceeds the timeout due to a hung focus operation or stalled delay
- **THEN** the executor returns `SecretFillExecutionResult.Fail(SecretFillExecutionStage.Injection, ...)` and the CancellationToken is cancelled

### Requirement: ISendKeysWriter method naming reflects actual behavior
The `ISendKeysWriter` method `EscapeForSendKeys` SHALL be renamed to `SanitizeInput` to accurately reflect that the underlying injection mechanism uses Unicode key events (`KEYEVENTF_UNICODE`) rather than .NET SendKeys format parsing.

#### Scenario: SanitizeInput does not perform SendKeys escaping
- **WHEN** `SanitizeInput` is called with a string containing SendKeys special characters (`+`, `^`, `%`, `~`, `(`, `)`, `{`, `}`)
- **THEN** the method returns the input unchanged (because `InputHelper.SendText` uses Unicode key events)

