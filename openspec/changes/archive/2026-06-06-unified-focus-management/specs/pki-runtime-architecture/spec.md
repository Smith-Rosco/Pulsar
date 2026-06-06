## ADDED Requirements

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
