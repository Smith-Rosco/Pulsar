## Context

The current PKI implementation delivers working credential injection, but it remains architecturally concentrated in `Pulsar/Pulsar/Plugins/Core/Pki/PkiPlugin.cs`. The plugin still owns argument validation, secret lookup, decryption, launcher hiding, focus restoration, timing delays, and keystroke sequencing in one class. This is out of step with the repository's newer plugin architecture, where plugins move toward `PluginBase<T>`, constructor injection, and thinner execution adapters.

Recent work improved testability by introducing `IInputSimulator` and `IWindowFocusSimulator`, but the refactoring report in `Docs/architecture/pki/PKI_REFACTORING_AND_BUGS.md` shows that the boundary is still too shallow. The runtime is testable only at the orchestration edge, while the actual execution policy remains embedded in plugin code. At the same time, PKI-related secret handling has spread into `SettingsViewModel`, `QuickSecretsViewModel`, `SecretPickerViewModel`, and helper code, with duplicated service implementations already present under both `Plugins/Core/Pki/Services/` and `Features/Pki/Services/`.

This change affects a core plugin, a security-sensitive path, and multiple consuming modules. The design must preserve current slot configuration and SendKeys-first runtime behavior while establishing explicit boundaries that allow safer evolution and deeper automated validation.

## Goals / Non-Goals

**Goals:**
- Separate PKI plugin protocol handling from PKI runtime business logic.
- Establish explicit contracts for secret storage, secret protection, focus restoration, and credential injection execution.
- Model PKI execution as a validated application flow that can be tested independently of Windows-specific adapters.
- Consolidate duplicated PKI services and provide a single source of truth for runtime and settings-related secret operations.
- Keep existing slot/action contracts (`fill`, `inject`, `secretId`, `autoEnter`) compatible while improving internal structure.
- Align documentation and tests with the actual SendKeys-first injection policy.

**Non-Goals:**
- Redesign the user-facing secret dialogs or slot editor UX beyond dependency changes needed for PKI service consolidation.
- Change persisted `secretId` storage format or require a profile migration.
- Reintroduce UIA as the default PKI injection path.
- Add external secret managers or cloud-backed credential storage in this change.

## Decisions

### 1. Convert `PkiPlugin` into a thin adapter over an application service
`PkiPlugin` will inherit from `PluginBase<PkiPlugin>` and delegate runtime work to an injected `IPkiExecutionService`. The plugin remains responsible for Pulsar-facing metadata, action dispatch, and compatibility aliases, but not for secret retrieval or execution sequencing.

Why this approach:
- It aligns PKI with the repository's modern plugin architecture and removes the remaining service-locator pattern.
- It shrinks the blast radius of plugin-level changes in a core security path.
- It allows most PKI behavior to be tested as application logic instead of through plugin initialization side effects.

Alternatives considered:
- Keep `Initialize(IServiceProvider)` and only extract helper methods: rejected because it preserves the central coupling and does not improve dependency clarity.
- Move all behavior into `PluginBase<T>` hooks: rejected because PKI-specific logic belongs in PKI services, not in generic plugin infrastructure.

### 2. Introduce layered PKI contracts across domain, application, and infrastructure boundaries
The PKI module will expose contracts such as `IPkiSecretStore`, `ISecretProtector`, `IInjectionExecutor`, and `IFocusRestorer`. Application services such as `PkiExecutionService`, `SecretLookupService`, and `InjectionPlanBuilder` will consume those contracts. Windows-specific classes such as SendKeys writers and foreground-window adapters remain in infrastructure.

Why this approach:
- It isolates side effects in infrastructure while keeping business rules in testable services.
- It creates clean seams for future changes such as alternate encryption backends, storage versioning, or richer execution diagnostics.
- It eliminates direct coupling between view models, plugin code, and concrete storage classes.

Alternatives considered:
- Keep the current service classes and only add interfaces around them: rejected because current classes still mix technical concerns and domain orchestration.
- Split only storage from plugin logic but leave execution sequencing in `PkiPlugin`: rejected because the most brittle behavior is the injection workflow itself.

### 3. Represent PKI execution as a deterministic injection plan
Credential fill operations will be translated from slot arguments and context into a validated `InjectionRequest`, then into an `InjectionPlan` made of explicit steps such as hide launcher, restore focus, wait, type account, press tab, type password, and optionally press enter. An `IInjectionExecutor` will execute the plan.

Why this approach:
- It removes imperative scripting logic from the plugin class and makes execution policy inspectable in tests.
- It provides a single location for timing, sequencing, and future telemetry decisions.
- It allows tests to verify both plan generation and execution behavior separately.

