## 1. Reshape Create Slot information hierarchy

- [x] 1.1 Audit `Pulsar/Pulsar/Views/Dialogs/Contents/AddSlotContent.xaml` and identify all always-visible explanatory text, preview blocks, and equal-weight section containers that currently compete with the primary flow.
- [x] 1.2 Refactor the top-level dialog structure so the header/preview area becomes a lighter workflow anchor rather than a dominant full preview card.
- [x] 1.3 Reorder and restyle the right-hand configuration column so action selection, required setup, optional details, and appearance polish read as a clear sequential flow.

## 2. Reduce always-visible helper prose and redundant chrome

- [x] 2.1 Remove or demote left-column plugin descriptions from always-visible list content into tooltip or comparable secondary disclosure.
- [x] 2.2 Remove redundant visible helper paragraphs and subsection headings that are no longer needed once the user has entered the active configuration flow.
- [x] 2.3 Update appearance and auxiliary guidance so low-priority instructional copy uses deeper disclosure while validation and required-state cues remain visible.

## 3. Increase density and scanability of selection and field rows

- [x] 3.1 Redesign the plugin-type picker items to favor icon, name, and selected state with a denser scan-first layout that remains usable as plugin count grows.
- [x] 3.2 Refactor `CreateSlotParameterFieldTemplate` so simple parameter rows rely on spacing, alignment, and lightweight separators before bordered card treatment.
- [x] 3.3 Preserve support for more complex picker states, long values, and validation output where compact rows would reduce clarity.

## 4. Align supporting ViewModel state with the new hierarchy

- [x] 4.1 Update `Pulsar/Pulsar/ViewModels/Dialogs/AddSlotViewModel.cs` to remove or repurpose properties whose current wording or visibility assumptions no longer match the redesigned layout.
- [x] 4.2 Ensure validation summary, preview identity, selected plugin context, and disclosure state still expose the right information at the right visual priority.
- [x] 4.3 Keep existing slot suggestion, parameter, and picker behavior intact while adapting only the presentation-oriented state needed by the new UI.

## 5. Validate the refactor

- [x] 5.1 Review the Create Slot dialog in both pre-selection and post-selection states to verify that the primary authoring path is visually dominant and helper prose no longer crowds the surface.
- [x] 5.2 Verify required-state indicators, validation summaries, preview badges, and tooltip-based guidance still behave correctly across representative plugin types and parameter configurations.
- [x] 5.3 Run the relevant build or UI validation steps to confirm the refactor compiles cleanly and does not break the dialog surface.
