## 1. Metadata Contract

- [x] 1.1 Extend slot parameter metadata models to support summary-safe display hints, quick-edit priority/tier hints, and dialog-only or advanced presentation flags.
- [x] 1.2 Update built-in plugin action metadata to provide the new layered-editing hints for summary generation and quick-edit selection.
- [x] 1.3 Update plugin development documentation/spec-aligned guidance so future plugin authors know which slot-parameter presentation metadata is required and what fallback behavior applies.

## 2. Slot Card Restructure

- [x] 2.1 Refactor the slots page card layout to emphasize collapsed summaries, action/state badges, and concise configuration-health indicators.
- [x] 2.2 Limit inline expansion to the quick-edit field set and remove long-form descriptions, advanced groups, and verbose validation copy from the expanded card.
- [x] 2.3 Add summary-generation and quick-edit selection logic in the relevant view-model layer so cards can render stable list-friendly parameter state.

## 3. Full Configuration Flow

- [x] 3.1 Add a dedicated full-configuration dialog/view model for slot parameter editing that preserves slot identity, action context, and validation state.
- [x] 3.2 Route advanced parameters, full help text, examples, picker-heavy controls, and detailed validation feedback into the full-configuration surface.
- [x] 3.3 Connect each slot card to the full-configuration entry point and ensure returning from the dialog preserves the current slots-page context.

## 4. Validation And Verification

- [x] 4.1 Add or update tests for summary generation, quick-edit selection, and fallback behavior when plugin metadata is incomplete.
- [x] 4.2 Add or update UI-facing tests or validation coverage to confirm incomplete slots still surface warning summaries while full details remain available in the dialog.
- [x] 4.3 Run the relevant build/test validation and confirm the layered slot editor works with existing built-in plugin slot configurations.
