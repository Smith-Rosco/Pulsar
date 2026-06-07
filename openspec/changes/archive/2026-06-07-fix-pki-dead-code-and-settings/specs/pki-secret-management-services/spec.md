# pki-secret-management-services (delta)

## MODIFIED Requirements

### Requirement: PKI service abstractions support deeper automated validation
The PKI secret-management service layer SHALL allow storage, protection, and metadata behaviors to be tested independently from the plugin adapter and Windows-specific injection code. Protection failures SHALL be logged before returning empty results to enable field diagnostics.

#### Scenario: Secret store behavior can be tested without plugin initialization
- **WHEN** automated tests exercise secret load and save behavior
- **THEN** they can validate the PKI secret store contract without instantiating the plugin class or Windows input adapters

#### Scenario: Protection failures are testable at the service boundary
- **WHEN** automated tests simulate decryption failure
- **THEN** they can verify the PKI protection contract's failure behavior without invoking focus restoration or keystroke injection

#### Scenario: Decryption failure is logged with exception details
- **WHEN** `CredentialsManager.Decrypt` catches an exception during `ProtectedData.Unprotect`
- **THEN** it logs the exception type and message via `ILogger` before returning an empty string

#### Scenario: Decryption failure does not expose plaintext or ciphertext
- **WHEN** `CredentialsManager.Decrypt` logs a decryption failure
- **THEN** the log entry does not contain the plaintext password or the encrypted Base64 data

## ADDED Requirements

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
