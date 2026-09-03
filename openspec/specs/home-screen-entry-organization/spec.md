# home-screen-entry-organization Specification

## Purpose
Define the entry-organization contract for Pulsar's in-app surfaces: the grouping and ordering rules applied to settings navigation, the plugin management list, and first-launch presentation, so the office-automation pillars (Excel/WPS macros, web scripts, secure form-fill) are surfaced first and system utilities are grouped behind them.

## Requirements

### Requirement: Settings navigation groups office-automation entries first
The system SHALL order settings navigation so that entries belonging to the office-automation workbench appear in a leading group, ahead of analytics and system/about entries.

#### Scenario: Office-automation entries precede system entries
- **WHEN** the settings window builds its navigation from the page catalog
- **THEN** the entries that represent core automation (slots/macros/web scripts/form-fill) SHALL appear before the analytics and about/system entries

#### Scenario: Navigation order is stable across sessions
- **WHEN** the settings window is opened repeatedly
- **THEN** the navigation SHALL present the same entry order each time, independent of configuration-edit state

### Requirement: Plugin management list surfaces the three pillars first
The system SHALL order and group the plugin management list so that the three office-automation pillars (VBA/macro runner, web-script runner, PKI form-fill) appear in a leading group, with system/utility plugins grouped in a separate trailing group.

#### Scenario: Pillar plugins render in the leading group
- **WHEN** the plugin management page renders the plugin list
- **THEN** the pillar plugins (VBA/macro, web-script, form-fill/PKI) SHALL appear in the leading group ahead of system utility plugins

#### Scenario: System plugins render in a separate trailing group
- **WHEN** the plugin management page renders the plugin list
- **THEN** system/utility plugins SHALL be visually grouped apart from the pillar group and SHALL appear after it

### Requirement: First-launch presentation follows the three-pillar narrative
The system SHALL present the first-launch onboarding steps and usage-selection options in an order consistent with the office-automation three-pillar narrative.

#### Scenario: First-launch options lead with office automation
- **WHEN** the first-launch wizard presents usage options
- **THEN** the office-automation-oriented options SHALL be presented before generic/system options

### Requirement: Entry titles and descriptions reflect the workbench framing
The system SHALL present entry titles and descriptions using the office-workbench framing (macros, web scripts, secure form-fill) rather than internal implementation labels, through the localization service.

#### Scenario: Entries use workbench-framed localized text
- **WHEN** an entry title or description is displayed
- **THEN** it SHALL resolve through the localization service to workbench-framed text
- **AND** the text SHALL NOT be a hardcoded string in code or XAML
