## 1. Fix Add Slot Picker Double-Border

- [x] 1.1 In `AddSlotContent.xaml`, locate the `<Border Style="{StaticResource PickerIconContainerStyle}">` wrapper around `SlotOrb` in the plugin type picker card template
- [x] 1.2 Remove the outer `<Border>` container entirely, leaving `<SlotOrb>` as the direct child of its parent layout cell
- [x] 1.3 Adjust `SlotOrb` size/margin on the now-unwrapped element to maintain correct layout alignment within the card grid (target size ~36)
- [x] 1.4 Verify `PickerIconContainerStyle` is no longer referenced in `AddSlotContent.xaml`; remove the style definition if it has no other consumers

## 2. Fix Plugins Page Accent-Color Fragmentation

- [x] 2.1 In `ExpandableCard.xaml`, locate the `<Border Background="{Binding OrbBackground, ElementName=Root}">` icon container in the card header
- [x] 2.2 Replace the dynamic `OrbBackground` binding with a static neutral theme brush: `{DynamicResource ControlFillColorSecondaryBrush}`
- [x] 2.3 In the same `ExpandableCard.xaml`, ensure `SlotOrb` inside the icon container has `IsTransparent="False"` (or that the default is False) so the orb renders its own circle
- [x] 2.4 In `SettingsPluginsPage.xaml`, remove the `OrbBackground="{Binding AccentColor, Converter={StaticResource HexToBrushConverter}}"` binding from all `ExpandableCard` usages
- [x] 2.5 Confirm `HexToBrushConverter` is still used elsewhere in the page; if `OrbBackground` was its only consumer on this page, the converter reference can be removed from `Page.Resources`

## 3. Verify OrbBackground Dependency Property

- [x] 3.1 Check `ExpandableCard.xaml.cs` for the `OrbBackground` dependency property definition — retain it (do not delete) in case other surfaces use it, but confirm no remaining XAML binding uses it after step 2.4

## 4. Build & Visual Verification

- [x] 4.1 Run `dotnet build Pulsar/Pulsar/Pulsar.csproj` and confirm zero errors
- [x] 4.2 Launch the app and open the Plugins settings page — confirm all plugin icons render as neutral-background circles with no accent colors
- [x] 4.3 Open the Add Slot dialog and inspect the plugin type picker — confirm icons render as clean circles with no outer square border
- [x] 4.4 Confirm the same plugin (e.g. Command Runner) looks visually consistent between the Plugins page card and the Add Slot picker card
