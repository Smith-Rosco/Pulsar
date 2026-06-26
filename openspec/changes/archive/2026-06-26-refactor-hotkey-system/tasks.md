## 1. Foundation: Constants and Model

- [x] 1.1 Create `Helpers/HotkeyConstants.cs` with `HotkeyActionIds` (ShowGrid, ShowSwitcher), `HotkeyModifiers` (Control, Shift, Alt, Windows), and `ReservedHotkeys.SystemReserved` array
- [x] 1.2 Add `IsEmpty`, `DisplayText`, and `NormalizedSignature` properties to `HotkeyConfig` in `Models/ProfilesConfig.cs`
- [x] 1.3 Update `HotkeyConfig.ToString()` to delegate to `DisplayText`
- [x] 1.4 Run `dotnet test --filter "FullyQualifiedName~HotkeyConfig"` to verify existing `ToString` tests still pass; update test expectations for new display format
- [x] 1.5 Replace all `"ShowGrid"` and `"ShowSwitcher"` magic strings in production code with `HotkeyActionIds` constants (7+ files: ProfilesConfig.cs, HotkeyService.cs, SettingsViewModel.cs, RadialMenuViewModel.cs, SettingsGeneralPage.xaml.cs)
- [x] 1.6 Replace all modifier name strings (`"Control"`, `"Shift"`, `"Alt"`, `"Windows"`) in `HotkeyService.cs` and `SettingsGeneralPage.xaml.cs` with `HotkeyModifiers` constants

## 2. Service Layer: HotkeyService Refactoring

- [x] 2.1 Add `ILogger<HotkeyService>` constructor parameter to `HotkeyService`; update DI registration in `App.xaml.cs` if needed
- [x] 2.2 Implement `ValidateHotkey(actionId, config)` on `HotkeyService` — check all registered actions for NormalizedSignature equality, excluding self
- [x] 2.3 Add system-reserved key detection logic using `ReservedHotkeys.SystemReserved` in `ValidateHotkey`
- [x] 2.4 Implement `ApplyHotkey(actionId, config)` — update in-memory config + rebuild cache without persistence
- [x] 2.5 Update `UpdateHotkey(actionId, config)` — persist via `_configService.SaveAsync` + rebuild cache (restore from dead code)
- [x] 2.6 Implement `GetAllHotkeys()` — return snapshot of `_config.Settings.Hotkeys`
- [x] 2.7 Remove `InitializeAsync()` default hotkey force-creation (lines 56-65) — trust deserialized config
- [x] 2.8 Fix `RebuildHotkeyCache()` — skip entries where `hotkeyConfig.IsEmpty` is true
- [x] 2.9 Fix `RebuildHotkeyCache()` — replace silent `catch(Exception){}` with `_logger.LogWarning(ex, ...)` including actionId

## 3. UI: HotkeyBox Reusable Control

- [x] 3.1 Create `Views/Controls/HotkeyBox.xaml` — Grid layout with read-only TextBox + hidden conflict badge Border
- [x] 3.2 Create `Views/Controls/HotkeyBox.xaml.cs` — code-behind with `PreviewKeyDown` handler (capture key+modifiers, ignore modifier-only, handle Backspace/Delete/Escape for clear)
- [x] 3.3 Register `Hotkey` and `ValidationResult` and `ActionId` and `PlaceholderText` dependency properties
- [x] 3.4 Implement conflict badge visibility logic — red border + warning text when `ValidationResult` has conflicts or is system-reserved
- [x] 3.5 Wire `GotFocus` → `PauseHotkeys()`, `LostFocus` → `ResumeHotkeys()` via `IHotkeyService`
- [x] 3.6 After each capture/clear, raise a `HotkeyChanged` routed event so parent page can trigger validation

## 4. UI: Settings Page Integration

- [x] 4.1 Add `xmlns:controls="clr-namespace:Pulsar.Views.Controls"` namespace if missing in `SettingsGeneralPage.xaml`
- [x] 4.2 Replace both hotkey TextBox elements with `<controls:HotkeyBox>` bound to `ShowGridHotkey` / `ShowSwitcherHotkey`
- [x] 4.3 Bind `ValidationResult` to new ViewModel properties (`ShowGridHotkeyValidation`, `ShowSwitcherHotkeyValidation`)
- [x] 4.4 Remove `Hotkey_PreviewKeyDown`, `Hotkey_GotFocus`, `Hotkey_LostFocus` event handlers from XAML
- [x] 4.5 Remove corresponding handler methods from `SettingsGeneralPage.xaml.cs` (lines 24-79)

## 5. ViewModel: Hotkey Integration

