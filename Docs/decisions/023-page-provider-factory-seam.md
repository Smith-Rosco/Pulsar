# ADR-023: Page Provider Factory Seam (Remove Service Locator from the Menu Execution Path)

**Status**: Accepted (implemented 2026-09-05)
**Date**: 2026-09-05
**Deciders**: Pulsar Development Team
**Related**: architecture review 2026-09-05 (candidate 1, "Strong"); ADR-008 (Menu Session refactor); ADR-010 (two-adapter seam principle)

---

## Context

The menu execution path resolved dependencies through an `IServiceProvider` service locator passed from `MenuSession` into the page providers:

- `MenuSession` constructor took `System.IServiceProvider` and called `GetService` 3× (for `IDebugStatePublisher`, `ISubMenuLayoutEngine`, `IEnumerable<ISubMenuStrategy>`), then passed the container to both page providers.
- `CommandPageProvider` called `GetService` 7× (config, executor, feedback, localization, usage tracker, feedback presenter, plus a second config resolution for `CreateProfileStrategy`).
- `ProcessPageProvider` called `GetService` 9× (config, localization, usage tracker, health monitor, log service, tray, executor, feedback, feedback presenter).
- `CreateProfileStrategy` called `GetRequiredService<SettingsWindow>()` 1×.

**Total: 20 `GetService`/`GetRequiredService` calls across 4 files**, every one resolving a fixed singleton known at composition time — nothing was dynamic.

Consequences:

- **Zero tests for page providers**: constructing them required a configured `IServiceProvider`, so tests never exercised `CommandPageProvider` or `ProcessPageProvider` directly.
- **Wiring errors surfaced only in production**: tests used `Mock.Of<IServiceProvider>()` (returning null for every service), so a missing DI registration would never fail a test.
- **Duplicated construction**: `CommandPageProvider` was constructed inline in `MenuSession` at two sites (`LoadPageContentAsync`, `RebuildPageProviderAsync`), including the "Add Profile" creator-slot insertion logic. The two copies had already diverged (`foundProfile` vs `HasCreatorSlot()` conditions).
- **Interface lied**: `MenuSession`'s constructor advertised `IServiceProvider` (the whole container) instead of the 3 collaborators it actually needed.

## Decision

Introduce an `IPageProviderFactory` seam and eliminate the service locator from the menu execution path entirely.

1. **`IPageProviderFactory`** (`ViewModels/Strategies/IPageProviderFactory.cs`): two methods — `CreateCommandPage(slots, context)` and `CreateProcessPage(config, context, seededWindows)`. Returns `IPageProvider` so tests can substitute fakes.
2. **`PageProviderFactory`** (`ViewModels/Strategies/PageProviderFactory.cs`): production implementation holding all 13 fixed singleton dependencies. Constructor-injected from DI.
3. **Page providers take explicit dependencies**: `CommandPageProvider` and `ProcessPageProvider` constructors now take every dependency as a typed parameter (no `IServiceProvider`). `CreateProfileStrategy` takes `Func<SettingsWindow>` instead of `GetRequiredService`.
4. **`MenuSession` constructor**: replaces `IServiceProvider` with `IPageProviderFactory`; the 3 optional collaborators (`IDebugStatePublisher`, `ISubMenuLayoutEngine`, `IEnumerable<ISubMenuStrategy>`) become explicit nullable ctor params. `IPluginRegistry` is removed (it was only passed through to `CommandPageProvider`; the factory now holds it).
5. **Creator-slot helper**: the duplicated "Add Profile" slot construction is extracted to `MenuSession.InsertCreatorSlot(slots, context)`, used by both call sites.
6. **DI registration**: `IPageProviderFactory` registered as singleton in `App.xaml.cs`; `Func<SettingsWindow>` registered as `sp => () => sp.GetRequiredService<SettingsWindow>()` (transient window, avoids captive dependency).

### Seam justification (ADR-010 principle)

The factory seam is real because it has two adapters: the production `PageProviderFactory` and test fakes/mocks. Every test that constructs `MenuSession` now supplies an `IPageProviderFactory` — either a mock returning no-op providers, or one returning real providers with test-scoped dependencies.

## Considered Options

- **Keep `IServiceProvider`**: rejected — the interface was the whole container, not the needed slice; tests couldn't exercise page providers; wiring errors hid until production.
- **`Func<CommandPageProvider>` / `Func<ProcessPageProvider>` factories**: rejected — the providers need per-call args (slots, context, config, seeded windows), so a plain Func doesn't work; a named factory interface is clearer and testable.
- **Register page providers as transient and inject them directly**: rejected — they need per-session data that changes every summon; direct injection would require property mutation or a separate "initialize" step.
- **Move creator-slot logic into the factory**: rejected — the two call sites have different trigger conditions (`!foundProfile` vs `existingCp.HasCreatorSlot()`); the factory shouldn't know about "previous provider state." A private helper in `MenuSession` is the right locality.

## Consequences

Positive:
- **Interface is the test surface**: page providers are now directly constructible with typed fakes; the first page-provider tests can be written without a container.
- **Wiring errors surface at composition time**: missing registrations cause DI resolution failures at startup, not null-reference crashes mid-execution.
- **Locality**: the 20 `GetService` calls collapse into 13 constructor parameters on the factory; the duplicate creator-slot construction is fixed once, fixed everywhere.
- **`MenuSession` ctor shrinks by 1** (`IPluginRegistry` removed) and gains honesty (3 optional collaborators are explicit instead of hidden in the container).
- **No ADR conflicts**: deepens ADR-008 (Menu Session stays the deep module; only the construction seam narrows); follows ADR-010 (two-adapter seam principle).

Negative / trade-offs:
- `PageProviderFactory` has 13 constructor parameters — a wide composition module. This is acceptable because its job is explicitly to wire providers; it has no logic beyond construction. It is registered as a singleton and never tested directly (tests mock the interface).
- 9 test files required updates (10 `new MenuSession` call sites) to pass `IPageProviderFactory` and (where needed) `subMenuStrategies` explicitly. This is a one-time cost.

Risk:
- `CreateProfileStrategy` now throws `InvalidOperationException` if `Func<SettingsWindow>` is null (previously `GetRequiredService` would throw a DI exception). Equivalent failure mode, clearer message.

---

**Change History**:
- v1.0.0 (2026-09-05): Initial version. Implemented alongside architecture review candidate 1.
