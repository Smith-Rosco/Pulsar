## 1. Slots Page Hierarchy

- [x] 1.1 Remove redundant explanatory prose from collapsed slot cards and keep only identity and status-oriented scan content.
- [x] 1.2 Redesign expanded slot cards so quick-edit controls appear as the primary content without redundant summary sections.
- [x] 1.3 Introduce lighter grouping and compact row-oriented layout rules for high-frequency quick-edit fields.

## 2. Full Configuration Dialog

- [x] 2.1 Rework the full configuration dialog into a clearer inspector-style hierarchy with a slot identity block and restrained grouping.
- [x] 2.2 Distinguish always-visible validation and required-state feedback from lower-priority help content in the dialog.
- [x] 2.3 Ensure complex fields can still use wider or taller layouts when compact rows would reduce clarity.

## 3. Progressive Disclosure

- [x] 3.1 Move field descriptions, examples, and input hints into tooltip or info-affordance disclosure patterns where appropriate.
- [x] 3.2 Keep keyboard-accessible help interactions so disclosed guidance is not pointer-only.
- [x] 3.3 Preserve visible validation summaries and field-critical status rather than hiding them behind tooltips.

## 4. Metadata-Driven Presentation Rules

- [x] 4.1 Update slot parameter presentation logic so summary-oriented content stays in scan surfaces and does not render as its own quick-edit section.
- [x] 4.2 Define renderer fallbacks for third-party or high-complexity fields that cannot fit compact row layouts safely.
- [x] 4.3 Align slot summary, quick-edit, and dialog presentation rules with the updated information hierarchy specs.

## 5. Validation And Review

- [x] 5.1 Verify the refined layouts with multiple built-in plugin slot types, including picker-heavy and sensitive-parameter flows.
- [x] 5.2 Run `dotnet build Pulsar/Pulsar/Pulsar.csproj` and address any XAML or binding regressions.
- [x] 5.3 Review tooltip, focus, and scanning behavior to confirm the UI feels lighter without hiding critical information.
