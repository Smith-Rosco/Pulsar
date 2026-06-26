## Context

Pulsar is adding multi-sensory feedback and data-driven insights to reinforce muscle memory and improve the user experience. The current implementation relies solely on visual animations and lacks distinct visual cues for different operational modes (Task vs. Action). Furthermore, while `PluginUsageTracker` collects rich usage data in the background, it is not currently exposed to the user.

## Goals / Non-Goals

**Goals:**
- Provide low-latency auditory feedback for menu interactions (hover, execute).
- Visually differentiate Task mode (Window Switcher) from Action mode (Command Toolbox) in the Radial Menu.
- Expose basic usage statistics (e.g., most used plugins, usage heatmaps) in the Settings UI without relying on heavy third-party charting libraries.

**Non-Goals:**
- Allowing users to upload custom sound packs (initially, we will use built-in, unconfigurable sounds).
- Complex, interactive, highly analytical charting in the settings (a simple list/bar presentation is sufficient).
- Changing the core plugin execution pipeline.

## Decisions

1. **Sound Feedback Implementation**:
   - Create `ISoundFeedbackService` with methods like `PlayTick()` (for hover) and `PlayThump()` (for execute/click).
   - *Implementation Choice*: Use `System.Media.SoundPlayer` with pre-loaded `Stream` objects for the sound assets. This avoids the overhead of instantiating `MediaPlayer` components for every sound event and provides acceptable latency for UI sounds without introducing heavy dependencies like NAudio.
   - *Alternative*: WPF `MediaPlayer` – rejected because it can be heavy for rapid consecutive sounds (like sweeping across multiple slots).

2. **Visual Mode Identity**:
   - The `RadialMenuViewModel` already exposes `CurrentMode`. We will add a `MenuThemeTone` property that updates when the mode changes.
   - In `RadialMenuWindow.xaml`, we will bind a background glow or center element color to this `MenuThemeTone`. 
   - Task Mode (System navigation) will use a cool tone (e.g., Blue/Cyan), while Action Mode (Toolbox) will use a warm tone (e.g., Orange/Red).

3. **Usage Analytics UI**:
   - Create a new settings page `SettingsAnalyticsPage.xaml` (and corresponding ViewModel).
   - Use standard WPF controls (e.g., `ItemsControl` with `Grid` or `ProgressBar` templates) to render relative usage frequencies.
   - Data will be fetched via `IPluginUsageTracker.GetMostUsedPlugins()` and `GetAllStats()`.

## Risks / Trade-offs

- **[Risk] Sound Latency**: If the sound playback lags behind the visual animation, it will break immersion.
  - *Mitigation*: Ensure sound assets are tiny, pre-loaded into memory streams, and played asynchronously.
- **[Risk] UI Thread Blocking**: Reading the usage stats JSON file or processing large histories could block the settings UI.
  - *Mitigation*: `PluginUsageTracker` already loads data asynchronously. We will ensure the ViewModel fetches data on a background thread and dispatches updates to the UI thread.
