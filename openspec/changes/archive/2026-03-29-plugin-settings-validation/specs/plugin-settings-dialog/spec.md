## ADDED Requirements

### Requirement: Configure button opens PluginSettingsDialog
When a user clicks the Configure button on a plugin card that has settings, the system MUST open a modal dialog showing all configurable settings.

#### Scenario: Configure button opens dialog for plugin with settings
- **WHEN** a user clicks Configure on a plugin where HasSettings is true
- **THEN** a modal dialog MUST open displaying the plugin's settings

#### Scenario: Configure button shows placeholder for plugin without settings
- **WHEN** a user clicks Configure on a plugin where HasSettings is false
- **THEN** the system MUST NOT open a dialog; instead, it MAY show a message indicating no settings are available

### Requirement: PluginSettingsDialog displays plugin metadata
The dialog header MUST show the plugin's icon, name, and description so users know which plugin they are configuring.

#### Scenario: Dialog header shows plugin info
- **WHEN** the PluginSettingsDialog opens
- **THEN** the dialog MUST display the plugin's icon, DisplayName, and Description

### Requirement: PluginSettingsDialog renders all setting types
The dialog MUST render each setting using the appropriate control based on its PluginSettingType.

#### Scenario: Boolean setting renders toggle switch
- **WHEN** a setting has type Boolean
- **THEN** the dialog MUST render a ToggleSwitch control bound to the setting value

#### Scenario: String setting renders text box
- **WHEN** a setting has type String
- **THEN** the dialog MUST render a TextBox control bound to the setting value

#### Scenario: Selection setting renders combo box
- **WHEN** a setting has type Selection
- **THEN** the dialog MUST render a ComboBox with the available Options, selected item bound to the setting value

#### Scenario: Path setting renders path picker
- **WHEN** a setting has type Path
- **THEN** the dialog MUST render a TextBox with a browse button that opens a folder/file picker dialog

#### Scenario: Integer setting renders number input
- **WHEN** a setting has type Integer
- **THEN** the dialog MUST render a numeric input control (NumberBox or similar) bound to the setting value

#### Scenario: Secret setting renders password input
- **WHEN** a setting has type Secret
- **THEN** the dialog MUST render a PasswordBox or masked input control bound to the setting value

### Requirement: PluginSettingsDialog shows validation inline
Each setting in the dialog MUST display its validation state inline.

#### Scenario: Invalid setting shows error message
- **WHEN** a setting has IsValid=false
- **THEN** an error icon and ValidationMessage MUST be displayed below the control

#### Scenario: Save button is disabled when validation fails
- **WHEN** any setting in the dialog has IsValid=false
- **THEN** the Save button MUST be disabled or show an error count badge

#### Scenario: All settings valid enables Save button
- **WHEN** all settings in the dialog have IsValid=true
- **THEN** the Save button MUST be enabled

### Requirement: PluginSettingsDialog provides Save and Cancel actions
The dialog MUST have Save and Cancel buttons with appropriate behavior.

#### Scenario: Save persists changes and closes dialog
- **WHEN** a user clicks Save and all validations pass
- **THEN** the settings MUST be persisted to ConfigService, the plugin's UpdateSettings() MUST be called, and the dialog MUST close

#### Scenario: Cancel discards changes and closes dialog
- **WHEN** a user clicks Cancel
- **THEN** any unsaved changes MUST be discarded and the dialog MUST close without calling UpdateSettings()

#### Scenario: Reset to Defaults resets all settings
- **WHEN** a user clicks "Reset to Defaults"
- **THEN** all settings MUST be restored to their DefaultValue from PluginSettingDefinition

### Requirement: Dialog is properly sized and scrollable
The dialog MUST be sized appropriately and scrollable for plugins with many settings.

#### Scenario: Dialog with many settings scrolls
- **WHEN** a plugin has more settings than fit in the dialog viewport
- **THEN** a vertical scrollbar MUST appear to allow scrolling through all settings

#### Scenario: Dialog size constraints are respected
- **WHEN** the dialog renders
- **THEN** it MUST respect DialogSizeConstraints (e.g., Large for complex plugins)
