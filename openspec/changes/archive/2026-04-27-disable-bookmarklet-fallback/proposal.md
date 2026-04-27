## Why

The bookmarklet runner currently falls back to simulated typing when UI Automation injection fails. Bookmarklet payloads are often long enough that this fallback can monopolize the browser, leave partially entered `javascript:` content behind, and turn a transient page-readiness issue into a disruptive failure.

## What Changes

- Remove the bookmarklet runner's automatic simulated-typing fallback when UI Automation injection fails.
- Require bookmarklet execution failures to surface as normal plugin errors so Pulsar's existing user-facing notification flow can report the issue immediately.
- Require failed bookmarklet attempts to avoid leaving partial script text or other residual input state that would prevent a clean retry after the page finishes loading.
- Clarify bookmarklet execution behavior around retry safety, especially for common transient failures such as the browser address bar not yet being ready for UI Automation text injection.

## Capabilities

### New Capabilities
- `bookmarklet-runner-execution`: Defines the bookmarklet runner's execution contract for UIA-only injection, failure handling, and retry-safe cleanup semantics.

### Modified Capabilities

## Impact

- Affected code: `Pulsar/Pulsar/Plugins/Extensions/BookmarkletRunner/BookmarkletRunnerPlugin.cs`, related bookmarklet helper code, and plugin action feedback tests or execution tests.
- User impact: bookmarklet execution will fail fast instead of degrading into browser-blocking typed input, and retries after page readiness issues should remain safe.
- Dependencies: no new external dependencies are expected; the change relies on existing plugin error propagation and tray-notification behavior.
