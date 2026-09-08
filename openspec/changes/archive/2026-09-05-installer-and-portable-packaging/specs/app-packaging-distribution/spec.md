# app-packaging-distribution Delta

## ADDED Requirements

### Requirement: The app SHALL ship in two distribution forms

Each release SHALL produce an installer package and a self-contained standalone package. The standalone package SHALL embed the .NET desktop runtime and SHALL run on a clean Windows 10/11 x64 machine without any pre-installed runtime. Artifact names SHALL follow `Pulsar-v{version}-Setup` and `Pulsar-v{version}-Standalone-win-x64` conventions.

#### Scenario: Standalone runs on a clean machine

- **WHEN** the standalone package is executed on a machine without the .NET 8 Desktop Runtime
- **THEN** the app SHALL start and function normally

#### Scenario: Installer registers the app

- **WHEN** the installer completes on a clean machine
- **THEN** the app SHALL be launchable from the Start Menu and appear in the system's installed-programs list with a working uninstall entry

### Requirement: Publishing SHALL be automated and reproducible

A single script SHALL produce both distribution forms from a clean checkout, stamping the version from the project file and generating a SHA256 manifest for every artifact. Re-running the script with the same inputs SHALL produce byte-identical layout (same artifact names and file set).

#### Scenario: One-command release build

- **WHEN** a maintainer runs the publish script on a clean checkout
- **THEN** both packages and a SHA256 manifest SHALL be produced without manual steps

### Requirement: Upgrades SHALL preserve user data

Installing a newer version over an existing one SHALL NOT modify or delete the contents of the user's `%AppData%\Pulsar` directory (configuration, custom icons, plugin authorization records). Uninstallation SHALL keep user data by default and SHALL clearly communicate this behavior.

#### Scenario: Version upgrade keeps configuration

- **WHEN** the installer runs an upgrade over an existing installation with saved profiles
- **THEN** after first launch of the new version, all previous profiles and settings SHALL be intact

#### Scenario: Uninstall leaves user data

- **WHEN** the user uninstalls the app
- **THEN** `%AppData%\Pulsar` SHALL remain on disk by default and the uninstaller SHALL state this explicitly

### Requirement: Release artifacts SHALL pass a packaging smoke test

Before publication, each artifact SHALL be smoke-tested: the app starts, the single-instance guard holds for its distribution form, and the About page reports the packaged version.

#### Scenario: Single instance across forms

- **WHEN** the app is already running from any distribution form and a second instance is started
- **THEN** the second instance SHALL exit and focus SHALL go to the running instance
