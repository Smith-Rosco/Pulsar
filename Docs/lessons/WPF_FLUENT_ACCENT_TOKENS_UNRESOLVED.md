# WPF-UI Fluent Accent Tokens Unresolvable in Multi-Headed UI

**Status**: Published  
**Scope**: Lesson  
**Applies To**: `ThemeService.ApplyAccent`, `Styles/ButtonStyles.xaml`, Wpf.Ui 4.3.0 `ApplicationAccentColorManager` / `UiApplication`  
**Last Updated**: 2026-09-01

---

## Rule (TL;DR)

**Wpf.Ui's `Accent*` Fluent tokens (`AccentFillColorDefaultBrush`, `AccentTextFillColorPrimaryBrush`, `SystemAccentBrush`, ...) are NOT part of `ThemesDictionary` and are NOT resolvable unless your `Application` merges a `"wpf.ui;"`-namespaced dictionary.** In a plain `System.Windows.Application` that merges only its own dictionaries (Pulsar's Multi-Headed `App.xaml`), `ApplicationAccentColorManager.Apply*()` writes into a detached dictionary, so every `{DynamicResource Accent*}` reference fails silently. `ThemeService` must **bridge** the injected values into `Application.Current.Resources` (see `ThemeService.BridgeAccentResources`).

Also: **`AccentTextFillColorPrimaryBrush` is a shade of the accent colour itself** (it is for accent-coloured *text* like links). Text sitting **on** an accent fill must use `TextOnAccentFillColorPrimaryBrush` (white in Light, black in Dark — flips with the fill's brightness).

---

## Symptom

- After the Fluent UX refactor (`c5c3592`), buttons lost readable contrast again: "蓝底蓝字 / 红底蓝字" (blue on blue, blue on red).
- Primary buttons render with fallback greys and black text (when the tokens are missing), or accent-on-accent (once the tokens resolve but the wrong text token is used).
- Every accent-coloured affordance degrades silently: selected nav item, segmented controls, SlotOrb badge, plugin-card hover borders, nav indicator.

---

## Root Cause (two independent layers)

### Layer 1 — tokens never resolve

Wpf.Ui 4.3.0's `ApplicationAccentColorManager.UpdateColorResources` writes the `Accent*` brushes into `UiApplication.Current.Resources`. `UiApplication.Current` only *binds* your `Application` if `ApplicationHasResources` returns true:

```csharp
// Wpf.Ui/Controls/UiApplication.cs (4.3.0)
private static bool ApplicationHasResources(Application application) =>
    application.Resources.MergedDictionaries.Any(e =>
        e.Source?.ToString().Contains("wpf.ui;", StringComparison.OrdinalIgnoreCase) == true);
```

Pulsar's `App` derives from `System.Windows.Application` and `App.xaml` intentionally merges **no** Wpf.Ui dictionary (Multi-Headed UI global-style isolation). So `_application` stays null and `UiApplication.Current.Resources` returns a private, **detached** dictionary that no window, dialog or context-menu ever resolves. `DynamicResource` fails silently on a missing key — no error, no log. Verified at runtime: after the real `ThemeService.Initialize` on a production-faithful `Application`, every `Accent*` key is MISSING from `Application.Current.Resources`, and `ThemesDictionary` (window level) does not define them either.

### Layer 2 — wrong token semantics

Even when the tokens resolve, the button text used `AccentTextFillColorPrimaryBrush`, which the manager sets to `secondaryAccent` — a shade of the accent colour:

```csharp
// Wpf.Ui/Appearance/ApplicationAccentColorManager.cs (4.3.0)
UiApplication.Current.Resources["AccentTextFillColorPrimaryBrush"] = secondaryAccent.ToBrush();
```

The correct "text on accent fill" token is `TextOnAccentFillColorPrimaryBrush`, which Wpf.Ui's `ThemesDictionary` DOES provide natively (`#FFFFFFFF` Light / `#FF000000` Dark) and which flips to match the accent fill's brightness per theme.

### Why the regression test didn't catch it

`ButtonThemeContrastTests.BuildLightTheme()` hand-seeded `AccentTextFillColorPrimaryBrush = White` and `AccentFillColorDefaultBrush = #0067C0` — values the runtime never provides. The test passed while production was broken (a stale "correct seam").

---

## Correct Pattern

```csharp
// ThemeService.ApplyAccent — after ApplicationAccentColorManager.Apply*():
foreach (System.Collections.DictionaryEntry entry in Wpf.Ui.UiApplication.Current.Resources)
{
    Application.Current!.Resources[entry.Key] = entry.Value;
}
```

```xml
<!-- Text on an accent-fill button (NOT AccentTextFillColorPrimaryBrush) -->
<Setter TargetName="TextPresenter" Property="TextElement.Foreground"
        Value="{DynamicResource TextOnAccentFillColorPrimaryBrush}"/>
```

## Incorrect Pattern

```xml
<!-- ❌ WRONG: accent-coloured text on an accent fill = unreadable -->
<Setter TargetName="TextPresenter" Property="TextElement.Foreground"
        Value="{DynamicResource AccentTextFillColorPrimaryBrush}"/>
```

---

## Audit (2026-09-01)

- `ThemeService.ApplyAccent` → added `BridgeAccentResources` (bridges runtime accent keys into `Application.Current.Resources`).
- `Styles/ButtonStyles.xaml` → all 7 `AccentTextFillColorPrimaryBrush` uses (Primary + Danger text) → `TextOnAccentFillColorPrimaryBrush`.
- `Views/Controls/SlotOrb.xaml` badge text → `TextOnAccentFillColorPrimaryBrush` (sits on `AccentFillColorDefaultBrush`).
- `Views/Pages/SettingsPluginsPage.xaml` + `SettingsExternalPluginsPage.xaml` → `SystemAccentColorBrush` → `SystemAccentBrush` (the manager injects only `SystemAccentBrush`).
- `Views/Dialogs/Contents/PluginSettingsDialogContent.xaml` toggle kept `AccentTextFillColorPrimaryBrush` — correct: accent-coloured text on an accent-*tinted* (`SystemFillColorAccentBackground3`) background.
- Regression test: `ThemeServiceTests.Initialize_ShouldMakeAccentBrushesResolvableAtApplicationLevel` (asserts the bridge) + `ButtonThemeContrastTests` no longer seeds the fake `AccentTextFillColorPrimaryBrush`.

## Gotchas

- Creating an `Application` in a test is **AppDomain-wide singleton** — "Cannot create more than one System.Windows.Application instance". Reuse the existing guard: `if (Application.Current == null) _ = new Application();`.
- The bridge must run on the thread that owns `Application.Current` (the main UI thread).

---

## Related Documents

- [WPF Button Template Frozen Foreground](./WPF_BUTTON_TEMPLATE_FROZEN_FOREGROUND.md) — the earlier button-readability regression (ContentPresenter text freeze).
- [WPF-UI Button Appearance="Primary" Bug](./WPFUI_BUTTON_PRIMARY_BUG.md) — why Pulsar styles hardcode template triggers instead of relying on dynamic accent inheritance.

---

**Change History**:
- v1.0.0 (2026-09-01): Initial version
