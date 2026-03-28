## 1. Canonical Plugin Identity

- [x] 1.1 Decide and apply canonical display names and descriptions for built-in plugins that currently use inconsistent naming, including `BasicCommand`, `WinSwitcher`, `Pki`, and `SystemCommand`.
- [x] 1.2 Align built-in plugin metadata and any fallback metadata generation so plugin picker surfaces, metadata registries, and docs all resolve to the same canonical display identity.
- [x] 1.3 Update built-in plugin documentation pages and `PLUGIN_DEVELOPMENT.md` to use the canonical plugin names and explain the naming model for future plugins.

## 2. Action Semantics And Compatibility

- [x] 2.1 Define the canonical primary action set for each built-in plugin and encode those actions explicitly in plugin metadata.
- [x] 2.2 Add or preserve runtime action-alias handling so legacy action names continue to execute without appearing as first-class choices for newly authored slots.
- [x] 2.3 Document the recommended mapping from common user intents to plugin/action pairs, especially for overlapping launch and app-control workflows.

## 3. Internal System Plugin Contract

- [x] 3.1 Refactor `SystemCommandPlugin` to expose explicit metadata-defined actions instead of relying on wrapper verbs plus nested `command` arguments.
- [x] 3.2 Preserve compatibility for existing saved system-command slots while ensuring new slots can reference the canonical action directly.
- [x] 3.3 Update system-plugin documentation and any authoring surfaces so internal Pulsar controls follow the same action contract as other built-in plugins.

## 4. Validation

- [x] 4.1 Add or update tests for canonical action exposure, alias execution, and metadata-driven action lists for built-in plugins.
- [x] 4.2 Validate that existing built-in plugin slots still load and execute while new slot authoring surfaces show only canonical names and actions.
- [ ] 4.3 Run the relevant build and test validation, and manually verify the plugin picker plus slot editor reflect the unified naming and usage model.
