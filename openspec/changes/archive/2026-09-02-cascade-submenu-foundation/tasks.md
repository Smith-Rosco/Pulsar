## 1. Descriptor & strategy contracts

- [x] 1.1 Add `SubMenuDescriptor` abstract base (StrategyId, IsWindowSwitch, total-slots hints) in `Models/` and verify it compiles
- [x] 1.2 Add `WindowSubMenuDescriptor` (ProcessName, IReadOnlyList<ProcessWindowInfo> Windows) in `Models/` and verify it compiles
- [x] 1.3 Add `CascadeSubMenuDescriptor` placeholder (carries source slot `SubSlots` reference) in `Models/` and verify it compiles — reserved for Change B, no wiring
- [x] 1.4 Add `ISubMenuStrategy` interface (`StrategyId`, `ConfigureSubMenu(SubMenuContext ctx, SubMenuDescriptor descriptor)`) in `ViewModels/Strategies/` and verify it compiles
- [x] 1.5 Add `SubMenuContext` (CenterSlot, Slots, SlotsPerPage, PageIndex) in `ViewModels/Strategies/` and verify it compiles

## 2. WindowSwitchSubMenuStrategy (behavior-preserving extraction)

- [x] 2.1 Create `WindowSwitchSubMenuStrategy` implementing `ISubMenuStrategy` (id `window-switch`) and copy today's `ConfigureSubMenu` body verbatim (center `BackActionStrategy`, child `WindowSwitchStrategy`/`NoOpStrategy`, `SubMenuColorPalette`, `CaptureThumbnailAsync`, `SelectTargetWindow` submenu intent, logging)
- [x] 2.2 Add `WindowSwitchSubMenuStrategyTests` asserting center is `BackActionStrategy`, child windows get `WindowSwitchStrategy`, empty page gets `NoOpStrategy`, palette/thumbnail code paths unchanged
- [x] 2.3 Run `dotnet test Pulsar/Pulsar.Tests/Pulsar.Tests.csproj` and verify window-submenu tests still green before touching the interface

## 3. Coordinator as strategy host

- [x] 3.1 Refactor `RadialMenuSubMenuCoordinator` into a host: constructor-inject `IEnumerable<ISubMenuStrategy>`, expose `ConfigureSubMenu(SubMenuDescriptor, int slotsPerPage, int pageIndex, SlotViewModel centerSlot, ObservableCollection<SlotViewModel> slots)` that routes by `StrategyId`
- [x] 3.2 Add unknown-strategy fallback: log warning + signal root fallback to the session, never throw (spec `submenu-coordinator-strategy`)
- [x] 3.3 Add `SubMenuCoordinatorStrategyTests`: registered-id routing, unknown-id fallback + logged warning, window strategy selected for `WindowSubMenuDescriptor`
- [x] 3.4 Register `WindowSwitchSubMenuStrategy` in `App.xaml.cs` `ConfigureServices` and verify `dotnet build Pulsar/Pulsar/Pulsar.csproj` resolves with 0 errors

## 4. IMenuSession generalization (breaking change)

- [x] 4.1 Change `IMenuSession.EnterSubMenuAsync` signature to `Task EnterSubMenuAsync(SubMenuDescriptor descriptor, int clickedSlotIndex)` and update `RadialMenuViewModel.EnterSubMenuAsync` projection
- [x] 4.2 Update `ProcessGroupStrategy.EnterSubMenuAsync` to build a `WindowSubMenuDescriptor` instead of passing raw lists; verify `ProcessGroupStrategyTests` still green
- [x] 4.3 Update `MenuSession.EnterSubMenuAsyncCore` to consume the descriptor: derive `_subMenuWindows`/`_subMenuProcessName` from `WindowSubMenuDescriptor`, compute pagination from payload, keep the morph/cancel logic unchanged
- [x] 4.4 Update `MenuSession.HandleGlobalMouseClickAsync` drill-in (currently type-checks `ProcessGroupStrategy` + `List<ProcessWindowInfo>`) to route through descriptor construction; preserve direct-switch and cascade-slot behaviors
- [x] 4.5 Verify `dotnet build` succeeds (0 errors) and update any remaining compile-break call sites in tests (`MenuSessionTests`, `GroupedSlotInteractionTests`, `WindowSwitchStrategyTests`)

## 5. SubSlots data model + persistence

- [x] 5.1 Add `SubSlotDescriptor` record (PluginId, Action, Args, Label, IconKey, ColorHex) in `Models/`
- [x] 5.2 Add `ObservableCollection<SubSlotDescriptor> SubSlots` (always present, empty default) to `SlotViewModel`
- [x] 5.3 Add optional `SubActions` (`List<SubSlotDescriptor>?`, camelCase JSON key, null-tolerant) to `PluginSlot` in `ProfilesConfig.cs`
- [x] 5.4 Add round-trip tests: slot with `subActions` persists/restores; legacy slot without the key loads with empty collection; existing slot behavior unchanged
- [x] 5.5 Verify `dotnet build` + `ProfilesConfigDefaultsTests` pass with the new field

## 6. Integration & regression

- [x] 6.1 Run full `dotnet test Pulsar/Pulsar.Tests/Pulsar.Tests.csproj` — window-submenu, gesture, and settings suites green (baseline 380+)
- [x] 6.2 Manual QA (requires human): grouped-process drill-in → submenu identical to pre-change (animation, thumbnails, colors, Back, pagination); both themes
- [x] 6.3 Manual QA (requires human): modifier-release on grouped root slot still direct-switches; root slots without sub-slots behave unchanged
