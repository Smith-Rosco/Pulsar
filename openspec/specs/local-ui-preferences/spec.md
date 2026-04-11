## Purpose
Define how local UI-only shell preferences are stored and restored so device-local state stays separate from business configuration in `Profiles.json`.

## Requirements

### Requirement: Separate Local UI Preferences Storage
The system SHALL store local UI-only preferences separately from `Profiles.json` so that business configuration remains distinct from device-local shell preferences.

#### Scenario: Store local-only preferences
- **WHEN** the application persists settings such as last-opened settings page, window size, or other shell preferences
- **THEN** those values are stored outside `Profiles.json`

#### Scenario: Business configuration remains authoritative
- **WHEN** the application reads slot, profile, plugin, or runtime business configuration
- **THEN** it continues to use `Profiles.json` and related business configuration sources as the source of truth

### Requirement: Best-Effort Preference Restoration
The system SHALL restore supported local UI preferences on a best-effort basis without preventing the application from functioning if preference data is missing or invalid.

#### Scenario: Preference exists and is valid
- **WHEN** the settings window opens and a valid last-opened page preference exists
- **THEN** the shell restores that page as the initial page

#### Scenario: Preference is missing or invalid
- **WHEN** the application cannot read or validate a stored local UI preference
- **THEN** it falls back to a safe default and continues without surfacing a fatal error
