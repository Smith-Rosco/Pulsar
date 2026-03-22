## ADDED Requirements

### Requirement: PulsarContext SHALL use PulsarNative for process ID lookup

The file `Core/Plugin/PulsarContext.cs` SHALL use `PulsarNative.GetWindowThreadProcessId` instead of inline NativeMethods class.

#### Scenario: Context uses PulsarNative
- **WHEN** PulsarContext needs to get process ID from window handle
- **THEN** it calls `PulsarNative.GetWindowThreadProcessId`

### Requirement: PulsarContext SHALL remove inline NativeMethods class

The inline NativeMethods class at L200-204 SHALL be removed after migration.

#### Scenario: Inline NativeMethods removed
- **WHEN** PulsarContext source is searched for "class NativeMethods"
- **THEN** no inline NativeMethods class exists

### Requirement: PulsarContext behavior SHALL remain unchanged

All properties and methods of PulsarContext SHALL continue to work exactly as before, providing the same window information snapshot.

#### Scenario: Context still captures correctly
- **WHEN** PulsarContext is created at radial menu invocation
- **THEN** it provides the same window handle, process ID, and title information as before