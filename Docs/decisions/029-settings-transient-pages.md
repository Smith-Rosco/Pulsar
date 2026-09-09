# ADR-029: Settings transient pages (dynamic settings tabs)

**Status**: Accepted
**Date**: 2026-09-08
**Deciders**: Project owner (milo), via grilling session (auto-with-guardrails, audit approved) + openspec change `2026-09-08-dynamic-settings-tabs`

---

## Context

Three settings-surface problems (see `Docs/planning/2026-09-08-dynamic-settings-tabs-research.md`):

1. **Card overload.** The right-drag gesture config lived in one `CardExpander` on
   `SettingsGeneralPage` (~30 controls, 9 `SettingsRow`s, up to 7 nesting levels,
   184 XAML lines) — beyond what an expandable card can express clearly.
2. **Modal fatigue.** Heavy configuration (slot editor, plugin settings, process
   blacklist) runs as modal content over the settings window, breaking workflow.
3. **Appearance underpowered.** Appearance was 4 rows inside General.

Meanwhile the settings shell (`SettingsWindow`) already builds navigation items
from `SettingsPageCatalog` in code-behind, hosts pages in a `Frame`, guards
navigation via `SettingsNavigationGuard`, and persists through the single-writer
`SettingsEditorSession`. The missing piece is a way to add/remove sidebar pages at
runtime with bounded tab count.

Industry reference: VS Code preview tabs (per-slot reuse, italic temporary tabs),
Raycast per-extension settings tabs, and the "avoid accordion-as-navigation" /
settings-registry consensus.

## Decision

1. **Transient pages** are first-class registrations (`SettingsPageRegistration.IsTransient`)
   in the same catalog as permanent pages. They are registered at runtime
   (`RegisterTransient`, appended at the end of their semantic group) and
   unregistered when recycled.
2. **Per-type singleton** (VS Code preview semantics): triggering an already-open
   transient type activates the existing instance instead of creating a new one.
   Tab count is bounded by the number of registered transient types. Entity-scoped
   ids (`<type>:<entityId>`) are deferred to P3 (slot editor).
3. **Recycle on clean leave**: navigating away from a transient page with no
   unsaved changes removes its nav entry and destroys its page instance.
   Dirty transient pages stay open (no prompt on leave); unsaved state is surfaced
   once at window close via the existing `CanCloseAsync` guard flow. This carves
   transient sources out of the existing `settings-dirty-state-guard` prompt-on-navigate
   rule (spec delta MODIFIED, not silently overridden).
4. **Visual distinction**: transient entries render italic with a hover close
   button. Closing a dirty tab asks save/discard/cancel (reuses the guard flow).
5. **Session scope**: transient tabs are never restored after the settings window
   closes; "last opened page" preference never records transient ids.
6. **Coordination seams**: `ITransientPageService` (register definitions / open /
   close / notify-navigated-away) composes `SettingsPageCatalog` + `SettingsShellViewModel`
   + `ISettingsNavigationGuard` without cycles — the shell does not depend on the
   service; the window drives recycle on page change. Page construction goes
   through a registry-based `SettingsPageFactory` (switch removed).
7. **Migrations**: P0 = mechanism + gesture page (`SettingsGesturePage`, card keeps
   toggle + detail-entry button). P1 = appearance becomes a permanent standalone
   page (`SettingsAppearancePage`, swatch grid for theme presets; language stays in
   General). P2 (plugin settings / process blacklist) and P3 (slot editor) are
   future changes. One-off pickers and confirmations remain modal.
8. **No page-level lifecycle interface in P0** (`ISettingsPageLifecycle` deferred);
   page instances are rebuilt on re-open, so state resets naturally.

## Consequences

- `Profiles.json` format unchanged; single-writer session untouched.
- Global dirty flag means edits made on any page keep all transient tabs open —
  conservative (never loses data); per-page dirty splitting is deferred to P2.
- `OnLanguageChanged` rebuild and transient add/remove share one rebuild path
  (`RebuildNavigationPreservingSelection`), so transient entries survive language
  switches.
- Spec deltas recorded in openspec change `2026-09-08-dynamic-settings-tabs`
  (new `settings-transient-pages`; modified `settings-shell-navigation`,
  `settings-dirty-state-guard`).
- Known deferred work: per-page dirty state (P2), entity-scoped transient ids (P3),
  deep-link entries into settings (API already supports it).

## Addendum (2026-09-09): Slot editor P3 — modal retirement

Via openspec change `unify-slot-editor-transient-pages` (tasks 4.1–4.2). The
entity-scoped transient ids deferred above (Decision §2) are now the only slot
editing surface:

- **Entry points** (`SettingsViewModel.AddSlotDialog` / `OpenSlotConfiguration`)
  open entity tabs exclusively: edit = `slot-editor:<ctx>:<slotNo>` (live
  `PluginSlot`, window-bottom save via the shared dirty chain), create =
  `slot-editor:<ctx>:draft` (two-step wizard, in-place commit converts the tab
  to an edit tab). The `UseTransientSlotEditor` fallback flag and both modal
  fallback branches were deleted.
- **Template registry**: the `SlotEditorViewModel → AddSlotContent` DataTemplate
  in `Themes/DialogTemplates.xaml` was removed; `SettingsSlotEditorPage`
  instantiates the content controls directly. `DialogTemplateRegistrationTests`
  gains an explicit `NonModalVmExemptions` list (with rationale) instead of a
  silent skip — future re-migrations must consciously edit it.
- **Reuse, not deletion**: `AddSlotContent` / `SlotConfigurationDialogContent`
  remain as content controls embedded in the page (with their self-contained
  `ui:ControlsDictionary` merge — see `Docs/lessons/` on implicit-style
  resolution). Only the modal hosting flow died.
- **Scope discipline on resx cleanup**: full-scan found ~296 candidate orphan
  keys, but most are convention-lookup keys (`SlotParam.*` / `SlotAction.*` /
  `PluginPermission.*`, resolved via `SlotParam.{AlphaNumOnly(Label)}` style
  name building). Only the two keys orphaned by this change
  (`Notification.CreateSlot`, `Notification.EditSlotFormat`) were removed; a
  dedicated audit change is required before touching the rest.
