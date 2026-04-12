## 1. Selection Contract

- [x] 1.1 Replace the thin `WindowSelectionContext` model with explicit selection request/result types that capture switching intent, skip behavior, and decision metadata.
- [x] 1.2 Refactor the shared selection helper into a dedicated `WindowSelectionEngine` that ranks candidates with explicit activation, Z-order, and stable-order semantics.
- [x] 1.3 Update `IWindowService` and immediate callers to consume the new selection request/result contract without changing end-user `activate` / `switch` / `launch` intent semantics.

## 2. Service Boundaries

- [x] 2.1 Extract window enumeration and candidate-building responsibilities into a `WindowInventoryService` or equivalent delegated component.
- [x] 2.2 Extract activation history, previous-window state, and registry ownership into a `WindowTrackingService` or equivalent delegated component.
- [x] 2.3 Extract shared foreground-switch logic into a `WindowActivator` that returns explicit activation outcomes and is used by all user-facing switch flows.
- [x] 2.4 Convert `WindowService` into a facade that delegates to the new switching components while preserving existing external integration points during migration.

## 3. Quick Switch And Menu Semantics

- [x] 3.1 Extract quick-switch history, pair management, and timeout logic into a dedicated `QuickSwitchEngine` with parity-preserving behavior.
- [x] 3.2 Route quick-switch target resolution and activation through the shared selection and activation paths.
- [x] 3.3 Update submenu coordination to use explicit stable display ordering for presentation and the new selection contract for default target choice.

## 4. Behavior Contract And Documentation

- [x] 4.1 Decide and implement the product semantics for discovery blacklist versus activation denylist handling in WinSwitcher.
- [x] 4.2 Update WinSwitcher settings text and documentation so `ExcludeProcesses` behavior matches runtime behavior and spec language.
- [x] 4.3 Update refactoring and lesson docs to describe explicit ordering semantics, selection reasoning, and the new switching architecture.

## 5. Verification

- [x] 5.1 Add decision-focused unit tests for process activation, grouped switching, submenu default selection, tracked versus untracked candidates, and explicit skip behaviors.
- [x] 5.2 Add parity-focused tests for quick-switch pair behavior, timeout handling, and fallback behavior when tracked windows become invalid.
- [ ] 5.3 Run the relevant test suite and `dotnet build Pulsar/Pulsar/Pulsar.csproj` to verify the refactor compiles and preserves expected switching behavior.
