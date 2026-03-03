# WPF UserControl DataContext Binding Breaks

**Status**: Published  
**Scope**: Lesson  
**Applies To**: WPF UserControl with ContentPresenter  
**Last Updated**: 2026-03-03

---

## Rule (TL;DR)

UserControls break the visual tree for `RelativeSource` bindings, causing command bindings to fail. Use **Code-Behind Loaded Event** to manually set the Tag property as a workaround.

---

## Symptom

Buttons inside UserControl content have `Command = NULL` even though the command exists in the parent ViewModel.

---

## Root Cause

When a UserControl exposes a property (e.g., `PageDataContext`) that needs to be passed to its content, standard XAML bindings like `RelativeSource AncestorType` or `ElementName` **will fail** because:
- ContentPresenter displays content directly without wrapping it
- The content is set before being added to the visual tree
- UserControl creates a visual tree boundary that blocks ancestor lookups

---

## Correct Pattern

### Step 1: Add Loaded event to the content root element

```xml
<controls:ExpandableCard PageDataContext="{Binding DataContext, RelativeSource={RelativeSource AncestorType=Page}}">
    <controls:ExpandableCard.ExpandedContent>
        <StackPanel Loaded="StackPanel_Loaded">
            <!-- Buttons can now bind to Tag.CommandName -->
            <ui:Button Command="{Binding Tag.PickIconCommand, RelativeSource={RelativeSource AncestorType=StackPanel}}"
                       CommandParameter="{Binding}"/>
        </StackPanel>
    </controls:ExpandableCard.ExpandedContent>
</controls:ExpandableCard>
```

### Step 2: Implement Loaded handler in Code-Behind

```csharp
/// <summary>
/// Workaround for UserControl breaking visual tree binding.
/// Manually sets StackPanel.Tag to ExpandableCard.PageDataContext when loaded.
/// </summary>
private void StackPanel_Loaded(object sender, RoutedEventArgs e)
{
    if (sender is StackPanel stackPanel)
    {
        // Find the UserControl ancestor
        var expandableCard = FindVisualParent<ExpandableCard>(stackPanel);
        if (expandableCard != null && expandableCard.PageDataContext != null)
        {
            // Set the Tag so child buttons can bind to commands
            stackPanel.Tag = expandableCard.PageDataContext;
        }
    }
}

// Helper method (should already exist in Page code-behind)
private static T? FindVisualParent<T>(DependencyObject child) where T : DependencyObject
{
    var parent = System.Windows.Media.VisualTreeHelper.GetParent(child);
    if (parent == null) return null;
    if (parent is T typedParent) return typedParent;
    return FindVisualParent<T>(parent);
}
```

---

## Why This Works

- At `Loaded` time, the element is in the visual tree, so `FindVisualParent` succeeds
- We manually bridge the gap that XAML bindings cannot cross
- Child elements can use standard `RelativeSource AncestorType` to find the StackPanel

---

## Incorrect Pattern (Does Not Work)

Wrapping content in a Grid with Tag binding does NOT work because ContentPresenter bypasses the wrapper.

```xml
<!-- ❌ WRONG: ContentPresenter bypasses the Grid -->
<controls:ExpandableCard.ExpandedContent>
    <Grid Tag="{Binding DataContext, RelativeSource={RelativeSource AncestorType=Page}}">
        <ui:Button Command="{Binding Tag.MyCommand, RelativeSource={RelativeSource AncestorType=Grid}}"/>
    </Grid>
</controls:ExpandableCard.ExpandedContent>
```

---

## When to Use

Any time you have a UserControl that needs to pass a ViewModel/DataContext to dynamically loaded content (e.g., ExpandableCard, custom dialogs, templated controls).

---

## Related Documents

- [Component Library](../guides/COMPONENT_LIBRARY.md) - ExpandableCard usage
- [UI Best Practices](../guides/UI_BEST_PRACTICES.md) - General UI guidelines

---

**Change History**:
- v1.0.0 (2026-03-03): Initial extraction from AGENTS.md
