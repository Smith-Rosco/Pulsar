## 1. Sound Feedback Integration

- [x] 1.1 Define `ISoundFeedbackService` interface with `PlayTick()` and `PlayThump()` methods in `Pulsar.Services.Interfaces`.
- [x] 1.2 Implement `SoundFeedbackService` using `System.Media.SoundPlayer` and embedded sound stream assets (or minimal `.wav` files).
- [x] 1.3 Register `SoundFeedbackService` in the dependency injection container (`App.xaml.cs`).
- [x] 1.4 Inject `ISoundFeedbackService` into `RadialMenuViewModel`.
- [x] 1.5 Add `PlayTick()` call in `RadialMenuViewModel.UpdateActiveSlot()` when the slot changes to a valid target.
- [x] 1.6 Add `PlayThump()` call in `RadialMenuInputCoordinator.ExecuteSelectionAsync()` when an action is executed.

## 2. Visual Identity Modes

- [x] 2.1 Add `MenuThemeTone` property to `RadialMenuViewModel` (e.g., Cool/Warm enum or specific brush/color strings).
- [x] 2.2 Update `RadialMenuViewModel.Show(RadialMenuMode)` to set the `MenuThemeTone` appropriately (Cool for Task, Warm for Action).
- [x] 2.3 Modify `RadialMenuWindow.xaml` to bind the center element's glow/color to `MenuThemeTone`.
- [ ] 2.4 Test visually to ensure Task mode and Action mode are instantly distinguishable.

## 3. Usage Analytics UI

- [x] 3.1 Create `SettingsAnalyticsPageViewModel` that queries `IPluginUsageTracker.GetMostUsedPlugins()` and exposes the data.
- [x] 3.2 Add `SettingsAnalyticsPage.xaml` View with an `ItemsControl` to display plugin/slot usage statistics (executions and average duration).
- [x] 3.3 Register the new Analytics page and its ViewModel in the DI container.
- [x] 3.4 Update Settings navigation to include a new "Analytics" or "Insights" tab pointing to `SettingsAnalyticsPage`.
