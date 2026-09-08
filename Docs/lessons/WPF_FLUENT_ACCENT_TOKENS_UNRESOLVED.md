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

## Audit (2026-09-08) — hardcoded `White` text on coloured fills

The 2026-09-01 audit converted the `AccentTextFillColorPrimaryBrush` uses, but four places still hardcoded the literal `White`, which is only correct in Light. In Dark the accent fill is `#4CC2FF` (light cyan) and the critical fill is a light red — white text on those measures ≈1.7:1 contrast, i.e. **invisible**.

User-visible symptom: on the analytics page the Top-3 rank badges showed their accent circle but no number, while `#4`/`#5` (grey fill + secondary text) stayed readable.

| File | Fill | Before | After |
|---|---|---|---|
| `Views/Pages/SettingsAnalyticsPage.xaml` (Top-3 rank badge) | `SystemFillColorAccentBrush` | `Value="White"` | `{DynamicResource TextOnAccentFillColorPrimaryBrush}` |
| `Views/Dialogs/DialogHostWindow.xaml` (danger tertiary button) | `SystemFillColorCriticalBrush` | `Value="White"` | `{DynamicResource TextOnAccentFillColorPrimaryBrush}` |
| `Views/Dialogs/Contents/AddSlotContent.xaml` (segmented checked) | `AccentFillColorSecondaryBrush` | `Value="White"` | `{DynamicResource TextOnAccentFillColorPrimaryBrush}` |
| `Views/Dialogs/Contents/SlotConfigurationDialogContent.xaml` (segmented checked) | `AccentFillColorSecondaryBrush` | `Value="White"` | `{DynamicResource TextOnAccentFillColorPrimaryBrush}` |

`TextOnAccentFillColorPrimaryBrush` is the right token for **any** coloured fill, not just the accent: WPF-UI ships it as white-in-Light / black-in-Dark, which flips correctly against both the accent and the critical fill.

### Fallout: a stale test premise

`ButtonThemeContrastTests.SegmentedRadioButton_CheckedAfterFirstLayout_UpdatesTextColor` asserted `R > 180` on a RadioButton rendered in an **unthemed** isolated tree. That only worked because the value was a literal; once it became a `DynamicResource`, an unthemed tree resolves nothing. Fixed by merging `BuildLightTheme()` into the isolated root — the test now exercises the real resource path instead of a literal.

### Regression guard

`Pulsar.Tests.UI.SettingsLayoutGuardTests.No_xaml_foreground_is_hardcoded_to_white_or_black` scans every XAML file for `Foreground="White|Black|#FFFFFFFF|#FF000000"` and the `<Setter Property="Foreground" Value="…">` equivalent, so the whole class cannot come back.

## Audit (2026-09-08, second pass) — the fill token itself was dead

The first pass fixed the *text* colour and still the badges stayed invisible. Measurement (`TryFindResource` against the themed page resource chain, both themes) shows why:

| Token | Light | Dark |
|---|---|---|
| `SystemFillColorAccentBrush` | **`<null>`** | **`<null>`** |
| `AccentFillColorDefaultBrush` | `<null>` until the runtime bridge injects it | `#003E92` (ControlsDictionary default) |
| `AccentTextFillColorPrimaryBrush` | `<null>` until injected | `#003E92` |
| `TextOnAccentFillColorPrimaryBrush` | `#FFFFFFFF` | `#FF000000` |
| `SystemFillColorCriticalBrush` | `#C42B1C` | `#FF99A4` |

**`SystemFillColorAccentBrush` is not a WPF-UI 4.3.0 key at all.** The analytics page painted the Top-3 rank badges and three bar fills with it, so every one of them rendered with *no fill*: the badge showed white text directly on the light card. Fixing the text colour could not help while the background was still transparent — the text colour was a no-op in Light (white → white).

### Rule

- **Accent fill** → `AccentFillColorDefaultBrush`; **accent-coloured text on a normal background** → `AccentTextFillColorPrimaryBrush`; **text on an accent fill** → `TextOnAccentFillColorPrimaryBrush`. All three are injected at runtime by `ThemeService.ApplyAccent` + `BridgeAccentResources`, which is the same pairing `PulsarPrimaryButtonStyle` and the `SlotOrb` badge use.
- `SystemFillColor*` only covers the *status* family (`Success` / `Caution` / `Critical`) — there is no `SystemFillColorAccent*` in 4.3.0.
- Dead `SystemFillColor*Accent*` references are now blocked by `SettingsLayoutGuardTests.No_xaml_references_accent_tokens_that_do_not_resolve`.

### Third Pass (2026-09-08): element-local dictionaries shadow the runtime bridge

Even after migrating to `AccentFillColorDefaultBrush`, the rank badges were **still invisible on real hardware** — while every test/probe passed. Root cause: lookup order.

`ThemeService.ApplyStandardTheme` merges a **element-local** `ThemesDictionary` + `ControlsDictionary` into each Window/Page. WPF resolves element-local dictionaries **before** `Application.Current.Resources`. So:

