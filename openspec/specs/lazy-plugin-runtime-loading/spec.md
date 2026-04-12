## Purpose
Define the plugin-loading contract so extension plugin metadata can be discovered after startup and instances are activated only when needed.

## Requirements

### Requirement: Extension plugin descriptors SHALL be discoverable without activating plugin instances
The system SHALL discover and retain extension plugin descriptors independently from live plugin instances so plugin metadata and availability can be inspected without instantiating every extension plugin during startup.

#### Scenario: Deferred plugin discovery completes
- **WHEN** Pulsar performs deferred extension plugin discovery after startup readiness
- **THEN** the runtime SHALL register extension plugin descriptors without creating live plugin instances for descriptors that are not yet needed

### Requirement: Extension plugins SHALL activate on demand
The runtime SHALL activate an extension plugin only when configuration or execution requires that specific plugin instance.

#### Scenario: User executes a configured extension plugin
- **WHEN** a slot references an extension plugin that has a registered descriptor but no active instance
- **THEN** the runtime SHALL activate that plugin before execution and reuse the active instance according to the runtime activation policy

### Requirement: Descriptor discovery SHALL be reusable within an app lifetime
The runtime SHALL cache plugin descriptor discovery results for the current app lifetime so repeated queries do not rescan assemblies and plugin folders unless the runtime explicitly invalidates the discovery state.

#### Scenario: Descriptor query repeats after deferred discovery
- **WHEN** a caller requests plugin descriptors after the runtime has already completed discovery for the current app lifetime
- **THEN** the runtime SHALL reuse the cached descriptor set instead of performing a second full reflection scan
