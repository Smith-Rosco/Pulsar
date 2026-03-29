# plugin-slot-parameter-metadata-contract

## Purpose
Define the plugin metadata contract that enables consistent summaries, quick edit, and full configuration rendering for slot parameters.

## Requirements

### Requirement: Plugin metadata SHALL support layered parameter presentation
Plugin slot parameter metadata SHALL provide enough presentation information for the settings UI to decide how each parameter participates in summaries, quick edit, and full configuration.

#### Scenario: Plugin defines a high-priority parameter
- **WHEN** a plugin marks a parameter as important for quick editing
- **THEN** the settings UI SHALL be able to treat that parameter as a quick-edit candidate without relying on plugin-specific UI templates

#### Scenario: Plugin defines an advanced-only parameter
- **WHEN** a plugin marks a parameter as advanced or dialog-only
- **THEN** the settings UI SHALL be able to exclude that parameter from inline quick edit while still rendering it in full configuration

### Requirement: Plugin metadata SHALL support concise and safe summaries
Plugin slot parameter metadata SHALL let the settings UI generate short slot summaries that are meaningful in a list context and safe to display.

#### Scenario: Parameter has a human-friendly selected label
- **WHEN** a parameter value maps to a compact user-facing label
- **THEN** the plugin metadata SHALL allow the UI to display that compact label in the slot summary

#### Scenario: Parameter is sensitive or noisy
- **WHEN** a parameter value is sensitive, verbose, or unsuitable for list display
- **THEN** the plugin metadata SHALL allow the UI to show a safe summary state such as configured or missing instead of the raw value

### Requirement: Plugin development guidance SHALL document layered-editing metadata responsibilities
The plugin development contract SHALL describe the metadata authors must provide for built-in and future plugins so layered slot editing behaves consistently.

#### Scenario: New built-in plugin action is added
- **WHEN** a developer adds a new built-in plugin action that accepts slot parameters
- **THEN** the documented plugin development guidance SHALL require the action metadata to include the presentation details needed for summaries and layered editing

#### Scenario: Metadata omits optional presentation hints
- **WHEN** a plugin provides only the minimum required metadata
- **THEN** the documented contract SHALL define the fallback behavior the settings UI uses to keep the plugin configurable
