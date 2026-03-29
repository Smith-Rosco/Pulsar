# pki-secret-management-services

## Purpose
Define the shared PKI secret storage, protection, and metadata contracts used by runtime and settings flows.

## Requirements

### Requirement: PKI secret storage and protection are exposed through shared contracts
The PKI module SHALL expose shared contracts for secret persistence and secret protection so runtime, settings, and dialog flows use the same authoritative implementations.

#### Scenario: Runtime and settings resolve the same secret store contract
- **WHEN** the PKI plugin runtime and secret-management view models request secret storage access
- **THEN** they depend on the same PKI secret store contract rather than separate duplicated repository implementations

#### Scenario: Runtime and settings resolve the same protection contract
- **WHEN** a secret is encrypted during editing and decrypted during runtime execution
- **THEN** both operations use the same PKI protection contract rather than constructing independent crypto helpers ad hoc

### Requirement: Shared PKI services preserve existing secret payload compatibility
The unified PKI secret service layer MUST preserve compatibility with the existing persisted secret payload shape and slot references unless an explicit migration is introduced.

#### Scenario: Existing secret identifiers remain valid after refactor
- **WHEN** the refactored PKI runtime loads previously saved slot configuration that contains `secretId`
- **THEN** it resolves the same stored secret without requiring changes to slot arguments or profile structure

#### Scenario: Existing secrets file remains readable
- **WHEN** the refactored PKI secret store loads the current `secrets.json`
- **THEN** it can read persisted secret payloads without requiring manual conversion by the user

### Requirement: PKI secret metadata resolution supports both persisted and pending edits
The PKI secret-management service layer SHALL provide a shared metadata-resolution path that can merge persisted secrets and pending in-memory edits for settings and picker flows.

#### Scenario: Secret picker shows pending edits consistently
- **WHEN** a user creates or edits a secret in settings before saving all configuration changes
- **THEN** PKI metadata resolution returns the pending label and account values for selection and display

#### Scenario: Missing label falls back without altering slot identity
- **WHEN** a secret payload lacks a stored label but a legacy label mapping exists
- **THEN** PKI metadata resolution uses the legacy display label without mutating the stored `secretId`

### Requirement: PKI service abstractions support deeper automated validation
The PKI secret-management service layer SHALL allow storage, protection, and metadata behaviors to be tested independently from the plugin adapter and Windows-specific injection code.

#### Scenario: Secret store behavior can be tested without plugin initialization
- **WHEN** automated tests exercise secret load and save behavior
- **THEN** they can validate the PKI secret store contract without instantiating the plugin class or Windows input adapters

#### Scenario: Protection failures are testable at the service boundary
- **WHEN** automated tests simulate decryption failure
- **THEN** they can verify the PKI protection contract's failure behavior without invoking focus restoration or keystroke injection
