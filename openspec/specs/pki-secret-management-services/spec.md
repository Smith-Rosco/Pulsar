# pki-secret-management-services

## Purpose
Define the shared Secret Fill secret storage, protection, and metadata contracts used by runtime and settings flows.

## Requirements

### Requirement: Secret Fill secret storage and protection are exposed through shared contracts
The Secret Fill module SHALL expose shared contracts for secret persistence and secret protection so runtime, settings, and dialog flows use the same authoritative implementations.

#### Scenario: Runtime and settings resolve the same secret store contract
- **WHEN** the Secret Fill plugin runtime and secret-management view models request secret storage access
- **THEN** they depend on the same Secret Fill secret store contract rather than separate duplicated repository implementations

#### Scenario: Runtime and settings resolve the same protection contract
- **WHEN** a secret is encrypted during editing and decrypted during runtime execution
- **THEN** both operations use the same Secret Fill protection contract rather than constructing independent crypto helpers ad hoc

### Requirement: Shared Secret Fill services preserve existing secret payload compatibility
The unified Secret Fill secret service layer MUST preserve compatibility with the existing persisted secret payload shape and slot references unless an explicit migration is introduced.

#### Scenario: Existing secret identifiers remain valid after refactor
- **WHEN** the refactored Secret Fill runtime loads previously saved slot configuration that contains `secretId`
- **THEN** it resolves the same stored secret without requiring changes to slot arguments or profile structure

#### Scenario: Existing secrets file remains readable
- **WHEN** the refactored Secret Fill secret store loads the current `secrets.json`
- **THEN** it can read persisted secret payloads without requiring manual conversion by the user

### Requirement: Secret Fill secret metadata resolution supports both persisted and pending edits
The Secret Fill secret-management service layer SHALL provide a shared metadata-resolution path that can merge persisted secrets and pending in-memory edits for settings and picker flows.

#### Scenario: Secret picker shows pending edits consistently
- **WHEN** a user creates or edits a secret in settings before saving all configuration changes
- **THEN** Secret Fill metadata resolution returns the pending label and account values for selection and display

#### Scenario: Missing label falls back without altering slot identity
- **WHEN** a secret payload lacks a stored label but a legacy label mapping exists
- **THEN** Secret Fill metadata resolution uses the legacy display label without mutating the stored `secretId`

### Requirement: Secret Fill service abstractions support deeper automated validation
The Secret Fill secret-management service layer SHALL allow storage, protection, and metadata behaviors to be tested independently from the plugin adapter and Windows-specific injection code. Protection failures SHALL be logged before returning empty results to enable field diagnostics.

#### Scenario: Secret store behavior can be tested without plugin initialization
- **WHEN** automated tests exercise secret load and save behavior
- **THEN** they can validate the Secret Fill secret store contract without instantiating the plugin class or Windows input adapters

#### Scenario: Protection failures are testable at the service boundary
- **WHEN** automated tests simulate decryption failure
- **THEN** they can verify the Secret Fill protection contract's failure behavior without invoking focus restoration or keystroke injection

#### Scenario: Decryption failure is logged with exception details
- **WHEN** `CredentialsManager.Decrypt` catches an exception during `ProtectedData.Unprotect`
- **THEN** it logs the exception type and message via `ILogger` before returning an empty string

#### Scenario: Decryption failure does not expose plaintext or ciphertext
- **WHEN** `CredentialsManager.Decrypt` logs a decryption failure
- **THEN** the log entry does not contain the plaintext password or the encrypted Base64 data

### Requirement: SecretRepository retry logic handles all failure paths correctly
The `SecretRepository` SHALL handle I/O contention retries without unreachable code or silent return-on-failure. After exhausting all retry attempts for `LoadAsync`, the method SHALL rethrow the last caught exception. After exhausting all retry attempts for `SaveAsync`, the method SHALL rethrow the last caught exception rather than returning silently.

#### Scenario: LoadAsync rethrows after all retries exhausted
- **WHEN** `SecretRepository.LoadAsync` encounters an IOException on all three retry attempts
- **THEN** the method rethrows the last IOException rather than falling through to a default return value

#### Scenario: SaveAsync rethrows after all retries exhausted
- **WHEN** `SecretRepository.SaveAsync` encounters an IOException on all three retry attempts
- **THEN** the method rethrows the last IOException rather than returning silently

#### Scenario: LoadAsync returns empty dictionary when file does not exist
- **WHEN** `SecretRepository.LoadAsync` is called and the `secrets.json` file does not exist
- **THEN** the method returns an empty `Dictionary<Guid, SecretPayload>` without attempting retries
