## 1. Bookmarklet Execution Path

- [x] 1.1 Remove the simulated-typing fallback branch from `BookmarkletRunnerPlugin` so UIA failure becomes the terminal execution outcome.
- [x] 1.2 Ensure failed UIA injection returns a recoverable `PluginResult.Error(...)` without sending payload text or Enter after the failed injection attempt.
- [x] 1.3 Review bookmarklet failure messages so retry guidance is clear for transient page-readiness or browser-readiness failures.

## 2. Retry-Safety Verification

- [x] 2.1 Add or update tests that verify UIA failure does not trigger typed payload input.
- [x] 2.2 Add or update tests that verify a failed bookmarklet attempt leaves no plugin-injected partial `javascript:` payload state that would block a retry.
- [x] 2.3 Add or update tests that verify successful UIA injection still executes the bookmarklet normally.

## 3. Feedback And Validation

- [x] 3.1 Verify bookmarklet failures continue to flow through the shared action feedback and tray-notification path without plugin-owned notification code.
- [x] 3.2 Run the relevant automated tests and a project build to confirm the fail-fast execution path compiles and behaves as expected.
