## Context

Pulsar already supports slot-scoped execution arguments through `PluginSlot.Args` and the plugin execution contract `ExecuteAsync(action, args, context)`, but the settings experience for authoring those arguments is almost entirely hand-built and plugin-specific. `SettingsSlotsPage.xaml` currently chooses one hard-coded template per plugin type, exposes only a subset of supported arguments, and relies on short placeholders or transient notifications to explain configuration. Most failures are only surfaced when a plugin executes and returns runtime errors such as missing required parameters.

The codebase already contains several pieces that should become the foundation for a better solution:

- Plugin metadata registration exists through `PluginMetadata`, `ConfigSchema`, `PropertySchema`, and `PluginMetadataRegistry`.
- Config validation already consumes plugin metadata schemas, but only for plugin-level configuration under `config.Plugins`.
- Multiple built-in plugins already have action-specific argument contracts in code, including WinSwitcher (`switch`, `launch`, `activate`) and SimpleCommand (`run`, `sendkeys`).

This change is cross-cutting because it touches plugin metadata modeling, settings UI composition, validation, and built-in plugin definitions. It also needs a careful migration path so existing `Profiles.json` slot definitions continue to work.

## Goals / Non-Goals

**Goals:**
- Define a formal metadata contract for slot action parameters that becomes the single source of truth for labels, help text, required state, examples, and validation hints.
- Replace the current plugin-specific slot form branching with an action-aware parameter authoring model that can render built-in plugin slot parameters consistently.
- Preserve compatibility with existing stored slot args while improving discoverability and guidance in the editor.
- Move common parameter mistakes earlier in the flow through inline and save-time validation.
- Normalize built-in plugin parameter vocabulary where the current metadata, UI, and runtime names diverge.

**Non-Goals:**
- Fully redesigning the entire Settings UI outside of slot editing.
- Replacing plugin runtime validation; execution-time checks remain required as a safety boundary.
- Building a generic third-party plugin marketplace authoring SDK in this change.
- Converting every possible slot-related visual affordance to metadata-driven rendering in one pass; shared appearance fields can remain as they are.

## Decisions

### 1. Add action-level parameter metadata to plugin metadata

Decision:
- Extend plugin metadata with explicit action definitions, each containing parameter definitions for slot args.

Rationale:
- The existing `ConfigSchema` describes plugin-level configuration, not per-slot action inputs. Reusing it directly would blur two separate concepts: global plugin settings and action execution arguments.
- Action-level metadata lets the UI understand that `WinSwitcher.switch` and `WinSwitcher.launch` do not require the same fields.

Alternatives considered:
- Continue hard-coding XAML templates per plugin: rejected because it scales poorly and keeps knowledge trapped in the settings page.
- Reuse `ConfigSchema` for slot args with no new type: rejected because it does not model actions, picker hints, or slot-specific UX semantics clearly enough.

### 2. Keep plugin execution contract unchanged

Decision:
- Retain `ExecuteAsync(action, args, context)` and adapt the editing/validation layers around it rather than changing runtime plugin invocation.

Rationale:
- The execution contract is already flexible and widely used across built-in plugins.
- The current problem is primarily discoverability, authoring, and validation, not the transport format itself.

Alternatives considered:
- Replace `args` dictionaries with strongly typed request objects per plugin: rejected for this change because it would be invasive, require broad plugin refactoring, and is not necessary to solve the UX issue.

### 3. Treat the metadata contract as the source for editor rendering and validation hints

Decision:
- Use the new slot parameter metadata in two places: the slot editor for form generation/presentation and the validation pipeline for slot argument validation.

Rationale:
- A single source of truth prevents drift between what the UI says and what validators enforce.
- The current system already demonstrates schema-driven validation for plugin config; extending that pattern to slots creates consistency.

Alternatives considered:
- Put help text in UI only and keep validation separate: rejected because it would recreate the current drift problem.

### 4. Adopt a hybrid rendering strategy for this change

Decision:
- Move slot parameter fields to metadata-driven rendering, but keep certain specialized flows as explicit integrations where needed, such as secret selection or process picking.

Rationale:
- Some parameters are best edited through assisted pickers, not generic text boxes.
- A hybrid approach allows metadata to declare picker intent while the UI maps known intents to existing dialogs and helpers.

Alternatives considered:
- Require all parameters to use generic controls only: rejected because it would degrade UX for secrets, file paths, and process selection.
- Keep all specialized controls hard-coded per plugin: rejected because it preserves the current maintenance problem.

### 5. Preserve backwards compatibility for stored args

Decision:
- Existing slot args in saved profiles continue to load unchanged. Any terminology normalization must support legacy aliases during validation/execution or include a migration path in config save/load.

Rationale:
- Users may already have saved slots that must not silently break.
- The current code already shows drift such as `autoSubmit` in metadata versus `autoEnter` at runtime; the design must reconcile naming without invalidating existing data.

Alternatives considered:
- Hard break on renamed parameters: rejected because it would create avoidable regressions.

### 6. Introduce explicit parameter presentation tiers in the editor

Decision:
- Parameters are grouped and presented as required, optional, and advanced where metadata indicates it.

Rationale:
- Current flat text-box stacks do not communicate importance or priority.
- Grouping reduces cognitive load and helps users fill the minimum required configuration first.

Alternatives considered:
- Flat alphabetical parameter list: rejected because it optimizes implementation convenience, not usability.

## Risks / Trade-offs

- [Metadata model becomes too abstract] -> Keep the first version intentionally small: action definitions, parameter definitions, validation hints, examples, and picker intents only.
- [UI generation reduces flexibility for highly custom plugins] -> Support explicit picker intents and allow selective bespoke rendering hooks for exceptional parameters.
- [Validation becomes inconsistent with runtime behavior] -> Keep runtime checks in plugins and ensure built-in plugin definitions are updated alongside metadata in the same change.
- [Backward compatibility regressions in saved configs] -> Add alias handling or migration for renamed parameters and validate against real example profiles before rollout.
- [Scope expansion into full plugin platform redesign] -> Limit the first rollout to slot authoring for built-in plugins already present in the repository.

## Migration Plan

1. Add the new slot action metadata types and registry access patterns without removing the current slot editor.
2. Define metadata for the first set of built-in plugins: WinSwitcher, SimpleCommand, PKI, BookmarkletRunner, and VbaRunner.
3. Update the slot editor to read action metadata and render parameter sections, while preserving current shared slot appearance editing.
4. Add slot-argument validation to the save pipeline and keep runtime plugin validation intact.
5. Reconcile naming mismatches and support any required legacy aliases during load/validation.
6. Remove obsolete hard-coded parameter templates once parity is confirmed.

Rollback strategy:
- Revert the settings editor to the previous static templates and ignore slot parameter metadata; because the runtime execution contract remains unchanged, stored slot args remain usable.

## Open Questions

- Should all built-in plugins be required to implement action parameter metadata, or should metadata-driven slot editing initially support only a curated subset with a fallback editor?
- Should unknown third-party plugin parameters fall back to a raw key/value editor, or remain unsupported in the first release?
- Where should alias translation for renamed parameters live most cleanly: config load/save, validation layer, or plugin runtime shim?
