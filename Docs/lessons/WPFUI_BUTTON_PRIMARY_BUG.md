# WPF-UI Button Appearance="Primary" Bug

**Status**: Published  
**Scope**: Lesson  
**Applies To**: Wpf.Ui Button controls  
**Last Updated**: 2026-03-03

---

## Rule (TL;DR)

**Do NOT use `Appearance="Primary"` on WPF-UI buttons!** Always use explicit Pulsar button styles from `Styles/ButtonStyles.xaml`.

---

## Symptom

Button text becomes invisible (white/transparent) on hover, making the button unusable.

---

## Root Cause

Wpf.Ui's `Appearance="Primary"` relies on dynamic resource inheritance that breaks when themes are injected at window-level (Multi-Headed UI architecture). This causes the foreground color to fallback to unexpected values (white/transparent) on hover.

---

## Correct Pattern

```xml
<Window.Resources>
    <ResourceDictionary>
        <ResourceDictionary.MergedDictionaries>
            <ResourceDictionary Source="pack://application:,,,/Pulsar;component/Styles/ButtonStyles.xaml"/>
        </ResourceDictionary.MergedDictionaries>
    </ResourceDictionary>
</Window.Resources>

<!-- Primary action -->
<ui:Button Content="Save" Style="{StaticResource PulsarPrimaryButtonStyle}"/>

<!-- Secondary action -->
<ui:Button Content="Cancel" Style="{StaticResource PulsarSecondaryButtonStyle}"/>

<!-- Danger action -->
<ui:Button Content="Delete" Style="{StaticResource PulsarDangerButtonStyle}"/>
```

---

## Incorrect Pattern

```xml
<!-- ❌ WRONG: Text becomes invisible on hover -->
<ui:Button Content="Save" Appearance="Primary"/>
```

---

## Why This Works

The Pulsar styles use explicit `ControlTemplate` with hardcoded `Trigger` definitions for each state (Normal/Hover/Pressed/Disabled), ensuring 100% predictable colors regardless of dynamic theme injection timing.

---

## Available Styles

- `PulsarPrimaryButtonStyle` - Primary action (blue background)
- `PulsarSecondaryButtonStyle` - Secondary action (gray background)
- `PulsarDangerButtonStyle` - Destructive action (red background)

---

## Related Documents

- [WPF Theme Injection Pitfalls](./WPF_THEME_INJECTION_PITFALLS.md) - Theme injection timing
- [UI Best Practices](../guides/UI_BEST_PRACTICES.md) - General UI guidelines

---

**Change History**:
- v1.0.0 (2026-03-03): Initial extraction from AGENTS.md
