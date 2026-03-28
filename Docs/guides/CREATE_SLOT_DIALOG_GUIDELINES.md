# Create Slot Dialog Guidelines

**Last Updated**: 2026-03-28  
**Status**: Published

---

## Decision

The `Create Slot` dialog uses a single-page creation flow with field-level validation.

Do:
- Keep the header minimal and stable.
- Put the editable sections in this order: `Label` -> `Action` -> `Required details` -> `Optional settings`.
- Keep all non-blocking inputs hidden inside one `Optional settings` expander.
- Show validation directly on the field or section that needs attention.
- Scroll and focus the first blocking issue when the user presses `Save Slot`.

Don't:
- Repeat the same validation summary in the header and body.
- Split non-required inputs across multiple top-level sections.
- Nest expanders inside `Optional settings`.
- Let preview cards change header height when the selected plugin changes.

---

## Applies To

- `Pulsar/Pulsar/ViewModels/Dialogs/AddSlotViewModel.cs`
- `Pulsar/Pulsar/Views/Dialogs/Contents/AddSlotContent.xaml`
- Plugin action metadata that drives slot creation fields

---

## Correct Information Architecture

### Header

Rule:
- The header only shows the dialog title and a compact state badge.

Reason:
- Long descriptions, duplicated helper text, or dynamic preview blocks make the header jump when the selected plugin changes.

Correct pattern:
- `Create Slot` title text
- compact status badge

Incorrect pattern:
- eyebrow title + title + helper paragraph + validation banner + preview card

### Identity Card

Rule:
- The selected plugin summary and slot preview are merged into one card inside the configuration column, not inside the header.

Reason:
- Slot identity belongs to the working area. Moving it out of the header prevents layout shift and keeps the title row stable.

Correct pattern:
- one card
- one primary orb/icon
- preview title
- plugin context as supporting text
- type/health badges on the right

---

## Section Order

Rule:
- The create form is always ordered as follows:

1. `Label`
2. `Action choice`
3. `Required details`
4. `Optional settings`

Reason:
- Users first identify what the slot is called, then define what it does, then satisfy blocking requirements, and only then adjust optional polish or advanced behavior.

Do:
- Keep `Label` always visible.
- Keep `Action choice` above required parameters.
- Treat `Required details` as the primary completion block.

Don't:
- Hide `Label` behind optional polish.
- Put optional or advanced inputs before blocking inputs.

---

## Optional Settings

Rule:
- Every non-required input belongs under a single `Optional settings` expander.

Reason:
- Users only need one mental model: required inputs are visible, non-required inputs are hidden until needed.

Correct pattern:
- one top-level `Optional settings` expander
- plain subsection headings inside it, such as `Optional details`, `Appearance`, and `Advanced`
- no nested expanders inside that section

Incorrect pattern:
- separate top-level `Optional details`, `Advanced`, and `Optional polish`
- advanced controls hidden behind a second expander

Plugin metadata guidance:
- Use `SlotParameterGroup.Required` only for inputs that block saving.
- Use `SlotParameterGroup.Optional` for non-blocking day-to-day configuration.
- Use `SlotParameterGroup.Advanced` for non-blocking expert settings that should appear lower inside `Optional settings`.

---

## Validation Rules

Rule:
- Validation is field-level, not summary-first.

Reason:
- A global detector forces users to search for the actual issue. Field-level validation shortens the fix loop.

Correct pattern:
- When the user presses `Save Slot`, highlight the missing action or required field.
- Show the error message directly on the invalid section or field.
- Scroll and focus the first blocking issue.

Incorrect pattern:
- Header warning plus duplicated body warning plus unchanged fields.

Current expected behavior:
- Missing action -> highlight `Action choice` and focus the first action control.
- Missing required field -> highlight that field row, show a validation message, and focus its primary picker/button.
- Optional and advanced fields do not block save unless they are explicitly marked required.

---

## Preview and Layout Stability

Rule:
- Preview content must not affect header height.

Reason:
- Selecting a plugin should not push the title downward or cause vertical jump.

Correct pattern:
- keep preview/identity card in the configuration pane
- keep header content stable in both empty and selected states

---

## Scroll Safety

Rule:
- Long create forms must reserve bottom safe space above the dialog footer.

Reason:
- The footer is fixed and should not visually crowd the last visible form controls.

Correct pattern:
- add bottom padding to the scrollable content
- use a large dialog size for `Create Slot`

Implementation note:
- `Create Slot` should use a larger dialog constraint than generic medium forms because its content includes plugin selection, validation, and optional sections.

---

## Plugin Author Checklist

When adding or updating a plugin action used by `Create Slot`:

- Put parameters in the same order users should fill them in.
- Mark only blocking inputs as `IsRequired = true`.
- Use concise labels because they appear directly in field-level validation.
- Use `ValidationHint` for short helper text, not for duplicate errors.
- Keep advanced settings non-blocking and grouped with other optional content.
- Avoid plugin-specific instructions that duplicate UI chrome text like `Choose a type to begin`.

---

## Related Documents

- [Docs/guides/UI_BEST_PRACTICES.md](./UI_BEST_PRACTICES.md)
- [Docs/architecture/DIALOG_SYSTEM.md](../architecture/DIALOG_SYSTEM.md)
- [Docs/handoff/slot-dialog-refactor-handoff.md](../handoff/slot-dialog-refactor-handoff.md)
