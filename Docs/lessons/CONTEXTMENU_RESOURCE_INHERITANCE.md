# ContextMenu Resource Inheritance

**Status**: Published  
**Scope**: Lesson  
**Applies To**: WPF ContextMenu  
**Last Updated**: 2026-03-03

---

## Rule (TL;DR)

Context menus do not inherit Window resources. Manually inject `ui:ControlsDictionary` into `ContextMenu.Resources`.

---

## Symptom

ContextMenu items appear unstyled or with default WPF styling instead of Wpf.Ui styling.

---

## Root Cause

ContextMenus are rendered in a separate visual tree (Popup) and do not inherit resources from the parent Window or Page.

---

## Correct Pattern

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

## Incorrect Pattern

```xml
<!-- ❌ WRONG: ContextMenu will not inherit Window resources -->
<ContextMenu>
    <MenuItem Header="Action"/>
</ContextMenu>
```

---

## Related Documents

- [WPF Theme Injection Pitfalls](./WPF_THEME_INJECTION_PITFALLS.md) - Theme injection timing
- [UI Best Practices](../guides/UI_BEST_PRACTICES.md) - General UI guidelines

---

**Change History**:
- v1.0.0 (2026-03-03): Initial extraction from AGENTS.md
