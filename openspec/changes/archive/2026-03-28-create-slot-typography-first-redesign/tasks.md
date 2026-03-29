## 1. ViewModel flow simplification

- [x] 1.1 Refactor `Pulsar/Pulsar/ViewModels/Dialogs/AddSlotViewModel.cs` to remove or demote wizard-specific presentation state so Create Slot can operate as a single progressive editing surface
- [x] 1.2 Update dialog copy properties in `Pulsar/Pulsar/ViewModels/Dialogs/AddSlotViewModel.cs` to reflect one continuous creation flow instead of step-based guidance
- [x] 1.3 Add any lightweight presentation helpers needed for secondary appearance disclosure or preview-adjacent summary placement without changing slot creation semantics

## 2. Create Slot dialog redesign

- [x] 2.1 Rebuild the top of `Pulsar/Pulsar/Views/Dialogs/Contents/AddSlotContent.xaml` into one unified header/preview anchor instead of separate repeated cards
- [x] 2.2 Replace the current step-based body layout in `Pulsar/Pulsar/Views/Dialogs/Contents/AddSlotContent.xaml` with a single scrollable editing surface that keeps type selection, required setup, and optional polish in one hierarchy
- [x] 2.3 Restyle the plugin type picker in `Pulsar/Pulsar/Views/Dialogs/Contents/AddSlotContent.xaml` so selection relies more on alignment, typography, and restrained highlight states than large card-like tiles
- [x] 2.4 Rework the Create Slot parameter field template in `Pulsar/Pulsar/Views/Dialogs/Contents/AddSlotContent.xaml` from bordered mini-cards into compact editor rows or lightweight stacked form groups
- [x] 2.5 Keep validation and required-state feedback visible in `Pulsar/Pulsar/Views/Dialogs/Contents/AddSlotContent.xaml` while moving low-priority descriptions and helper content to lighter disclosure patterns where appropriate
- [x] 2.6 Demote icon and color customization in `Pulsar/Pulsar/Views/Dialogs/Contents/AddSlotContent.xaml` into a secondary appearance disclosure while keeping label editing easy to access
- [x] 2.7 Integrate summary tokens and supporting metadata in `Pulsar/Pulsar/Views/Dialogs/Contents/AddSlotContent.xaml` as lightweight context near the preview or footer instead of a full standalone summary card

## 3. Slot Configuration dialog alignment

- [x] 3.1 Refactor `Pulsar/Pulsar/Views/Dialogs/Contents/SlotConfigurationDialogContent.xaml` to follow the same typography-first hierarchy and reduced-container language introduced in Create Slot
- [x] 3.2 Rework the slot parameter field template in `Pulsar/Pulsar/Views/Dialogs/Contents/SlotConfigurationDialogContent.xaml` to match the calmer row-oriented editing style used in Create Slot
- [x] 3.3 Rebalance preview, validation, required fields, and optional/advanced disclosures in `Pulsar/Pulsar/Views/Dialogs/Contents/SlotConfigurationDialogContent.xaml` so primary editing tasks dominate over decorative surfaces
- [x] 3.4 Add any minimal supporting presentation properties to `Pulsar/Pulsar/ViewModels/Dialogs/SlotConfigurationDialogViewModel.cs` required by the revised hierarchy and disclosure patterns

## 4. Typography and spacing consistency

- [x] 4.1 Standardize the slot dialog type scale in `Pulsar/Pulsar/Views/Dialogs/Contents/AddSlotContent.xaml` and `Pulsar/Pulsar/Views/Dialogs/Contents/SlotConfigurationDialogContent.xaml` so title, section, body, and metadata roles use a tighter consistent hierarchy
- [x] 4.2 Normalize vertical spacing and section rhythm across both dialog XAML files so grouping depends primarily on whitespace and alignment rather than repeated bordered panels
- [x] 4.3 Remove or reduce redundant section headings and equivalent-weight card treatments across both dialog XAML files, preserving stronger emphasis only for true primary landmarks

## 5. Validation and verification

- [x] 5.1 Verify the new Create Slot layout still supports plugin type selection, action selection, parameter picking, appearance editing, and save confirmation without regressions
- [x] 5.2 Verify the aligned Slot Configuration dialog still supports action changes, parameter picking, validation visibility, appearance edits, and slot removal behavior without regressions
- [x] 5.3 Run `dotnet build Pulsar/Pulsar/Pulsar.csproj` and resolve any build issues introduced by the dialog refactor
