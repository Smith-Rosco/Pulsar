## MODIFIED Requirements

### Requirement: Slots page SHALL prioritize scanable slot summaries
The slots settings experience SHALL present each slot primarily as a compact summary object that supports fast scanning, comparison, and health recognition before any deep editing begins. Collapsed presentation SHALL focus on identity and configuration health and SHALL NOT spend list space on instructional prose about how to edit the slot.

#### Scenario: User scans a populated slots list
- **WHEN** the slots page renders multiple configured slots
- **THEN** each slot SHALL show a stable summary including identity, action context, and configuration health without requiring expansion

#### Scenario: Slot has missing required parameters
- **WHEN** a slot is missing one or more required parameters
- **THEN** the collapsed slot summary SHALL expose a visible warning state that communicates incomplete configuration without showing the full form

#### Scenario: Collapsed card avoids redundant guidance
- **WHEN** the slot is otherwise healthy and ready
- **THEN** the collapsed summary SHALL remain concise and SHALL NOT display helper copy whose only purpose is to instruct the user to expand or open full configuration

### Requirement: Inline expansion SHALL remain lightweight
When a user expands a slot from the list page, the inline editing surface SHALL stay limited to high-frequency controls and SHALL NOT become the primary container for long-form parameter authoring. The expanded layout SHALL prefer compact editing rows and lightweight grouping over multiple prominent section headings.

#### Scenario: User expands a simple slot
- **WHEN** a slot with a small quick-edit set is expanded
- **THEN** the expanded content SHALL expose only the designated quick-edit controls and keep the card height materially smaller than a full parameter form

#### Scenario: Slot has extensive parameter guidance
- **WHEN** a slot action includes verbose help text, many fields, or advanced configuration sections
- **THEN** the inline expanded state SHALL omit the long-form guidance and rely on the full-configuration entry point for the deeper authoring flow

#### Scenario: Quick edit avoids extra hierarchy
- **WHEN** related inline controls can be understood through alignment and proximity
- **THEN** the UI SHALL avoid adding summary sections or redundant headings that increase reading overhead without improving task clarity

### Requirement: Slot summaries SHALL communicate parameter state concisely
The slots page SHALL summarize parameter readiness using concise, list-friendly state text instead of reproducing full values or long explanations inside the card.

#### Scenario: Required parameter contains a long file path
- **WHEN** a summary includes a parameter whose raw value is long or noisy
- **THEN** the list card SHALL prefer a concise state-oriented summary such as configured, missing, or a short label rather than the full raw text

#### Scenario: Sensitive parameter is configured
- **WHEN** a parameter is marked as not safe for summary display
- **THEN** the list card SHALL communicate only safe state text and SHALL NOT reveal the underlying value

#### Scenario: User enters active edit mode
- **WHEN** the slot is expanded for quick editing
- **THEN** summary-only tokens or summary blocks SHALL yield to the active editing controls rather than occupying their own editing section
