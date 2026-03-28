## 1. Rebalance the Create Slot layout

- [x] 1.1 Refactor `Pulsar/Pulsar/Views/Dialogs/Contents/AddSlotContent.xaml` so the top region becomes a lightweight title/status bar instead of a broad explanatory header.
- [x] 1.2 Shrink and reposition the Create Slot live preview into a compact supporting surface that remains visible without dominating the main editing flow.
- [x] 1.3 Rework the left-side plugin type selection surface into a scan-first categorized layout with consistent card sizing.

## 2. Fix plugin type icon rendering consistency

- [x] 2.1 Audit the current Create Slot plugin type icon path and remove the raw `TextBlock` glyph rendering that treats hex icon keys as literal text.
- [x] 2.2 Reuse or extract a shared icon presenter/path so Create Slot plugin type icons resolve Fluent/MDL2 hex keys, emoji, and file-backed icons through the same interpretation rules as slot surfaces.
- [x] 2.3 Update the plugin type option view-model/data flow only as needed so the picker binds to semantic icon keys rather than a dialog-specific raw glyph assumption.

## 3. Reduce header noise and rebalance validation emphasis

- [x] 3.1 Move non-critical explanatory copy out of the prime header area and keep brief orienting text only where it helps pre-selection state.
- [x] 3.2 Restyle validation summary presentation so severe states remain visible while normal draft-state guidance becomes compact supporting context.
- [x] 3.3 Keep field-level required indicators and corrective validation anchored near the active editing controls.

## 4. Clarify the primary authoring flow

- [x] 4.1 Adjust the right-hand configuration column so selected type context, action choice, required details, and secondary polish read in a clear visual sequence.
- [x] 4.2 Preserve optional, advanced, and appearance disclosure behavior while ensuring they remain visually secondary to required setup.
- [x] 4.3 Update any presentation-oriented properties in `Pulsar/Pulsar/ViewModels/Dialogs/AddSlotViewModel.cs` needed to support the new hierarchy and categorized picker layout.

## 5. Validate the polished dialog

- [x] 5.1 Review the Create Slot dialog in pre-selection, post-selection, incomplete, and error states to confirm the hierarchy matches the intended workflow.
- [x] 5.2 Verify plugin type icons render correctly for hex glyph keys and remain aligned with shared slot/icon rendering behavior.
- [x] 5.3 Run the relevant build or UI validation steps to confirm the dialog compiles cleanly after the layout and rendering changes.
