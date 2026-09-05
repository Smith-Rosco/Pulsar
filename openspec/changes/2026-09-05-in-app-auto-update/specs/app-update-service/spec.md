# app-update-service Delta

## ADDED Requirements

### Requirement: The app SHALL detect the latest release through a multi-tier resilient check

The app SHALL determine the latest published release version via GitHub, trying tiers in order: (1) GitHub REST API, (2) the Releases Atom feed on the `github.com` main domain, (3) the `releases/latest` redirect Location header. Each tier SHALL use a short per-request timeout, and the check SHALL succeed if any tier succeeds without ever blocking the UI thread.

#### Scenario: REST API succeeds

- **WHEN** the GitHub REST API responds within its timeout with the latest release
- **THEN** the service SHALL use that version and SHALL NOT try later tiers

#### Scenario: API blocked, Atom feed succeeds

- **WHEN** the REST API fails or times out and the Atom feed responds with the latest release tag
- **THEN** the service SHALL parse the tag from the feed and treat the check as successful

#### Scenario: All tiers fail

- **WHEN** every tier fails or times out
- **THEN** the service SHALL report a network-error state, SHALL NOT show a false "outdated" or false "up to date" badge, and SHALL remain retryable

### Requirement: Version comparison SHALL produce exactly three outcomes

The service SHALL compare the running version against the detected version using semantic comparison of `{Major}.{Minor}.{Patch}` and produce exactly one of: up-to-date, update-available, or parse-failed. A running version greater than or equal to the detected version SHALL be reported as up-to-date.

#### Scenario: Running version ahead of latest release

- **WHEN** the running version (e.g. `1.10.0`) is greater than the detected release (e.g. `1.9.0`)
- **THEN** the UI SHALL present the state as up-to-date, never as an available update

#### Scenario: Unparseable version string

- **WHEN** the detected version string cannot be parsed
- **THEN** the service SHALL report parse-failed and SHALL NOT auto-trigger a download

### Requirement: Asset download SHALL fail over across multiple sources

When the user requests the update, the service SHALL download the release asset trying the official source first, then configured accelerator mirrors in order; a failing source (timeout, 404, 502, mid-stream error) SHALL advance to the next source automatically. Request headers SHALL be scoped per request kind so that GitHub-API-specific Accept headers never leak to XML or binary downloads.

#### Scenario: Mirror failover succeeds

- **WHEN** the currently selected mirror returns 404 or times out during download
- **THEN** the service SHALL retry with the next configured source without user intervention and complete the download if any source succeeds

#### Scenario: Header isolation

- **WHEN** the service alternates between API calls, feed requests, and binary downloads
- **THEN** each request SHALL carry only the Accept header appropriate to its kind

### Requirement: Downloaded assets SHALL be integrity-checked before handoff

The service SHALL verify the downloaded asset against a SHA256 digest when one is available from the release metadata; when no digest is available (Atom-only path), the service SHALL fall back to size verification and SHALL clearly indicate the weaker check in the UI. A failed integrity check SHALL discard the asset and never launch the installer.

#### Scenario: Digest available and matching

- **WHEN** release metadata provides a SHA256 digest and the downloaded file matches
- **THEN** the service SHALL proceed to the install handoff

#### Scenario: Integrity check fails

- **WHEN** the digest or size check fails
- **THEN** the service SHALL delete the partial file, surface an error state, and offer retry

### Requirement: Update UI SHALL be non-intrusive and privacy-bounded

The About settings page SHALL expose the current version, the check result, and a manual check action; a new-version detection MAY raise a tray notification via the existing notification seam. Automatic background checks SHALL be disableable in settings, and the service SHALL only contact GitHub-related hosts and SHALL NOT transmit any machine-identifying information.

#### Scenario: User disables automatic checks

- **WHEN** the user turns off automatic update checks in settings
- **THEN** the app SHALL NOT perform background checks while still allowing manual checks from the About page

#### Scenario: New version detected in background

- **WHEN** a background check detects a newer release and notifications are enabled
- **THEN** the app SHALL show a single tray notification with a non-blocking path to the About page
