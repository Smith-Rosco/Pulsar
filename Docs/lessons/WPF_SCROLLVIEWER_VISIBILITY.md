# WPF ScrollViewer Visibility Workaround

**Status**: Published  
**Scope**: Lesson  
**Applies To**: WPF NavigationView, ListView, complex controls  
**Last Updated**: 2026-03-03

---

## Rule (TL;DR)

If standard `ScrollViewer.VerticalScrollBarVisibility="Hidden"` fails to work (common in complex controls like `NavigationView` or `ListView`), use the **Code-Behind Visual Tree Helper** approach.

---

## Symptom

Scrollbars remain visible even after setting `ScrollViewer.VerticalScrollBarVisibility="Hidden"` in XAML.

---

## Root Cause

Internal control templates often override implicit styles or set properties locally. Direct manipulation of the visual tree at runtime is the only 100% reliable way to force visibility.

---

## Correct Pattern

```csharp
// In Window/Control Code-Behind (e.g. SettingsWindow.xaml.cs)

this.Loaded += (s, e) =>
{
    // Force hide scrollbars after the control is loaded
    DisableScrollViewers(MyTargetControl);
};

private void DisableScrollViewers(DependencyObject depObj)
{
    if (depObj == null) return;

    for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(depObj); i++)
    {
        var child = System.Windows.Media.VisualTreeHelper.GetChild(depObj, i);
        if (child is ScrollViewer scrollViewer)
        {
            scrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Hidden;
            scrollViewer.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
        }
        DisableScrollViewers(child); // Recursive
    }
}
```

---

## When to Use

- Complex controls with internal ScrollViewers (NavigationView, ListView, TreeView)
- When XAML-based approaches fail
- When you need 100% guaranteed scrollbar hiding

---

## Related Documents

- [UI Best Practices](../guides/UI_BEST_PRACTICES.md) - General UI guidelines

---

**Change History**:
- v1.0.0 (2026-03-03): Initial extraction from AGENTS.md
