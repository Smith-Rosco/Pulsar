## Why

The codebase has accumulated architectural debt that increases maintenance burden and impacts developer productivity:

- **Composition root bloat**: `App.xaml.cs` contains ~140 lines of DI registrations (75+ services), making it a merge conflict magnet and a single point of ordering sensitivity.
- **God classes**: `ConfigService` is 791 lines handling file I/O, JSON normalization, validation, first-launch logic, and reset behavior. `RadialMenuViewModel` is 982 lines with 15+ direct dependencies.
- **Over-engineered subsystems**: The tutorial system has 25 files with its own orchestrator, trigger engine, spotlight controller, and factory pattern — more infrastructure than the plugin loading system.
- **Hidden static dependencies**: Seven static `Initialize()` calls bypass DI, making testing harder and creating invisible coupling chains.
- **Legacy cruft**: `PluginRegistryV2`, `PluginRepository` (obsolete), and `LegacySlotConverter` are still registered and compiled into every build.
- **Testing friction**: Test and simulator projects are not in the solution file, so `dotnet test` from the root does not find them.

This change applies a focused hygiene pass to improve maintainability without altering user-visible behavior.

## What Changes

- **DI Registration Modules**: Extract service registrations into subsystem-specific extension methods (e.g., `AddPulsarPlugins`, `AddPulsarPki`, `AddPulsarTutorial`, `AddPulsarUi`). Reduce `App.xaml.cs` to ~20 lines of orchestration.
- **ConfigService Refactoring**: Split into `ConfigFileService` (I/O), `ConfigNormalizer` (type coercion), and keep validation separate. Remove temporal coupling (`SetValidationPipeline` hack).
- **Tutorial System Simplification**: Replace the 25-file event-driven trigger system with a flat, linear sequence driven by a single orchestrator. Remove the trigger factory pattern and dedicated `StartupCoordinator`.
- **Static Helpers to DI**: Convert `IconHelper`, `UiaHelper`, `BrowserHelper`, and VBA runner helpers into injectable services registered with the container.
- **Legacy Code Removal**: Delete `PluginRegistryV2`, `PluginRepository`, `LegacySlotConverter`, and all `#pragma warning disable CS0618` suppressions.
- **Solution File Update**: Add `Pulsar.Tests.csproj` and `Pulsar.Simulator.csproj` to `Pulsar.sln`.

No breaking changes. All changes are internal refactorings that preserve existing behavior.

## Capabilities

### New Capabilities
*None* — this change introduces no new user-facing capabilities.

### Modified Capabilities
*None* — all changes are internal refactorings that preserve existing spec-level behavior. The tutorial simplification maintains the same user-visible outcomes (first-run steps, progress persistence, skip behavior). The ConfigService refactoring preserves configuration loading, saving, and reset semantics.

## Impact

**Affected code**:
- `App.xaml.cs` — registration logic moved to extension methods
- `Services/ConfigService.cs` — split into multiple files
- `Services/Tutorial/*` — 25 files simplified/replaced
- `Helpers/IconHelper.cs`, `Helpers/UiaHelper.cs`, `Plugins/Extensions/*/BrowserHelper.cs`, VBA runner helpers — converted to services
- `Services/PluginRegistryV2.cs`, `Services/PluginRepository.cs`, `Core/Converters/LegacySlotConverter.cs` — deleted
- `Pulsar.sln` — updated

**Dependencies**: None. No external APIs or breaking changes.

**Risk**: Low. Each refactoring is self-contained and can be verified by existing tests and the simulator.
