## Context

Pulsar documents a clean plugin model where the core application captures context, dispatches tasks, and renders feedback. In implementation, however, `PluginRegistry` has accumulated far more responsibility: plugin descriptor registration, activation, config application, enable/disable state management, circuit-breaker state, lifecycle hook invocation, execution dispatch, usage tracking, health monitoring, and user notifications. `PluginRegistryV2` introduces additional concepts such as `PluginHost`, permission interception, version resolution, and hot reload, but it does so as a second large orchestrator instead of completing the underlying separation of concerns.

The result is architectural drift. Runtime semantics such as whether load implies enable, when settings are applied relative to lifecycle hooks, and where breaker state lives are no longer owned by one clear abstraction. This makes the plugin platform harder to reason about, harder to test in isolation, and harder to extend safely for future isolation or runtime-management features.

## Goals / Non-Goals

**Goals:**
- Establish a stable plugin runtime kernel with explicit boundaries for discovery, runtime state, execution policy, and instance hosting.
- Define one authoritative lifecycle state model for plugin load, enable, disable, execute, fault, and unload transitions.
- Make execution rules such as availability checks, circuit breaking, activation, exception mapping, and telemetry composable and independently testable.
- Preserve existing plugin-facing contracts such as `IPulsarPlugin`, plugin IDs, action dispatch, and persisted plugin configuration.
- Create a runtime shape that can absorb existing `PluginHost` and future capabilities such as permissions or hot reload without reintroducing a giant registry class.

**Non-Goals:**
- Redesign plugin authoring contracts beyond the minimal internal changes needed to support the new runtime kernel.
- Change persisted plugin profile format or slot configuration semantics.
- Deliver full hot reload, permission UX, or new external plugin packaging formats as part of this change.
- Rewrite all monitoring or settings UI flows unless required to consume the new runtime abstractions.

## Decisions

### 1. Split registry responsibilities into catalog, runtime, execution pipeline, and host layers
The new internal architecture will separate four concerns:
- `PluginCatalog`: discovery, descriptors, metadata, dependency ordering
- `PluginRuntime`: runtime state, activation orchestration, enable/disable transitions, host/session ownership
- `PluginExecutionPipeline`: execution-time policy ordering and result handling
- `PluginHost`: plugin instance lifecycle and isolation concerns

Why this approach:
- It maps directly to the concerns already present in the code instead of inventing an abstract framework.
- It prevents future features from continuing to inflate `PluginRegistry` or `PluginRegistryV2`.
- It gives each policy domain a narrow, testable surface.

Alternatives considered:
- Keep `PluginRegistry` as the main orchestrator and only extract helper classes: rejected because it preserves the central control bottleneck.
- Replace V1 entirely with `PluginRegistryV2`: rejected because V2 currently adds capabilities but still centralizes too much policy.

### 2. Reduce `PluginRegistry` to a facade over the runtime kernel
`PluginRegistry` may remain as the externally consumed entry point for compatibility, but it will delegate to the new runtime kernel instead of owning policy state directly.

Why this approach:
- It preserves the rest of the application surface while enabling internal restructuring.
- It allows incremental migration without forcing every consumer to change at once.
- It makes the runtime kernel the real architectural unit rather than another versioned registry class.

Alternatives considered:
- Rename everything immediately and remove `PluginRegistry`: rejected because the repository already depends on it in multiple runtime and UI paths.

### 3. Define a single plugin lifecycle state machine and enforce it centrally
Lifecycle semantics will be made explicit with stable states such as `Unloaded`, `Loaded`, `Enabled`, `Running`, `Disabled`, and `Faulted`. Transitions such as load, enable, disable, execute, recover, and unload will be owned by runtime services instead of being implied differently in `PluginRegistry`, `PluginRegistryV2`, and `PluginHost`.

Why this approach:
- Current code contains semantic drift around `OnEnableAsync`, activation timing, and runtime readiness.
- A shared lifecycle model prevents duplicate or skipped lifecycle hooks when multiple execution paths coexist.
- State transitions become test cases rather than incidental behavior.

