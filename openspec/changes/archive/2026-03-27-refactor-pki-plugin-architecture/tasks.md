## 1. Establish PKI runtime architecture

- [x] 1.1 Introduce PKI domain and application contracts for execution, secret storage, protection, focus restoration, and injection execution under `Pulsar/Pulsar/Plugins/Core/Pki/`.
- [x] 1.2 Implement a PKI execution service and request/plan model that validates slot arguments and converts them into deterministic injection steps.
- [x] 1.3 Refactor `PkiPlugin` to inherit from `PluginBase<PkiPlugin>`, use constructor injection, preserve `fill`/`inject` compatibility, and delegate runtime work to the PKI execution service.

## 2. Consolidate secret-management services

- [x] 2.1 Converge duplicated PKI repository and crypto implementations into a single shared secret store/protection layer while preserving the current `secrets.json` payload shape.
- [x] 2.2 Update PKI-related view models and helpers such as `QuickSecretsViewModel`, `SecretPickerViewModel`, and settings flows to consume the shared PKI service contracts instead of constructing concrete helpers directly.
- [x] 2.3 Preserve pending-secret and legacy-label resolution behavior behind a unified PKI metadata-resolution service used consistently across runtime and settings flows.

## 3. Harden Windows execution adapters

- [x] 3.1 Rework Windows-specific PKI infrastructure so SendKeys-based multi-field injection is the explicit PKI execution path while keeping adapter responsibilities isolated from application logic.
- [x] 3.2 Ensure focus restoration, launcher hiding, and injection-execution failures are surfaced through stage-specific PKI results without leaking plaintext secret material.

## 4. Expand validation and documentation

- [x] 4.1 Add layered tests for PKI request validation, secret lookup outcomes, decryption failures, injection-plan generation, execution sequencing, and compatibility aliases.
- [x] 4.2 Add storage-focused tests covering secret store compatibility with existing persisted payloads and pending/persisted metadata resolution behavior.
- [x] 4.3 Update `Docs/Plugins/PkiPlugin.md`, `Docs/architecture/INPUT_INJECTION.md`, and PKI architecture notes to reflect the new layering and the SendKeys-first PKI policy.
- [x] 4.4 Run targeted PKI tests plus `dotnet build Pulsar/Pulsar/Pulsar.csproj` and `Pulsar.Simulator` validation for `com.pulsar.pki`, fixing regressions until the refactor is green.
