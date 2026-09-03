## Purpose

Define the guided onboarding scenario for the first web script: scenario metadata, a step sequence, and prerequisite handling that lead a new user from opening a legacy web page to running their first bookmarklet script, reusing the existing tutorial scenario mechanism.

## ADDED Requirements

### Requirement: Web-script scenario defines a first-script walkthrough
The system SHALL define a web-script onboarding scenario that demonstrates creating and running a bookmarklet script in addition to window switching, mirroring the existing scenario structure.

#### Scenario: Scenario has correct metadata
- **WHEN** the web-script scenario is defined in the scenario registry
- **THEN** its `Id` SHALL be a stable web-script identifier
- **AND** its `StepsJsonPath` SHALL point to a web-script steps file
- **AND** its `PrerequisiteProvider` SHALL validate that a browser is available

#### Scenario: Scenario generates a primary web-script slot
- **WHEN** the scenario's initial configuration is generated and a browser is available
- **THEN** the command slots SHALL include a bookmarklet slot with `PluginId = "com.pulsar.bookmarklet"`, `Action = "run"`, marked as the tutorial primary slot

### Requirement: Web-script steps guide the user through the first run
The web-script steps JSON SHALL follow the standard step structure with copy that leads the user from opening a legacy page to running the script.

#### Scenario: Early steps orient the user to the web-script slot
- **WHEN** a user follows the early steps of the web-script scenario
- **THEN** the instructions SHALL show the user how to invoke the web-script slot (radial menu gesture) on the target page

#### Scenario: Final step executes the script
- **WHEN** the user reaches the final step of the web-script scenario
- **THEN** the instruction SHALL direct the user to run the bookmarklet script and observe the page change

### Requirement: Scenario handles missing browser gracefully
The web-script scenario SHALL fall back gracefully when a browser prerequisite is not met, so the onboarding remains understandable.

#### Scenario: Browser unavailable
- **WHEN** the web-script scenario's prerequisite check finds no usable browser
- **THEN** the scenario SHALL present a readable message about the missing prerequisite instead of failing silently
