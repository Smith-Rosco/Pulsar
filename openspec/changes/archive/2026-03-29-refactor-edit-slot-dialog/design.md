## Context

The Edit Slot dialog (SlotConfigurationDialogContent) is used to modify existing slot configurations. It shares the same functionality as the Create Slot dialog (AddSlotContent) but was not updated during a previous refactoring that fixed a critical feedback loop bug.

**Current State:**
- ComboBox bindings use default TwoWay mode
- No validation error banner for action field
- No re-entry guard in code-behind
- ViewModel doesn't filter Action PropertyChanged events

**Reference Implementation:**
The Create Slot dialog (AddSlotContent) was already fixed for these issues. The fixes are documented in:
- `Docs/lessons/WPF_RADIOBUTTON_PROPERTYCHANGED_FEEDBACK_LOOP.md`

## Goals / Non-Goals

**Goals:**
1. Fix the feedback loop bug that clears Slot.Action to empty string
2. Align Edit Slot dialog UI with Create Slot dialog patterns
3. Add validation error banner for action field
4. Ensure consistent user experience between Create and Edit flows

**Non-Goals:**
- No new functionality or capability changes
- No changes to data models or serialization
- No modifications to the plugin system

## Decisions

### Decision 1: Use Mode=OneWay on ComboBox bindings

**Choice:** Change `SelectedValue="{Binding Slot.Action}"` to `SelectedValue="{Binding Slot.Action, Mode=OneWay}"`

**Rationale:** The TwoWay binding causes WPF to write back the initial ComboBox value (empty string) to Slot.Action when the dialog loads, before the user has made a selection. This is the root cause of the "action": "" bug.

**Alternative Considered:**
- Add logic to initialize Slot.Action to first available action on dialog load
- This would change behavior and require careful handling of existing configurations

### Decision 2: Filter Action PropertyChanged in ViewModel

**Choice:** Add property change filter in SlotConfigurationDialogViewModel.OnSlotPropertyChanged to skip Action property

**Rationale:** Prevents the feedback loop from re-entering SetAction when other properties change. The Action property is managed exclusively through SetAction method.

**Alternative Considered:**
- Use a more complex state machine to detect re-entry
- The simple filter matches the Create Slot implementation pattern

### Decision 3: Add re-entry guard in code-behind

**Choice:** Add `_isSettingAction` boolean flag to prevent double-execution

**Rationale:** Provides defense-in-depth against race conditions when user interacts rapidly with the action selector.

**Alternative Considered:**
- Use debouncing
- Debouncing adds latency that feels unresponsive for action selection

## Risks / Trade-offs

### Risk: Missing edge case in feedback loop

**Mitigation:** The fix pattern is proven in AddSlotContent. Both dialogs use the same underlying mechanism (ComboBox + SetAction).

### Risk: Breaking existing Edit Slot behavior

**Mitigation:** The changes are bug fixes, not behavioral changes. The UI will function the same way, just without the bug that clears the action.

### Risk: Inconsistent validation between Create and Edit

**Mitigation:** Align both dialogs to use the same validation patterns. The Edit Slot dialog will gain the error banner that was missing.