Alternatives considered:
- Keep sequencing inline and verify mocks more deeply: rejected because it still couples validation, plan generation, and side effects in the same method.
- Model the sequence as a raw list of delegates: rejected because it is harder to inspect, serialize for diagnostics, or reason about in tests.

### 4. Standardize on SendKeys-first multi-field execution for PKI
The PKI runtime architecture will codify the current SendKeys-first approach for account/password injection and treat UIA-based writing as infrastructure that may remain available for non-PKI use cases but not as the default PKI execution strategy.

Why this approach:
- The existing bug triage document already established that UIA `SetValue` semantics are incompatible with rapid multi-field PKI injection.
- Encoding this as an architectural invariant prevents future regressions caused by partial reintroduction of UIA into the PKI path.
- It ensures docs, tests, and runtime behavior describe the same execution policy.

Alternatives considered:
- Keep UIA-first as a configurable PKI option: rejected because it reopens the exact timing and overwrite failures already documented.
- Remove UIA infrastructure entirely: rejected because the current change is about PKI architecture, not broad text-input platform cleanup.

### 5. Consolidate secret storage and protection into a shared PKI service layer
Secret persistence and encryption will have one authoritative implementation used by both runtime and settings flows. Existing duplicate classes under `Plugins/Core/Pki/Services/` and `Features/Pki/Services/` will be converged behind shared contracts. UI-facing flows such as `QuickSecretsViewModel` and `SecretPickerViewModel` will consume the shared services instead of constructing crypto helpers directly.

Why this approach:
- It prevents behavior drift between runtime secret usage and settings secret editing.
- It removes duplicated maintenance burden and ambiguity about which service implementation is authoritative.
- It creates a stable seam for future storage metadata such as schema versioning, audit fields, or pending-edit resolution.

Alternatives considered:
- Leave duplicated services in place and only update the plugin path: rejected because the duplication is already a source of architectural drift.
- Move all PKI service logic into Settings-specific view models: rejected because secret storage is a shared domain concern, not a UI concern.

### 6. Upgrade PKI validation coverage from plugin-edge tests to layered tests
Tests will expand from current plugin initialization and parameter checks to cover request validation, secret lookup outcomes, plan construction, execution sequencing, repository behavior, and compatibility alias handling. Existing plugin tests remain, but they become a thin adapter verification layer.

Why this approach:
- The current tests do not validate the most failure-prone parts of PKI execution.
- Layered tests match the new design boundaries and give faster diagnosis when regressions occur.
- This satisfies the repository's AI-first guidance to isolate side effects and verify ViewModel or service state headlessly.

Alternatives considered:
- Keep only end-to-end simulator coverage: rejected because failures would be harder to localize and slower to iterate on.

## Risks / Trade-offs

- [Core plugin refactor can introduce runtime regressions in a security-critical path] -> Mitigate with compatibility-preserving phases, adapter-level tests, and targeted simulator validation for `com.pulsar.pki` before merge.
- [Service consolidation may ripple into settings dialogs and secret-picker flows] -> Mitigate by preserving current UI contracts and introducing new abstractions behind existing view-model method signatures first.
- [Added domain/application layers increase short-term complexity] -> Mitigate by keeping the public API minimal and only extracting boundaries that map to existing pain points.
- [Documentation and code may diverge again if execution policy changes informally] -> Mitigate by making SendKeys-first behavior an explicit requirement/spec and updating architecture docs in the same change.
- [Storage refactor could accidentally alter file format or pending-secret behavior] -> Mitigate by preserving persisted payload shape, testing pending + persisted merge semantics, and deferring schema migration to a future change unless explicitly needed.

## Migration Plan

- Introduce the new PKI contracts and application services alongside current implementation.
- Migrate `PkiPlugin` to constructor injection and delegate execution to the new application service while preserving existing action names and argument semantics.
- Move secret storage/protection consumers in settings and dialog flows onto the shared service layer without changing UI behavior.
- Remove duplicated PKI service implementations once all consumers depend on the unified contracts.
- Update docs and expand tests, then validate with targeted PKI unit tests and simulator/build checks.
- Rollback strategy: revert the plugin adapter and service registrations to the pre-change implementation; persisted slot and secret data remain compatible because storage format does not change.

## Open Questions

- Should the unified secret store introduce a versioned envelope now, or should versioning be deferred until a storage-format change is actually required?
- Do we want structured PKI-specific error codes in `PluginResult` now, or should that remain an internal PKI execution result mapped back to today's string-based plugin contract?
- Should secret metadata resolution remain reusable from a general helper/service outside the PKI folder, or be pulled fully into the PKI module as part of this consolidation?
