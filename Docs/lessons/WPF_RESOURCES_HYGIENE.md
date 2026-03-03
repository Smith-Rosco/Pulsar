# WPF Resources Hygiene

**Status**: Published  
**Scope**: Lesson  
**Applies To**: WPF Page, Window, UserControl  
**Last Updated**: 2026-03-03

---

## Rule (TL;DR)

Each element can set `Resources` only once. Do not mix a top-level `<ResourceDictionary>...</ResourceDictionary>` followed by additional resources in the same `<Page.Resources>` block. This causes `XAMLParseException`.

---

## Symptom

`XAMLParseException` at runtime with message about "Resources property can only be set once".

---

## Root Cause

WPF's XAML parser treats the `Resources` property as a single assignment. If you define a `<ResourceDictionary>` wrapper and then try to add more resources outside of it, the parser sees two assignments to the same property.

---

## Correct Pattern (Page)

```xml
<Page.Resources>
    <ResourceDictionary>
        <ResourceDictionary.MergedDictionaries>
            <ResourceDictionary Source="pack://application:,,,/Pulsar;component/Styles/ButtonStyles.xaml"/>
        </ResourceDictionary.MergedDictionaries>

        <!-- All converters/styles/templates go here (same dictionary) -->
        <BooleanToVisibilityConverter x:Key="BoolToVis"/>
        <DataTemplate x:Key="MyTemplate">
            <TextBlock Text="{Binding Name}"/>
        </DataTemplate>
    </ResourceDictionary>
</Page.Resources>
```

---

## Incorrect Pattern

```xml
<Page.Resources>
    <ResourceDictionary>
        <ResourceDictionary.MergedDictionaries>
            <ResourceDictionary Source="pack://application:,,,/Pulsar;component/Styles/ButtonStyles.xaml"/>
        </ResourceDictionary.MergedDictionaries>
    </ResourceDictionary>
    
    <!-- ❌ WRONG: This is a second assignment to Resources -->
    <BooleanToVisibilityConverter x:Key="BoolToVis"/>
</Page.Resources>
```

---

## Applies To

- `Page.Resources`
- `Window.Resources`
- `UserControl.Resources`
- `ContextMenu.Resources`

---

## Related Documents

- [WPF Theme Injection Pitfalls](./WPF_THEME_INJECTION_PITFALLS.md) - Theme injection timing
- [UI Best Practices](../guides/UI_BEST_PRACTICES.md) - General UI guidelines

---

**Change History**:
- v1.0.0 (2026-03-03): Initial extraction from AGENTS.md
