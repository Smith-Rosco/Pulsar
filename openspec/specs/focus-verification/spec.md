# focus-verification Specification

## Purpose
TBD - created by archiving change unified-focus-management. Update Purpose after archive.
## Requirements
### Requirement: Focus activation SHALL be verified after completion
After `IFocusManager.ActivateWindowAsync()` completes and reports success, the caller MAY request verification that the foreground window is the expected target. When verification is enabled, the system SHALL confirm that `GetForegroundWindow()` returns the target window handle before declaring activation complete.

#### Scenario: Verification succeeds
- **WHEN** `ActivateWindowAsync` is called with `VerifyAfterActivation = true` and the target window becomes foreground
- **THEN** the returned result SHALL have `VerificationPassed = true` and `ActualForegroundAfterActivation` SHALL match the target handle

#### Scenario: Verification fails on first attempt
- **WHEN** `ActivateWindowAsync` completes but `GetForegroundWindow()` returns a different handle than the target
- **THEN** the system SHALL retry activation up to `MaxRetries` times with a delay of `VerifyDelayMs` between attempts

#### Scenario: Verification fails after all retries
- **WHEN** `ActivateWindowAsync` fails verification after exhausting all retries
- **THEN** the returned result SHALL have `Success = false`, `VerificationPassed = false`, and a `FailureReason` of `ForegroundSwitchFailed`

### Requirement: PKI credential injection SHALL verify focus before injecting
When the PKI injection executor restores focus to the target window before typing credentials, it SHALL request post-activation verification through `IFocusManager` and SHALL abort injection if verification fails.

#### Scenario: Focus verified before credential injection
- **WHEN** the PKI injection plan reaches the `RestoreFocus` step
- **THEN** the executor SHALL call `IFocusManager.ActivateWindowAsync(targetHandle, options with VerifyAfterActivation=true)` and SHALL only proceed to the injection step if verification passes

#### Scenario: PKI injection aborted on verification failure
- **WHEN** focus verification fails after the `RestoreFocus` step in a PKI injection plan
- **THEN** the executor SHALL return a `PkiExecutionResult` with stage `FocusRestore` and SHALL NOT inject any credential text

### Requirement: Activation verification SHALL respect configurable timing parameters
The verification delay and retry count SHALL be configurable via `FocusActivationOptions` to accommodate applications with varying focus-transition latency.

#### Scenario: Custom verification timing
- **WHEN** `ActivateWindowAsync` is called with `VerifyDelayMs = 200` and `MaxRetries = 1`
- **THEN** the system SHALL wait 200ms before the first verification check and retry once if the check fails

#### Scenario: Verification disabled by configuration
- **WHEN** `ActivateWindowAsync` is called with `VerifyAfterActivation = false`
- **THEN** the system SHALL skip all verification steps and return immediately after the activation attempt

