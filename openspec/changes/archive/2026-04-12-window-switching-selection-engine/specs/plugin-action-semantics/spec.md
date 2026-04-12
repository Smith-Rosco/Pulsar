## MODIFIED Requirements

### Requirement: Built-in plugins SHALL define primary user-facing actions in metadata
Each built-in plugin that exposes configurable slot behavior SHALL define its primary user-facing actions explicitly in metadata so authoring flows can present a consistent action list.

#### Scenario: Slot editor opens action choices for a plugin
- **WHEN** a user selects a built-in plugin while creating or editing a slot
- **THEN** the slot editor SHALL present the plugin's primary user-facing actions from metadata rather than relying on undocumented runtime conventions

#### Scenario: Plugin supports more than one distinct user intent
- **WHEN** a built-in plugin supports materially different behaviors such as switching, launching, or sending keys
- **THEN** metadata SHALL expose separate primary actions for those behaviors instead of collapsing them into a generic wrapper verb

### Requirement: Runtime compatibility aliases SHALL not expand the authoring surface
If a built-in plugin preserves older action names for compatibility, those aliases SHALL execute correctly at runtime but SHALL NOT be presented as first-class user-facing actions in newly authored slot configurations.

#### Scenario: Existing slot uses a legacy action alias
- **WHEN** Pulsar executes a previously saved slot that references a supported legacy alias
- **THEN** the plugin SHALL map that alias to the canonical action behavior without requiring the user to edit the slot first

#### Scenario: New slot is authored after standardization
- **WHEN** the slot editor shows the action list for a built-in plugin
- **THEN** it SHALL show only the canonical primary actions rather than both canonical names and legacy aliases

### Requirement: Common user intents SHALL map to recommended plugin/action pairs
Pulsar SHALL document and maintain a consistent recommended mapping from common user intents to plugin/action pairs for built-in plugins, and app-switching actions SHALL preserve that intent mapping even when the runtime adopts a shared window-selection core and clarified blacklist semantics.

#### Scenario: User wants to open a file, URL, or generic target
- **WHEN** the product or documentation describes the recommended plugin for opening generic shell targets
- **THEN** it SHALL point to the canonical command-runner plugin action rather than leaving users to infer between overlapping plugins

#### Scenario: User wants to switch to an existing app or launch one when absent
- **WHEN** the product or documentation describes application-window control workflows
- **THEN** it SHALL distinguish switch-only, launch-only, and switch-or-launch behaviors with their canonical plugin actions

#### Scenario: Shared selection core is introduced for app switching
- **WHEN** Pulsar updates WinSwitcher runtime behavior to use the shared window-selection core
- **THEN** the `activate` action SHALL remain switch-only, the `launch` action SHALL remain launch-only, and the `switch` action SHALL remain switch-or-launch

#### Scenario: ExcludeProcesses behavior is documented
- **WHEN** Pulsar documents or validates WinSwitcher exclusion settings
- **THEN** it SHALL describe consistently whether `ExcludeProcesses` affects discovery, explicit switching, or both, in a way that matches runtime behavior
