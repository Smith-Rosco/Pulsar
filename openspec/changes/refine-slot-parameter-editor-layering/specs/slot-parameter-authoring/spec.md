## ADDED Requirements

### Requirement: Slots page SHALL prioritize scanable slot summaries
The slots settings experience SHALL present each slot primarily as a compact summary object that supports fast scanning, comparison, and health recognition before any deep editing begins.

#### Scenario: User scans a populated slots list
- **WHEN** the slots page renders multiple configured slots
- **THEN** each slot SHALL show a stable summary including identity, action context, and configuration health without requiring expansion

#### Scenario: Slot has missing required parameters
- **WHEN** a slot is missing one or more required parameters
- **THEN** the collapsed slot summary SHALL expose a visible warning state that communicates incomplete configuration without showing the full form

### Requirement: Inline expansion SHALL remain lightweight
When a user expands a slot from the list page, the inline editing surface SHALL stay limited to high-frequency controls and SHALL NOT become the primary container for long-form parameter authoring.

#### Scenario: User expands a simple slot
- **WHEN** a slot with a small quick-edit set is expanded
- **THEN** the expanded content SHALL expose only the designated quick-edit controls and keep the card height materially smaller than a full parameter form

#### Scenario: Slot has extensive parameter guidance
- **WHEN** a slot action includes verbose help text, many fields, or advanced configuration sections
- **THEN** the inline expanded state SHALL omit the long-form guidance and rely on the full-configuration entry point for the deeper authoring flow

### Requirement: Slot summaries SHALL communicate parameter state concisely
The slots page SHALL summarize parameter readiness using concise, list-friendly state text instead of reproducing full values or long explanations inside the card.

#### Scenario: Required parameter contains a long file path
- **WHEN** a summary includes a parameter whose raw value is long or noisy
- **THEN** the list card SHALL prefer a concise state-oriented summary such as configured, missing, or a short label rather than the full raw text

#### Scenario: Sensitive parameter is configured
- **WHEN** a parameter is marked as not safe for summary display
- **THEN** the list card SHALL communicate only safe state text and SHALL NOT reveal the underlying value
