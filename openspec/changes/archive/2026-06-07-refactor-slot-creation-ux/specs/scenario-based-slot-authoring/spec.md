## REMOVED Requirements

### Requirement: Slot creation defaults to an intent-first flow
**Reason**: The Scenario/Advanced flow toggle is replaced by a unified intent grid picker (`unified-slot-type-picker`) that serves the same purpose without requiring a separate flow designation.
**Migration**: The curated intent cards in the unified picker serve the same function as the scenario cards. Users no longer toggle between flows; all slot types are accessible from a single unified view.

### Requirement: Supported intents map to canonical plugin actions
**Reason**: Intent-to-plugin-action mapping is preserved and moved to the `SlotTypeCard.DefaultAction` property. The mapping logic is the same but lives in the `SlotTypeCard` model rather than in the `ScenarioOption` class.
**Migration**: Plugin-action mappings for built-in intents remain identical. New mappings use `SlotTypeCard` instead of `ScenarioOption`.

### Requirement: Advanced editing remains available
**Reason**: The separate "Advanced" flow path is replaced by the "Browse All" collapsible section within the unified picker, which provides the same functionality without requiring a mode switch.
**Migration**: Users access non-primary plugin types through the "Browse all slot types..." expander at the bottom of the unified picker, which replaces the category-filtered plugin browser in the former Advanced flow.
