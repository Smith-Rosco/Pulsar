## ADDED Requirements

### Requirement: Bookmarklet runner SHALL fail fast when UIA injection is unavailable
The bookmarklet runner SHALL treat UI Automation address-bar injection as its only payload entry mechanism. If UIA injection cannot set the full bookmarklet payload into the focused browser address bar, the runner SHALL return a recoverable execution failure instead of degrading to typed or character-by-character payload entry.

#### Scenario: UIA injection fails before payload entry completes
- **WHEN** the bookmarklet runner focuses the browser address bar but UIA cannot write the full bookmarklet payload to the focused element
- **THEN** the runner returns a recoverable failure result
- **AND** the runner SHALL NOT type the bookmarklet payload through simulated keyboard text entry
- **AND** the runner SHALL NOT press Enter to execute a payload that UIA did not confirm as fully injected

#### Scenario: UIA injection succeeds
- **WHEN** the bookmarklet runner successfully writes the full bookmarklet payload into the focused browser address bar through UIA
- **THEN** the runner proceeds with normal bookmarklet execution

### Requirement: Failed bookmarklet attempts SHALL remain retry-safe
When bookmarklet execution fails before successful payload injection, Pulsar SHALL avoid introducing residual bookmarklet text or partial payload input that would interfere with the user's next execution attempt.

#### Scenario: Failure occurs while page is not yet ready
- **WHEN** the page or browser chrome is not yet ready and the bookmarklet runner cannot complete UIA payload injection
- **THEN** the failure leaves no plugin-typed partial `javascript:` payload in the target browser input surface
- **AND** the user can retry the same bookmarklet after the page finishes loading without having to clear plugin-injected script remnants first

#### Scenario: Browser address bar was focused for setup
- **WHEN** the runner has already focused the browser and selected the address bar as part of setup but UIA payload injection fails
- **THEN** the runner MAY leave browser focus or address-bar selection in place
- **AND** it MUST NOT add additional synthetic text-entry cleanup steps that could introduce new residual input state

### Requirement: Bookmarklet execution failures SHALL use the standard action feedback path
The bookmarklet runner SHALL surface execution failures through the normal plugin-result contract so Pulsar can present consistent user-facing failure feedback through existing action-notification surfaces.

#### Scenario: Bookmarklet execution returns a handled failure
- **WHEN** the bookmarklet runner cannot execute because UIA injection fails, the script file is invalid, or the browser cannot be prepared
- **THEN** the runner returns a plugin failure result with a user-meaningful error message
- **AND** Pulsar presents that failure through the shared action feedback mechanism rather than requiring plugin-specific notification code
