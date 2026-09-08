# Settings Page Panels Sized by Content Instead of Viewport

**Status**: Published
**Scope**: Lesson
**Applies To**: `Views/Pages/Settings*.xaml` — any `ScrollViewer` content panel
**Last Updated**: 2026-09-08

---

## Rule (TL;DR)

**A settings page's `ScrollViewer` content must be sized by the viewport, never by its own content.** Writing

```xml
<ScrollViewer VerticalScrollBarVisibility="Auto">
    <StackPanel MaxWidth="700" HorizontalAlignment="Left">   <!-- ❌ -->
```

makes the panel shrink to `min(widest child's desired width, MaxWidth)`. Two visible defects follow:

1. Cards never fill the available area (they stop at whatever the widest collapsed card needs).
2. **Expanding or collapsing any card re-widens every card on the page** — the widest child changed, so the panel's width changed.

Use `HorizontalAlignment="Stretch"` (keep `MaxWidth` purely as a reading-width cap), or bind the width to the viewport the way `SettingsPluginsPage` already does:

```xml
<ScrollViewer x:Name="PluginsScroll" HorizontalScrollBarVisibility="Disabled">
    <Grid Width="{Binding ElementName=PluginsScroll, Path=ViewportWidth}">   <!-- ✅ -->
```

---

## Symptom (as reported 2026-09-08)

> 关于页，卡片的宽度没有占满视图，折叠和展开卡片的时候，会导致所有卡片的宽度变化，卡片内容越多，宽度变化越明显。

The second half is the tell: "expanding changes *every* card's width". A per-card bug would only move one card; a *shared* width that reacts to one card means an ancestor is being measured from its content.

---

## Root Cause

`ScrollViewer` with `HorizontalScrollBarVisibility="Disabled"` (the default) measures its child with the **viewport width** as the constraint — that part is correct. But `HorizontalAlignment="Left"` overrides the child's `Stretch` behaviour, so the panel answers that constraint with its own *desired* width (content-driven) and the leftover space stays empty.

`StackPanel`'s desired width is `max(child desired widths)`. `CardExpander`/`CardControl` children stretch to whatever the panel gives them, so they all inherit the moving target.

`MaxWidth` only caps the result; it does not make the panel absorb the viewport.

---

## Correct Pattern

```xml
<ScrollViewer VerticalScrollBarVisibility="Auto">
    <StackPanel MaxWidth="700" HorizontalAlignment="Stretch">   <!-- ✅ -->
```

## Incorrect Pattern

```xml
<StackPanel MaxWidth="700" HorizontalAlignment="Left">         <!-- ❌ -->
```

---

## Why Tests Missed It

There is no headless seam for rendered layout in this repo: the test host has no desktop session, so `Window.Show()` yields `ActualWidth == 0` and WPF-UI `VisualState` animations never run (verified 2026-09-08 — an STA probe with a real `Window` + `DispatcherFrame` pump still measured 0). Layout regressions therefore have to be locked with **static XAML scans**, not rendered assertions.

Guard added: `Pulsar.Tests.UI.SettingsLayoutGuardTests.Settings_pages_do_not_size_scroll_content_to_content` — flags any `StackPanel`/`Grid`/`Border`/`ItemsControl` in `Views/Pages/*.xaml` that carries **both** `HorizontalAlignment="Left"` and `MaxWidth`. The attribute check is deliberately order-independent: both defects declared `MaxWidth` *before* `HorizontalAlignment`, which a single left-to-right regex silently misses.

---

## Audit (2026-09-08)

| Page | Before | After |
|---|---|---|
| `SettingsAboutPage.xaml` | `MaxWidth="700" HorizontalAlignment="Left"` | `HorizontalAlignment="Stretch"` |
| `SettingsAnalyticsPage.xaml` | `MaxWidth="720" HorizontalAlignment="Left"` | `HorizontalAlignment="Stretch"` |
| `SettingsGeneralPage.xaml` | no alignment set (already Stretch) | unchanged ✅ |
| `SettingsPluginsPage.xaml` | `Width="{Binding ViewportWidth}"` (pre-existing workaround) | unchanged ✅ |
| `SettingsSlotsPage.xaml` | no content-sized panel | unchanged ✅ |

---

## Related Documents

- [WPF-UI Fluent Accent Tokens Unresolvable](./WPF_FLUENT_ACCENT_TOKENS_UNRESOLVED.md) — the sibling defect fixed in the same pass (hardcoded `White` text on accent fills).

---

**Change History**:
- v1.0.0 (2026-09-08): Initial version
