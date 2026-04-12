## ADDED Requirements

### Requirement: Window switching SHALL be decomposed into explicit inventory, tracking, selection, and activation responsibilities
Pulsar SHALL implement window switching through distinct responsibilities for window inventory, tracking, selection, and activation so architectural boundaries remain explicit and independently testable.

#### Scenario: Switching services are composed for app switching
- **WHEN** Pulsar resolves a target window for an app-switching flow
- **THEN** inventory, tracking, selection, and activation responsibilities SHALL be delegated through explicit components rather than embedded as unrelated logic inside one monolithic service

### Requirement: Selection requests SHALL express switching intent explicitly
Pulsar SHALL represent switching decisions with an explicit selection request that declares the calling intent instead of inferring behavior solely from ad hoc skip flags.

#### Scenario: Entry point requests process activation target
- **WHEN** the WinSwitcher plugin requests a target window for a process activation flow
- **THEN** it SHALL issue a selection request that identifies the process-activation intent so the selection engine can apply the correct skip and ranking behavior

#### Scenario: Entry point requests submenu default target
- **WHEN** the radial menu determines the default target for a submenu of windows
- **THEN** it SHALL issue a submenu-specific selection request rather than reusing an unrelated process-switch assumption

### Requirement: Selection inputs SHALL expose explicit ordering semantics
Pulsar SHALL expose real activation recency, visual stacking order, and stable display ordering as distinct selection inputs rather than storing those concepts under ambiguous shared field names.

#### Scenario: Candidate includes tracked recency and display order
- **WHEN** the selection engine receives a candidate window with both activation history and stable display metadata
- **THEN** it SHALL be able to consume those inputs independently without interpreting one field as both recency and display order

### Requirement: Quick switch SHALL be modeled as a dedicated stateful engine
Pulsar SHALL implement quick switch through a dedicated stateful component that owns history, active pair state, and timeout behavior.

#### Scenario: Quick switch uses pair state
- **WHEN** Pulsar performs a quick switch within the active timeout window
- **THEN** the quick-switch component SHALL resolve the reverse target from its managed pair state rather than relying on incidental state in a general switching service
