## MODIFIED Requirements

### Requirement: Full configuration SHALL establish a deeper editing hierarchy
The system SHALL present full configuration and slot creation as deeper editing layers with a typography-led hierarchy, making slot identity, validation state, and grouped fields easy to understand without relying on repeated card-style containers for every section, and SHALL keep slot identity and health indicators readable in every supported theme. In the Create Slot dialog specifically, the visible hierarchy SHALL keep the primary creation flow dominant, with slot type selection and active configuration work reading as the main path while preview content remains supportive rather than competing for first attention. The Create Slot header SHALL behave as a lightweight workflow/status anchor rather than a broad explanatory content region.

#### Scenario: User opens full configuration from the slots page
- **WHEN** the full configuration dialog opens
- **THEN** the dialog SHALL identify the slot clearly and present grouped editing content in a more explicit hierarchy than the inline quick-edit surface, using text hierarchy and spacing as the primary structure

#### Scenario: Slot has both common and advanced settings
- **WHEN** the dialog renders a slot with mixed-complexity fields
- **THEN** the dialog SHALL differentiate foundational settings from deeper configuration without forcing all content into the same visual priority or surrounding every group with equivalent bordered panels

#### Scenario: User creates a new slot
- **WHEN** the Create Slot dialog opens
- **THEN** the surface SHALL emphasize the primary editing path in a single coherent hierarchy where slot type, required setup, and optional polish read as one workflow rather than as competing stacked summary sections

#### Scenario: Slot preview badges render with theme tones
- **WHEN** full configuration or slot creation renders slot identity and health badges
- **THEN** those badges SHALL preserve readable text contrast and SHALL NOT degrade into border-only pills because of resource scope mismatches

#### Scenario: Create Slot shows type selection and live preview together
- **WHEN** the Create Slot dialog renders both plugin-type selection and slot preview context
- **THEN** the preview region SHALL support orientation and validation without visually outweighing the user's next required decision in the authoring flow

#### Scenario: User has selected a slot type and begins configuration
- **WHEN** the right-hand configuration surface becomes active after type selection
- **THEN** action selection and required configuration SHALL read as the dominant content, while descriptive or summary-only context SHALL be visually secondary

#### Scenario: Create Slot shows draft-state guidance before configuration begins
- **WHEN** the dialog is waiting for the user to choose a slot type
- **THEN** the surface MAY show a brief orienting hint, but SHALL reserve the most prominent space for task identity, type selection, and compact preview/status context rather than long explanatory header content
