## ADDED Requirements

This change is a **refactoring** to fix a bug in the Edit Slot dialog. No new requirements are introduced - the existing behavior is preserved while fixing a feedback loop that causes data corruption.

### Requirement: Edit Slot dialog action selection does not corrupt slot configuration

The Edit Slot dialog SHALL preserve the slot's action value during dialog interaction without causing feedback loops that corrupt the configuration.

#### Scenario: Dialog loads with existing action
- **GIVEN** a slot with a valid action configured
- **WHEN** the Edit Slot dialog opens
- **THEN** the slot's action SHALL remain unchanged after dialog initialization

#### Scenario: User selects a different action
- **GIVEN** the Edit Slot dialog is open
- **WHEN** user selects a different action from the dropdown
- **THEN** the selected action SHALL be saved to the slot configuration

#### Scenario: User cancels without saving
- **GIVEN** the Edit Slot dialog is open with unsaved changes
- **WHEN** user clicks Cancel
- **THEN** the original slot configuration SHALL be preserved

#### Scenario: User confirms changes
- **GIVEN** the Edit Slot dialog is open with valid configuration
- **WHEN** user clicks OK
- **THEN** the updated slot configuration SHALL be saved to Profiles.json with the correct action value

## REMOVED Requirements

None - this is a pure bug fix with no removed functionality.
