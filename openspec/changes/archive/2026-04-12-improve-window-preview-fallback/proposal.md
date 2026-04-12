## Why

The current radial-menu window preview flow depends on on-demand `PrintWindow` capture at hover time. For long-idle windows, especially Remote Desktop windows, capture can legitimately fail even when the window is still a valid switch target, causing the center slot to fall back to the app icon and making window switching feel unreliable.

## What Changes

- Introduce a dedicated window-preview behavior contract for the radial menu center slot.
- Define a layered preview strategy that prefers a live system-managed preview path, then falls back to a last-known-good snapshot, and only finally falls back to the window or process icon.
- Preserve the current ability to switch to windows that remain valid Alt-Tab targets even when fresh preview capture is unavailable.
- Clarify cache lifetime, invalidation, and stale-preview behavior so preview availability is resilient across menu sessions instead of depending entirely on a fresh capture.
- Establish explicit behavior for minimized, cloaked, invalid, or otherwise non-previewable windows.

## Capabilities

### New Capabilities
- `window-preview-fallback`: Defines how Pulsar resolves, displays, caches, invalidates, and degrades hover previews for window-switching surfaces.

### Modified Capabilities

## Impact

- Affected code: `PreviewService`, `WindowService`, `RadialMenuVisualStateCoordinator`, `RadialMenuViewModel`, and the radial menu preview host UI.
- Affected systems: window-switching UX, preview caching, native preview interop, and fallback presentation behavior.
- Likely native/API impact: DWM thumbnail APIs may become the preferred preview path, with `PrintWindow` retained as a secondary capture path.
