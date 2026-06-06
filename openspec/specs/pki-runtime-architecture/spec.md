# pki-runtime-architecture

## Purpose
Define the PKI runtime execution model, validation flow, and failure boundaries for credential fill operations.
## Requirements
### Requirement: PKI plugin delegates runtime execution to a dedicated service
The PKI plugin SHALL act as a thin adapter that preserves Pulsar plugin metadata and action compatibility while delegating credential-fill execution to a dedicated PKI application service.

#### Scenario: Fill action uses application service
- **WHEN** Pulsar executes `com.pulsar.pki` with the `fill` action
- **THEN** the plugin delegates the request to the PKI execution service instead of directly loading secrets, decrypting data, or performing keystroke injection inside the plugin class

#### Scenario: Inject alias remains supported
- **WHEN** Pulsar executes `com.pulsar.pki` with the legacy `inject` action
- **THEN** the plugin maps the request to the same PKI execution flow used by `fill`

### Requirement: PKI execution flow is expressed as a validated request and deterministic plan
The PKI runtime SHALL translate slot arguments and `PulsarContext` into a validated execution request and a deterministic injection plan before side effects occur.

#### Scenario: Missing secret identifier is rejected before execution
- **WHEN** the PKI runtime receives a fill request without a valid `secretId`
- **THEN** it returns a recoverable error result before attempting secret lookup, focus restoration, or input injection

#### Scenario: Valid request produces ordered execution steps
- **WHEN** the PKI runtime receives a valid secret reference and target window context
- **THEN** it produces an ordered plan that includes hiding the launcher, restoring focus, waiting for stabilization, injecting the account when present, injecting the password, and optionally pressing Enter

### Requirement: PKI runtime treats SendKeys-first multi-field injection as the supported execution policy
The PKI runtime MUST use the SendKeys-based text injection path as the supported policy for multi-field credential injection and SHALL NOT depend on UIA-first execution for the PKI credential-fill path.

#### Scenario: Account and password fill use SendKeys-based execution
- **WHEN** a secret contains both account and password values
- **THEN** the PKI runtime injects the account, sends Tab, and injects the password through the SendKeys-capable execution path

#### Scenario: Password-only secret skips account step
- **WHEN** a secret contains no account value
- **THEN** the PKI runtime omits the account and Tab steps and injects only the password

### Requirement: PKI runtime surfaces structured execution outcomes across failure boundaries
The PKI runtime SHALL distinguish validation, secret lookup, decryption, focus restoration, and injection-execution failures so tests and logs can identify the failing stage without exposing plaintext secrets.

#### Scenario: Secret lookup failure stops execution before focus change
- **WHEN** the requested secret does not exist in the secret store
- **THEN** the PKI runtime returns an error result and does not hide the launcher or attempt input injection

#### Scenario: Injection execution failure is reported without leaking secret material
- **WHEN** the injection executor throws during credential fill
- **THEN** the PKI runtime returns an error result that identifies the execution stage without including plaintext account or password values in logs or messages

### Requirement: PKI focus restoration SHALL use IFocusManager with verification
The PKI injection executor SHALL restore focus to the target window through `IFocusManager.ActivateWindowAsync()` with `VerifyAfterActivation = true` rather than through the removed `IFocusRestorer`/`IWindowFocusSimulator` chain. Focus verification SHALL be performed before any credential text is injected.

#### Scenario: PKI RestoreFocus step uses IFocusManager
- **WHEN** the PKI injection plan reaches the `RestoreFocus` step
- **THEN** the executor SHALL call `IFocusManager.ActivateWindowAsync(step.TargetWindowHandle)` with verification enabled

#### Scenario: PKI injection aborts if focus verification fails
- **WHEN** `IFocusManager.ActivateWindowAsync` returns a failed verification result during PKI injection
- **THEN** the executor SHALL return `PkiExecutionResult.Fail(PkiExecutionStage.FocusRestore, ...)` and SHALL NOT proceed to inject credentials

#### Scenario: IFocusRestorer and IWindowFocusSimulator are removed
- **WHEN** the PKI subsystem resolves its focus dependencies
- **THEN** it SHALL depend on `IFocusManager` directly, and the `IFocusRestorer` and `IWindowFocusSimulator` interfaces and their implementations SHALL be removed from the codebase