- In **Light**, the static `ThemesDictionary` does not contain `AccentFillColorDefaultBrush` at all → `{DynamicResource AccentFillColorDefaultBrush}` on the element resolves nothing, even though the runtime bridge has a valid value sitting in `Application.Current.Resources`.
- `TextOnAccentFillColorPrimaryBrush` *does* exist statically (white-in-Light / black-in-Dark), but the runtime manager recomputes it from actual accent brightness — a yellow Windows accent in Light theme would render white text on a yellow fill = invisible.

Fix: `ThemeService.ApplyStandardTheme` now ends with `CopyRuntimeAccentResources(element.Resources)` — copying every accent-family key from `UiApplication.Current.Resources` onto the element, putting them back on top of the lookup chain. Guarded by `ThemeServiceTests.ApplyTheme_ShouldCopyRuntimeAccentResourcesOntoElement`.

### Fourth Pass (2026-09-08): local attribute beats style trigger — the badge was never turning accent

After the copy-onto-element fix the badges were **still** invisible on real hardware. Final root cause, and it was plain WPF dependency-property precedence, not tokens at all:

```xml
<Border ... Background="{DynamicResource ControlFillColorDisabledBrush}">   <!-- local value -->
    <Border.Style>
        <Style TargetType="Border">
            <Style.Triggers>
                <DataTrigger Binding="{Binding IsTopThree}" Value="True">
                    <Setter Property="Background" Value="{DynamicResource AccentFillColorDefaultBrush}"/>  <!-- dead -->
```

WPF precedence: **local value > style trigger > style setter**. The local `Background` attribute silently overrode the trigger, so the badge stayed light grey forever; the *text* trigger (separate TextBlock, no local attribute) worked fine and switched Top-3 text to `TextOnAccentFillColorPrimaryBrush` — white text on a light grey badge. Fix: delete the local attribute, keep the default as a Style Setter. #4/#5 always looked right because their default text (`TextFillColorSecondaryBrush`) has contrast on grey.

Three lessons in one bug: (1) every earlier probe checked *token resolution*, none checked *effective value precedence*; (2) when "trigger doesn't apply" and "token doesn't resolve" look identical from the outside, grep for a local attribute with the same property name before blaming DynamicResource; (3) a repo-wide scan (`local Background/Foreground attr immediately above a `.Style` element with a same-property trigger`) found no other instances.

### Related: CardControl puts Content in the right Auto column

`CardControl`'s template is 3 columns `Auto | * | Auto` (Icon | Header | Content). Anything placed in `Content` renders in the **rightmost Auto column** (right-aligned, shrunk to fit). `HorizontalContentAlignment="Stretch"` cannot fix this — the column itself is `Auto`. Left-aligned full-width content must go into the **`Header`** slot (`<ui:CardControl.Header>`). This is what the About page "app identity" card needed (three rounds of user feedback before the real mechanism was found).

## Related Documents

- [WPF Button Template Frozen Foreground](./WPF_BUTTON_TEMPLATE_FROZEN_FOREGROUND.md) — the earlier button-readability regression (ContentPresenter text freeze).
- [WPF-UI Button Appearance="Primary" Bug](./WPFUI_BUTTON_PRIMARY_BUG.md) — why Pulsar styles hardcode template triggers instead of relying on dynamic accent inheritance.
- [Settings Page Panels Sized by Content](./WPF_SETTINGS_PANEL_WIDTH_CONTENT_DRIVEN.md) — the sibling defect fixed in the same pass (viewport vs. content width).

---

**Change History**:
- v1.0.0 (2026-09-01): Initial version
- v1.1.0 (2026-09-08): Audit — 4 remaining hardcoded `White` foregrounds converted to `TextOnAccentFillColorPrimaryBrush`; static regression guard added; stale test premise fixed.
- v1.2.0 (2026-09-08): Second pass — measured that `SystemFillColorAccentBrush` does not exist in 4.3.0 at all (fills silently never render). Analytics page badges + 3 bar fills moved to `AccentFillColorDefaultBrush`, accent-coloured text to `AccentTextFillColorPrimaryBrush`; second guard added.
- v1.3.0 (2026-09-08): Third pass — element-local `ThemesDictionary`/`ControlsDictionary` shadow the runtime accent bridge (Light static dict lacks `AccentFillColorDefaultBrush` entirely). Fix: `ThemeService.CopyRuntimeAccentResources` copies accent keys onto each themed element; element-level regression test added. Plus CardControl template note: `Content` → right Auto column, `Header` → left `*` column.
- v1.4.0 (2026-09-08): Fourth pass — the real final root cause was WPF DP precedence: a local `Background` attribute on the badge Border silently overrode the `IsTopThree` style trigger (local > trigger), so the badge never turned accent while the white OnAccent text did. Fix: remove the local attribute, default via Style Setter. Repo scan found no other instances of the anti-pattern.
