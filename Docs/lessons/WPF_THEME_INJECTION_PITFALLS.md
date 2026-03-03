# WPF Theme Injection Pitfalls

**Status**: Published  
**Scope**: Lesson  
**Applies To**: WPF Page, Window, UserControl  
**Last Updated**: 2026-03-03

---

## Rule (TL;DR)

Pulsar uses a "Multi-Headed" UI architecture where `App.xaml` does NOT contain global styles. When creating Windows or Pages, you MUST inject themes manually via `IThemeService.ApplyTheme()`. For Pages with `<Page.Resources>`, call `ApplyTheme()` **after** `InitializeComponent()` to avoid resource dictionary replacement.

---

## Architecture Context

- **Radial Menu**: Uses lightweight custom themes (`Themes/Theme.Dark.xaml`). Background is strictly transparent.
- **Settings/Dialogs**: Uses `Wpf.Ui` themes with Mica backdrop.
- **Action**: When creating a new window, you MUST inject the theme manually via `IThemeService.ApplyTheme()` in the constructor.

---

## Critical Pitfall: Pages + XAML Resources

### Symptom

Theme DynamicResources become missing (e.g. `Theme.Orb.*`), resulting in blank/unstyled visuals (e.g. `JellyOrb`'s `OrbFill` appears empty) and generally "ugly" fallback UI.

### Root Cause

If a `Page` defines `<Page.Resources>`, WPF will create/assign the `Resources` dictionary during XAML load. If you call `ApplyTheme(page, ...)` **before** `InitializeComponent()`, the XAML load can replace the `Resources` dictionary instance, discarding injected dictionaries (e.g., `ThemesDictionary`, `ControlsDictionary`, and Pulsar `Themes/Theme.*.xaml`).

### Correct Pattern

```csharp
public partial class MyPage : Page
{
    private readonly IThemeService _themeService;

    public MyPage(IThemeService themeService)
    {
        _themeService = themeService;
        
        InitializeComponent(); // FIRST: Load XAML
        
        // THEN: Apply theme after Resources dictionary is stable
        _themeService.ApplyTheme(this, ThemeType.Dark);
    }
}
```

### Incorrect Pattern

```csharp
public MyPage(IThemeService themeService)
{
    _themeService = themeService;
    
    // ❌ WRONG: Theme gets discarded when InitializeComponent runs
    _themeService.ApplyTheme(this, ThemeType.Dark);
    
    InitializeComponent();
}
```

### Additional Rule: SettingsWindow Page Caching

`SettingsWindow` should explicitly apply theme to each cached page (`General`, `Slots`, `Plugins`) to keep behavior consistent.

---

## Context Menus

### Rule

Context menus do not inherit Window resources. Manually inject `ui:ControlsDictionary` into `ContextMenu.Resources`.

### Correct Pattern

```xml
<ContextMenu>
    <ContextMenu.Resources>
        <ResourceDictionary>
            <ResourceDictionary.MergedDictionaries>
                <ui:ControlsDictionary/>
            </ResourceDictionary.MergedDictionaries>
        </ResourceDictionary>
    </ContextMenu.Resources>
    <MenuItem Header="Action"/>
</ContextMenu>
```

---

## Animations and Theme Switching

### Rule

When switching themes, avoid clearing resources (`MergedDictionaries.Remove`) if animations are running (common in `Wpf.Ui`). Instead, update the existing `ThemesDictionary.Theme` property in place.

### Correct Pattern

```csharp
// Find existing ThemesDictionary and update it
var themesDict = window.Resources.MergedDictionaries
    .OfType<ThemesDictionary>()
    .FirstOrDefault();

if (themesDict != null)
{
    themesDict.Theme = newTheme; // Update in place
}
```

### Incorrect Pattern

```csharp
// ❌ WRONG: Clearing can break running animations
window.Resources.MergedDictionaries.Clear();
window.Resources.MergedDictionaries.Add(new ThemesDictionary { Theme = newTheme });
```

---

## Related Documents

- [Dialog System](../architecture/DIALOG_SYSTEM.md) - Dialog theme inheritance
- [WPF Resources Hygiene](./WPF_RESOURCES_HYGIENE.md) - Resource dictionary best practices
- [UI Best Practices](../guides/UI_BEST_PRACTICES.md) - General UI guidelines

---

**Change History**:
- v1.0.0 (2026-03-03): Initial extraction from AGENTS.md
