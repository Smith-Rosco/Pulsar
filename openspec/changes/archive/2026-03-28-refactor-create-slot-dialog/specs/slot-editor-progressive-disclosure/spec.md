## MODIFIED Requirements

### Requirement: Low-priority field guidance SHALL use progressive disclosure
The system SHALL move descriptions, examples, and other low-priority instructional guidance into tooltip-style or comparable secondary disclosure patterns when that content is not required for immediate comprehension, and SHALL apply the same approach to low-priority appearance customization and summary support in slot creation/configuration surfaces. In the Create Slot dialog, plugin-type descriptions, appearance coaching copy, and parameter help that is not required to complete the current step SHALL NOT permanently occupy primary layout space.

#### Scenario: Field includes example text
- **WHEN** a parameter exposes an example or descriptive help text that is not critical to understanding current state
- **THEN** the system SHALL make that help available through secondary disclosure rather than reserving permanent vertical space beneath the field

#### Scenario: User needs more context for a field
- **WHEN** the user focuses or hovers the field help affordance
- **THEN** the system SHALL reveal the field guidance close to the related control without changing the overall page hierarchy

#### Scenario: Appearance customization is secondary
- **WHEN** the Create Slot or full configuration dialog renders label, icon, color, and behavior controls together
- **THEN** lower-priority appearance customization SHALL be allowed to use disclosure or visually secondary presentation so required behavior setup remains primary

#### Scenario: Create Slot lists many plugin types
- **WHEN** the Create Slot dialog renders multiple plugin-type choices in the left column
- **THEN** per-plugin descriptive prose SHALL be available through secondary disclosure such as tooltip support instead of being shown inline for every option

### Requirement: Critical status information SHALL remain visible
The system SHALL keep validation status, required-state indicators, and other critical correctness signals visible in-context rather than hiding them behind disclosure.

#### Scenario: Required field is missing
- **WHEN** a field required for slot execution is not configured
- **THEN** the UI SHALL expose that incomplete state directly in the active editing surface without requiring hover or focus to discover it

#### Scenario: Validation guidance affects correction
- **WHEN** the system has corrective validation feedback relevant to the user's current edit
- **THEN** the UI SHALL display the validation state or summary visibly even if explanatory field help or lower-priority appearance content uses secondary disclosure

#### Scenario: Optional summary context exists
- **WHEN** the dialog exposes summary tokens or supporting metadata
- **THEN** that information MAY be visually secondary or integrated near the preview anchor, but SHALL NOT displace visible validation or required-state cues from the primary editing path

#### Scenario: User has not yet selected a plugin type
- **WHEN** the Create Slot dialog is still waiting for the first authoring choice
- **THEN** orienting copy MAY explain the next action, but secondary explanatory text SHALL collapse or disappear once the active configuration path is available
