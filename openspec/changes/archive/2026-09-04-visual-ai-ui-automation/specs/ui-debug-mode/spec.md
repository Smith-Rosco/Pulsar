## Purpose

Provides an application-level debug mode that makes Pulsar externally drivable without contaminating the user's real configuration or firing real global hotkeys, enabling automated UI testing and visual AI iteration.

## ADDED Requirements

### Requirement: Debug Mode Activation

The system SHALL activate debug mode only when the `--ui-debug` command-line flag is present at startup. In debug mode the application SHALL NOT alter, create, or write to the user's production `Profiles.json`; it SHALL redirect all configuration persistence to an isolated debug configuration directory.

#### Scenario: Startup without the flag
- **WHEN** the application starts without the `--ui-debug` flag
- **THEN** the application runs in normal mode with the production configuration path and all behavior unchanged

#### Scenario: Startup with the flag
- **WHEN** the application starts with the `--ui-debug` flag
- **THEN** the application runs in debug mode and reads and writes configuration only under the isolated debug directory

### Requirement: Disabled Global Input Hooks

In debug mode the system SHALL NOT register global keyboard/mouse hooks or global hotkey listeners, and SHALL expose a way to trigger the radial menu and menu actions explicitly instead of via system-wide input interception.

#### Scenario: No global hooks in debug mode
- **WHEN** the application is running in debug mode
- **THEN** no global keyboard hook, global mouse hook, or hotkey listener is registered

#### Scenario: Explicit triggering available
- **WHEN** an external driver needs to open the radial menu in debug mode
- **THEN** the menu can be triggered through an explicit in-app channel rather than a simulated system-wide hotkey

### Requirement: Named Pipe State Publishing

In debug mode the system SHALL publish internal runtime state to a named pipe so external drivers can wait for state transitions (menu opened, slot activated, action executed) without polling the UI.

#### Scenario: State events observable
- **WHEN** a state transition occurs (e.g., radial menu opened)
- **THEN** the event is published to the named pipe with a stable event name and the external driver receives it

#### Scenario: State timeout
- **WHEN** an external driver waits for a state event that does not occur within its configured timeout
- **THEN** the driver fails that workflow step with a timeout diagnostic

### Requirement: Sensitive Content Redaction

In debug mode the system SHALL redact or mask sensitive PKI/secret content in screenshots, recordings, and state published to the named pipe, so debug artifacts do not leak secret material.

#### Scenario: PKI UI redacted in capture
- **WHEN** a screenshot or recording is captured in debug mode and the PKI area is visible
- **THEN** the PKI content is masked or excluded from the captured output

### Requirement: Verbose Diagnostics

In debug mode the system SHALL enable verbose logging so that failures can be diagnosed from logs produced during automated runs.

#### Scenario: Debug run produces verbose logs
- **WHEN** the application runs in debug mode
- **THEN** verbose log output is written to the isolated debug log location
