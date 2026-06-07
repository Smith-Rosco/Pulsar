## ADDED Requirements

### Requirement: IProcessLauncher wraps Process.Start for testability

The system SHALL provide an `IProcessLauncher` interface with a `Launch(ProcessStartInfo)` method that abstracts `Process.Start`. The default implementation `ProcessLauncher` SHALL delegate directly to `Process.Start`.

#### Scenario: ProcessLauncher delegates to Process.Start

- **WHEN** `ProcessLauncher.Launch(new ProcessStartInfo { FileName = "cmd.exe", Arguments = "/k echo hi" })` is called
- **THEN** `Process.Start` SHALL be invoked once with the same `ProcessStartInfo` values

#### Scenario: IProcessLauncher is mockable in unit tests

- **WHEN** a test injects a mock `IProcessLauncher`
- **AND** the CommandPlugin's `ExecuteAsync("run", ...)` is called with `path = "notepad.exe"`
- **THEN** the mock's `Launch` method SHALL be called with `FileName = "notepad.exe"`
- **AND** no actual process is started

### Requirement: CommandPlugin uses IProcessLauncher for run action

The `CommandPlugin` SHALL accept `IProcessLauncher` via constructor injection and use it to launch processes when the `run` action is invoked, instead of calling `Process.Start` directly.

#### Scenario: Run action launches process through IProcessLauncher

- **WHEN** `ExecuteAsync("run", { path: "notepad.exe", arguments: "readme.txt" })` is called
- **THEN** `IProcessLauncher.Launch` SHALL be called with `FileName = "notepad.exe"` and `Arguments = "readme.txt"`

#### Scenario: Run action with working directory sets WorkingDirectory

- **WHEN** `ExecuteAsync("run", { path: "cmd.exe", workingDir: "C:\\Temp" })` is called
- **THEN** `IProcessLauncher.Launch` SHALL be called with `WorkingDirectory = "C:\\Temp"`

#### Scenario: Run action with missing path returns error

- **WHEN** `ExecuteAsync("run", {})` is called (no "path" parameter)
- **THEN** the result SHALL be `PluginResult.Error` with message indicating missing "path" parameter
- **AND** `IProcessLauncher.Launch` SHALL NOT be called

#### Scenario: Process launch failure returns localized error

- **WHEN** `IProcessLauncher.Launch` throws a `Win32Exception`
- **THEN** the result SHALL be `PluginResult.Error` with a localized error message
- **AND** the exception SHALL be logged at Error level
