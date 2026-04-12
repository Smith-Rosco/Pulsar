## MODIFIED Requirements

### Requirement: Common user intents SHALL map to recommended plugin/action pairs
Pulsar SHALL document and maintain a consistent recommended mapping from common user intents to plugin/action pairs for built-in plugins, and app-switching actions SHALL preserve that intent mapping even when the runtime adopts a shared window-selection core.

#### Scenario: User wants to open a file, URL, or generic target
- **WHEN** the product or documentation describes the recommended plugin for opening generic shell targets
- **THEN** it SHALL point to the canonical command-runner plugin action rather than leaving users to infer between overlapping plugins

#### Scenario: User wants to switch to an existing app or launch one when absent
- **WHEN** the product or documentation describes application-window control workflows
- **THEN** it SHALL distinguish switch-only, launch-only, and switch-or-launch behaviors with their canonical plugin actions

#### Scenario: Shared selection core is introduced for app switching
- **WHEN** Pulsar updates WinSwitcher runtime behavior to use the shared window-selection core
- **THEN** the `activate` action SHALL remain switch-only, the `launch` action SHALL remain launch-only, and the `switch` action SHALL remain switch-or-launch