- [x] 5.1 Fix `ShowGridHotkey` getter — return `GetValueOrDefault` with empty `HotkeyConfig` fallback instead of hardcoded `{Key="Q", Modifiers="Control"}`
- [x] 5.2 Fix `ShowSwitcherHotkey` getter — same fix, return empty fallback
- [x] 5.3 In both hotkey setters, call `_hotkeyService.ApplyHotkey()` with the new value for immediate live update
- [x] 5.4 In both hotkey setters, call `_hotkeyService.ValidateHotkey()` and set the corresponding `ValidationResult` property
- [x] 5.5 Add `[ObservableProperty] HotkeyValidationResult? _showGridHotkeyValidation` field
- [x] 5.6 Add `[ObservableProperty] HotkeyValidationResult? _showSwitcherHotkeyValidation` field
- [x] 5.7 In `Save()` method, after `_configService.SaveAsync(_config)`, call `_hotkeyService.UpdateHotkey()` for both ShowGrid and ShowSwitcher
- [x] 5.8 Remove dead `UpdateHotkey` RelayCommand method or repurpose it

## 6. Validation Pipeline Integration

- [x] 6.1 Add `ValidateHotkeys(ProfilesConfig, ValidationResult)` private method to `ConfigValidationPipeline` as Stage 5
- [x] 6.2 Call `ValidateHotkeys` in `ValidateAsync()` method after Stage 4
- [x] 6.3 Detect duplicate `NormalizedSignature` values across all non-empty hotkey configs; emit warnings with category `"Hotkeys"`

## 7. Localization

- [x] 7.1 Add to `Resources/Strings.resx`: `Settings.General.HotkeyNone` = "(None)", `Settings.General.HotkeyConflict` = "Conflict: already assigned to \"{0}\"", `Settings.General.HotkeyReserved` = "This combination is reserved by Windows", `Settings.General.HotkeyClearTooltip` = "Press Backspace/Delete to clear"
- [x] 7.2 Add to `Resources/Strings.zh-CN.resx` with Chinese translations: "(无)", "冲突：已分配给\"{0}\"", "此组合键为 Windows 系统保留", "按 Backspace/Delete 清除"

## 8. DI Wiring

- [x] 8.1 Ensure `ILogger<HotkeyService>` is resolvable from DI container (verify Serilog registration in `App.xaml.cs`)
- [x] 8.2 Verify `HotkeyService` constructor signature change is compatible with existing `services.AddSingleton<IHotkeyService, HotkeyService>()` registration

## 9. Tests

- [x] 9.1 Create `Pulsar.Tests/Services/HotkeyServiceTests.cs` — test class with `Mock<IConfigService>`, `Mock<ILogger<HotkeyService>>`, and `GlobalKeyboardHook`
- [x] 9.2 Test: `ValidateHotkey_EmptyConfig_ReturnsIsEmpty`
- [x] 9.3 Test: `ValidateHotkey_NoConflict_ReturnsValid`
- [x] 9.4 Test: `ValidateHotkey_DetectsConflict_WithCorrectActionId`
- [x] 9.5 Test: `ValidateHotkey_SelfReference_NotConflict`
- [x] 9.6 Test: `ValidateHotkey_EmptyHotkey_NotInConflicts`
- [x] 9.7 Test: `ValidateHotkey_SystemReserved_ReturnsFlagged`
- [x] 9.8 Test: `ApplyHotkey_UpdatesInMemoryConfig_WithoutPersistence`
- [x] 9.9 Test: `RebuildCache_SkipsEmptyHotkey`
- [x] 9.10 Test: `RebuildCache_LogsWarning_OnInvalidKey`
- [x] 9.11 Add `HotkeyConfig` property tests to `ProfilesConfigDefaultsTests.cs`: `IsEmpty_WhenKeyEmpty`, `IsEmpty_WhenKeySet`, `NormalizedSignature_Format`, `DisplayText_Format`
- [x] 9.12 Add dirty-state test in `SettingsViewModelDirtyStateTests.cs`: verify hotkey change via setter marks dirty

## 10. Build and Verification

- [x] 10.1 Run `dotnet build Pulsar/Pulsar/Pulsar.csproj` — verify no compilation errors
- [x] 10.2 Run `dotnet test` — verify all existing and new tests pass
- [x] 10.3 Run `dotnet test --filter "FullyQualifiedName~Hotkey"` — focused run on all hotkey tests
- [ ] 10.4 Manual smoke test: launch app, open Settings, capture Ctrl+F1 for ShowGrid, verify Save works, verify Ctrl+F1 opens radial menu without restart
- [ ] 10.5 Manual smoke test: clear a hotkey with Backspace, verify it shows "(None)", verify the old combination no longer triggers
- [ ] 10.6 Manual smoke test: set both hotkeys to same combination, verify conflict badge appears