Alternatives considered:
- Keep lifecycle behavior implicit and document it better: rejected because the problem is divergent runtime behavior, not missing prose.

### 4. Move circuit breaking into a dedicated runtime policy service
Circuit-breaker state and decisions will live in a dedicated breaker policy/state service with an explicit interface for checking availability, recording outcomes, reporting state, and triggering recovery transitions.

Why this approach:
- The current field-based dictionaries inside `PluginRegistry` are not independently testable or observable.
- A dedicated policy object creates a clean seam for future sliding windows, backoff strategies, or persistence.
- UI and diagnostics can consume breaker state without coupling to registry internals.

Alternatives considered:
- Leave breaker state in the registry and extract only helper methods: rejected because the policy would still not be a first-class runtime concern.

### 5. Standardize plugin execution through a deterministic execution pipeline
Every plugin execution will flow through the same ordered stages: resolve descriptor and runtime state, check enablement and breaker availability, ensure activation/host readiness, open execution scope, invoke plugin action, classify result/exception, update breaker state, and emit usage/health telemetry.

Why this approach:
- Today these concerns are partially mixed inside `ExecuteAsync` and partially duplicated elsewhere.
- A deterministic pipeline provides one place to reason about ordering, failures, and instrumentation.
- Future concerns such as permission checks or richer execution diagnostics can be inserted without reshaping the whole runtime.

Alternatives considered:
- Keep direct `plugin.ExecuteAsync()` calls and add more guards around them: rejected because ordering remains implicit and duplicated.

### 6. Treat `PluginHost` as an instance-hosting primitive, not a policy owner
`PluginHost` will remain responsible for instance creation, isolated load context concerns, unload handling, and host-local lifecycle bridging. It will not own circuit-breaking rules, user enable/disable state, or telemetry policy.

Why this approach:
- Host isolation and execution policy evolve at different speeds and have different failure modes.
- Keeping `PluginHost` narrow makes it reusable for both built-in and isolated plugin forms.
- It allows host internals to change without rewriting platform policy.

Alternatives considered:
- Let `PluginHost` absorb most runtime behavior: rejected because it would merely move the giant orchestrator problem to a different class.

## Risks / Trade-offs

- [Runtime refactoring could regress plugin activation or execution ordering] -> Mitigate with compatibility-preserving facade layers and targeted lifecycle/dispatch tests before deleting old paths.
- [Two runtime models may coexist too long during migration] -> Mitigate by defining one canonical lifecycle and routing all new behavior through it first.
- [Additional abstractions can increase short-term code volume] -> Mitigate by extracting only the boundaries that map to current responsibilities rather than introducing speculative infrastructure.
- [Host isolation paths may diverge from built-in plugin paths] -> Mitigate by making the execution pipeline and lifecycle model shared regardless of hosting mode.
- [Telemetry or health data may shift due to unified execution semantics] -> Mitigate by explicitly defining success/failure recording rules and validating report consumers against them.

## Migration Plan

- Introduce runtime-kernel abstractions alongside the existing registry implementation.
- Move descriptor discovery and metadata concerns behind a catalog abstraction.
- Extract circuit-breaker policy and execution-pipeline stages from `PluginRegistry.ExecuteAsync()` while preserving current user-visible behavior.
- Centralize lifecycle transitions and adapt `PluginHost` plus registry compatibility paths to the shared state model.
- Route existing `PluginRegistry` entry points through the new runtime kernel, then retire duplicated policy logic from `PluginRegistryV2` or fold V2-only capabilities into the shared runtime.
- Update docs and expand tests before removing obsolete runtime paths.
- Rollback strategy: keep the compatibility facade and revert DI wiring to the previous registry-owned orchestration if regressions appear before cleanup completes.

## Open Questions

- Should built-in and external plugins share exactly the same host abstraction from day one, or should built-ins use a lighter host adapter during migration?
- Do we want the runtime kernel to expose a queryable runtime snapshot API immediately for settings/diagnostics UI, or defer that until after execution behavior stabilizes?
- Should permission checks become a first-class pipeline stage in this change, or remain an extension point until the permission model is fully specified?
