## MODIFIED Requirements

### Requirement: Low-priority field guidance SHALL use progressive disclosure
The system SHALL move descriptions, examples, and other low-priority instructional guidance into tooltip-style or comparable secondary disclosure patterns when that content is not required for immediate comprehension. In the unified picker phase, per-card descriptive prose SHALL be available through tooltip support rather than inline display. In the configuration phase, the Appearance section SHALL default to collapsed in Create mode but expanded in Edit mode. The three-section hierarchy (Behavior always visible, Appearance context-dependent, Advanced always collapsed) SHALL serve as the primary disclosure mechanism.

#### Scenario: Field includes example text
- **WHEN** a parameter exposes an example or descriptive help text that is not critical to understanding current state
- **THEN** the system SHALL make that help available through secondary disclosure rather than reserving permanent vertical space beneath the field

#### Scenario: User needs more context for a field
- **WHEN** the user focuses or hovers the field help affordance
- **THEN** the system SHALL reveal the field guidance close to the related control without changing the overall page hierarchy

#### Scenario: Appearance customization is secondary
- **WHEN** the unified slot editor renders label, icon, color, and behavior controls together
- **THEN** lower-priority appearance customization SHALL use disclosure or visually secondary presentation so required behavior setup remains primary. In Create mode, Appearance SHALL be collapsed by default; in Edit mode, it SHALL be expanded by default.

#### Scenario: Picker renders many slot type cards
- **WHEN** the unified picker renders primary intent cards and browse-all plugin types
- **THEN** per-card descriptive prose SHALL be available through tooltip support instead of being shown inline for every option

### Requirement: Critical status information SHALL remain visible
The system SHALL keep validation status, required-state indicators, and other critical correctness signals visible in-context rather than hiding them behind disclosure. In the unified configuration phase, the compound status indicator in the header SHALL communicate validation state compactly, while detailed validation messages SHALL appear inline near the relevant fields.

#### Scenario: Required field is missing
- **WHEN** a field required for slot execution is not configured
- **THEN** the UI SHALL expose that incomplete state directly in the active editing surface without requiring hover or focus to discover it

#### Scenario: Validation guidance affects correction
- **WHEN** the system has corrective validation feedback relevant to the user's current edit
- **THEN** the UI SHALL display the validation state or summary visibly even if explanatory field help or lower-priority appearance content uses secondary disclosure

#### Scenario: Optional summary context exists
- **WHEN** the dialog exposes summary tokens or supporting metadata
- **THEN** summary tokens SHALL render within the Behavior section below the action selector, visually secondary to the required parameters and SHALL NOT displace visible validation cues

#### Scenario: Picker phase is active without a selection
- **WHEN** the unified picker is still waiting for the first authoring choice
- **THEN** orienting copy MAY explain the next action concisely, but the intent grid SHALL remain the primary visual element without competing explanatory banners

#### Scenario: Dialog is incomplete but not in error
- **WHEN** the slot is still in a normal draft state with required details remaining
- **THEN** the dialog SHALL expose that status in the compound header indicator without using the same visual prominence reserved for severe validation or blocking error states
