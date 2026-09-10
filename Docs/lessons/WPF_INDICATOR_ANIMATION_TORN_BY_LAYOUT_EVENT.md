# Custom-Drawn Indicator Animation Torn Down by Layout-Event Reposition

**Status**: Published
**Scope**: Lesson
**Applies To**: `Views/SettingsWindow.xaml.cs` — the self-drawn `NavIndicator`; and any UI where a **multi-phase animation** coexists with **event-driven repositioning** of the same element
**Last Updated**: 2026-09-10

---

## Rule (TL;DR)

**An animation gate must cover every entry point that writes the animated element — not just the layout callback.** And **pin the animation's origin explicitly (`From=`) instead of relying on `From=null`'s implicit origin.**

Two invariants in `SettingsWindow`:

1. `_isNavAnimating` **must be checked at the top of every deferred write path**. A single unguarded path is enough to destroy the animation:

```csharp
// ❌ one path guarded, the other not
private void OnNavPaneLayoutUpdated(object? s, EventArgs e)
{
    if (_isNavAnimating) return;          // guarded ✅
    RepositionNavIndicatorImmediate(clearHeldAnimations: true);
}

private void RepositionNavIndicator()      // DPI / pane open-close / size change / nav rebuild
{
    _ = Dispatcher.InvokeAsync(() => {
        InitializeNavIndicator();         // ❌ unguarded → clearHeldAnimations:true kills the animation
    }, DispatcherPriority.Render);
}
```

The deferred callback must re-check the flag **at execution time** (the event may fire *before* the animation starts and run *during* it):

```csharp
_ = Dispatcher.InvokeAsync(() => {
    if (_isNavAnimating) return;          // ✅ animation owns the indicator
    InitializeNavIndicator();
}, DispatcherPriority.Render);
```

2. An in-animation degrade path that *deliberately* gives up must **release the gate** (`_isNavAnimating = false`) before deferring a reposition — otherwise the guard it just introduced swallows its own fallback.

3. **Never let two writers race on an animation's origin.** `DoubleAnimation(to, duration)` (`From = null`) resolves its origin from the property's *current effective* value at `BeginAnimation` time. A concurrent base-value write between your measurement and `BeginAnimation` silently re-targets the animation start:

```csharp
var startTop = Canvas.GetTop(NavIndicator);      // ✅ capture, then pass explicitly
new DoubleAnimation(startTop, stretchTop, duration);
// phase 2 must continue from phase 1's landing point, not from a re-read base:
new DoubleAnimation(stretchTop, newCenterY, duration);
```

Side benefit: explicit `From` also removes the `AnimationException` crash channel when the property is still `NaN` (see the `[FIX 2026-09-09]` note in `SettingsWindow.xaml.cs`).

---

## Symptom (as reported 2026-09-10)

> tab indicator 正常来说会随着 tab 切换而触发动画，但是从其他 tab 切换到第一个 tab 时始终是瞬态，其他 tab 之间切换都有动画。

"Always instant when entering the first tab, but animated everywhere else" looks like a page-specific layout cost. It is not: the page was merely the one whose navigation **raised a layout event**.

---

## Root Cause

The indicator is a two-phase animation (stretch → snap, 120 ms + 130 ms) driven from `ShellViewModel_PropertyChanged`. `SettingsWindow` repositions it on four event sources, and **three were not animation-aware**. Navigating to the affected page raised a burst of `Window.DpiChanged` (7 events in 23 ms), each queuing a deferred reposition; the first one ran 1 ms after phase 1 began:

```
24.363 [NAV] old='Appearance' new='Slots' animating=False
24.388 [REPOS] dpi-changed animating=True      ← ×7, deferred repositions queued
24.469 [ANIM] PHASE1 baseTop=207.0 → stretchTop=69.7 stretchH=159.3
24.470 [SNAP] caller=InitializeNavIndicator top=207.0→69.7 clearAnim=True animating=True   ← BeginAnimation(prop, null)
24.599 [ANIM] PHASE2 atTop=69.7 atH=22.0       ← already at the target: zero visible motion
```

`clearHeldAnimations: true` exists to strip a *previous* animation's `HoldEnd` residue before writing base values. During a live animation it is destructive: it removes the running animation and the indicator snaps to its target in one frame — the "瞬态".

**Machine-checkable signature** of the defect: a `[SNAP] … clearAnim=True animating=True` line, or a phase-2 entry whose `atH` is already the collapsed height (`22.0`) instead of the stretched height.

---

## Fix

- `RepositionNavIndicator()`: `if (_isNavAnimating) return;` in the deferred callback. The animation's `finally` settles the base value and `OnNavPaneLayoutUpdated` self-heals afterwards, so nothing is lost.
- Both in-animation degrade paths (`boundsInvalid`, `NaN centers`) set `_isNavAnimating = false` before deferring — they have relinquished animation ownership.
- Phase 1/2 origins pinned with explicit `From` (`startTop/startHeight`, then `stretchTop/stretchHeight`).

Result: the indicator survives an arbitrary storm of layout events mid-animation; the events are dropped and the post-animation self-heal pins the final position.

---

## Verification (headless-safe)

This class of defect cannot be eyeballed reliably, so verify with **event injection + the same E2E driver before/after**:

1. Drive settings navigation with `Pulsar.E2E` `click` steps on `Pulsar.Settings.Nav.*` automation ids (real `SendInput` mouse input → same `PreviewMouseLeftButtonUp` path as a human).
2. Inject `NavPaneGrid.SizeChanged` mid-animation by nudging the settings window with `SetWindowPos(h, …, SWP_NOZORDER|SWP_NOACTIVATE)` every ~55 ms (`artifacts/nav-resize-inject.ps1`).
3. Compare the two counters, which are driver-independent:

| Counter | Pre-fix | Post-fix |
|---|---|---|
| `clearAnim=True animating=True` | 3 | **0** |
| `PHASE2 … atH=22.0` (collapsed on entry) | 2 | **0** |
| guard hits (`SKIP animating-suppressed`) | — | 36 of 280 injected events |
| navigations completing both phases | partial | **8/8** |

---

## Related Documents

- [WPF Theme Injection Pitfalls](./WPF_THEME_INJECTION_PITFALLS.md) — same window, same event sources (`DpiChanged` / pane state), different symptom (misalignment rather than a dead animation).
- [Settings Page Panels Sized by Content Instead of Viewport](./WPF_SETTINGS_PANEL_WIDTH_CONTENT_DRIVEN.md) — sibling defect from the same settings-tab migration.

**Change History**:
- v1.0.0 (2026-09-10): Initial version
