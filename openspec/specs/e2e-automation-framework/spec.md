# e2e-automation-framework

## Purpose

Provides an external driver process that executes JSON-defined UI workflows against a debug-mode Pulsar instance, using the UI Automation tree for stable element location and assertions and real system input for hotkey/gesture paths, and emitting standard diagnostic packages on failure.

## Requirements

### Requirement: JSON Workflow Execution

The system SHALL execute UI workflows defined as ordered JSON step lists, supporting at minimum these step types: `launch`, `wait`, `hotkey`, `click`, `assert`, `screenshot`, `record`, and `exit`. Each step SHALL run against an isolated debug-mode Pulsar instance launched with a fixture configuration.

#### Scenario: Workflow runs to success
- **WHEN** a workflow defines steps that all succeed
- **THEN** the driver reports success and exits with code 0

#### Scenario: Workflow step fails
- **WHEN** a workflow step fails (assertion mismatch, timeout, or launch error)
- **THEN** the driver stops, reports the failing step, emits a diagnostic package, and exits with a non-zero code

### Requirement: Stable Element Location via AutomationId

The system SHALL locate and assert UI elements using stable AutomationId-based identifiers. The system SHALL NOT rely on localized text for element location or assertions.

#### Scenario: Element found by AutomationId
- **WHEN** a workflow asserts an element by its AutomationId and the element exists in the UI Automation tree
- **THEN** the assertion succeeds

#### Scenario: Element missing
- **WHEN** a workflow asserts an element by AutomationId that does not exist in the UI Automation tree
- **THEN** the assertion fails with a clear diagnostic naming the missing identifier

#### Scenario: Localized text never used for location
- **WHEN** a workflow runs under any supported language
- **THEN** element location and assertion outcomes are identical because only AutomationIds are used

### Requirement: Real System Input for Hotkey Paths

The system SHALL trigger global hotkey-driven paths (such as opening the radial menu) by injecting real system input, not by invoking UI Automation patterns.

#### Scenario: Hotkey triggers radial menu
- **WHEN** a workflow sends a `hotkey` step matching the fixture's registered global hotkey
- **THEN** the radial menu opens as it would for a real user

### Requirement: State Synchronization

The system SHALL wait for debug-mode state events (published by the application's named pipe hook) before performing state-dependent assertions, rather than blind sleeps.

#### Scenario: Wait for menu-open state
- **WHEN** a workflow issues a `hotkey` step followed by a `waitForState` step for menu-open
- **THEN** the driver proceeds only after the menu-open event is received or the wait times out

### Requirement: Diagnostic Package on Failure

The system SHALL, on any workflow failure, emit a diagnostic package containing at minimum the failed step, the failing assertion, a UI Automation tree snapshot, a screenshot, and the relevant log excerpt. The package SHALL be produced in a deterministic location usable by downstream tools.

#### Scenario: Failure emits complete diagnostic package
- **WHEN** a workflow step fails
- **THEN** the diagnostic package directory contains the failed step record, UIA tree snapshot, screenshot, and log excerpt, all keyed to the same run

### Requirement: Recording and Screenshot Capture

The system SHALL capture screenshots on demand and video recording on workflow demand, using screen-level or window-level capture that includes popups and context menus rendered in separate top-level windows.

#### Scenario: Screenshot captures popup content
- **WHEN** a workflow captures a screenshot while a context menu or popup is visible
- **THEN** the captured image includes the popup content

### Requirement: CI-Safe Execution

The system SHALL run only in an interactive desktop session and SHALL signal a clear pre-flight failure when run in a non-interactive session where real system input cannot be delivered.

#### Scenario: Non-interactive session detected
- **WHEN** the driver detects that no interactive desktop session is available
- **THEN** the driver aborts with a clear pre-flight diagnostic instead of producing flaky input failures
