## Why

The PKI plugin is a core security capability, but its current implementation concentrates parameter validation, secret retrieval, decryption, focus management, and keystroke injection inside a single plugin class. That structure has already reached the limits of safe iteration: recent UI and testability improvements exposed duplicated services, shallow test coverage, and documentation drift around the actual injection strategy.

## What Changes

- Refactor the PKI runtime from a monolithic plugin implementation into a layered architecture with a thin plugin adapter, application services, domain contracts, and Windows-specific infrastructure adapters.
- Replace the PKI plugin's remaining service-locator initialization pattern with constructor injection and align it with the repository's modern `PluginBase<T>` plugin pattern.
- Introduce explicit PKI contracts for secret storage, secret protection, focus restoration, and injection execution so runtime behavior can be tested without binding tests to Windows APIs or file-system details.
- Model credential injection as an application-level execution flow built from validated requests and deterministic execution steps instead of embedding the sequence directly in `PkiPlugin`.
- Consolidate duplicated PKI service implementations and establish a single source of truth for secret storage, encryption, and secret-display resolution used by plugin runtime and settings dialogs.
- Update PKI documentation and validation coverage so the published architecture, actual runtime behavior, and automated tests stay in sync.

## Capabilities

### New Capabilities
- `pki-runtime-architecture`: Defines the layered PKI execution architecture, dependency boundaries, execution contracts, and runtime invariants for credential injection.
- `pki-secret-management-services`: Defines shared PKI secret storage, protection, and metadata-resolution service contracts used by runtime and settings flows.

### Modified Capabilities
- None.

## Impact

- Affected runtime code: `Pulsar/Pulsar/Plugins/Core/Pki/`, `Pulsar/Pulsar/App.xaml.cs`, and PKI-related helpers/view models that currently depend on concrete PKI services.
- Affected tests: `Pulsar/Pulsar.Tests/Plugins/Core/Pki/` plus new service-level and repository-level tests for execution flow, storage, and encryption boundaries.
- Affected documentation: `Docs/Plugins/PkiPlugin.md`, `Docs/architecture/INPUT_INJECTION.md`, and PKI architecture notes to reflect the actual SendKeys-first injection path and new layering.
- Affected dependency graph: PKI runtime will depend on explicit interfaces rather than concrete storage and Windows adapter classes, improving testability and future extensibility without changing persisted slot configuration.
