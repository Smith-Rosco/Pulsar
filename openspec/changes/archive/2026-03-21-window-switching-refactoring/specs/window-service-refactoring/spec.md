## ADDED Requirements

### Requirement: WindowService SHALL use PulsarNative for all Win32 API calls

All Win32 API calls in `WindowService.cs` SHALL use methods from `PulsarNative` class instead of inline P/Invoke or local NativeMethods classes.

#### Scenario: Window enumeration uses PulsarNative
- **WHEN** WindowService enumerates windows
- **THEN** it calls `PulsarNative.EnumWindows` and `PulsarNative.GetWindow` instead of any inline definition

### Requirement: WindowService SHALL remove inline NativeMethods classes

The inline NativeMethods class at L1294-1333 and any inline P/Invoke at L180-204 SHALL be removed after migration to PulsarNative.

#### Scenario: NativeMethods class removed
- **WHEN** WindowService source is searched for "class NativeMethods"
- **THEN** no inline NativeMethods class exists

### Requirement: WindowService SHALL organize code with regions

The WindowService class SHALL contain the following #region blocks: Constructor & Fields, Constructor, Public API (IWindowService), Window Enumeration, QuickSwitch State, Icon Management, Window Registry, Internal Event Handlers, Private Helpers.

#### Scenario: Region structure present
- **WHEN** WindowService is opened in editor
- **THEN** code is organized into logical regions as documented

### Requirement: WindowService behavior SHALL remain unchanged

All public methods of IWindowService SHALL continue to behave exactly as before the refactoring. No API contract changes.

#### Scenario: SwitchToPreviousWindow still works
- **WHEN** SwitchToPreviousWindow is called
- **THEN** it switches to the previously recorded window with identical behavior