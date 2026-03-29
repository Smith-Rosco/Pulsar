## ADDED Requirements

### Requirement: PluginSettingViewModel provides validation state
The PluginSettingViewModel base class and its subclasses MUST expose validation-related properties so the UI can display error and warning states.

#### Scenario: Setting is valid after creation
- **WHEN** a PluginSettingViewModel is created with a valid value
- **THEN** IsValid MUST be true, ValidationMessage MUST be empty, and HasValidation MUST be false

#### Scenario: Setting becomes invalid on value change
- **WHEN** a user changes a setting value and validation fails
- **THEN** IsValid MUST be set to false, ValidationMessage MUST contain the error message, and HasValidation MUST be true

#### Scenario: Setting regains validity after correction
- **WHEN** a user corrects an invalid setting to a valid value
- **THEN** IsValid MUST be set to true, ValidationMessage MUST be cleared, and HasValidation MUST be false

### Requirement: Setting validates on value change
When a user modifies a setting value in the UI, the system MUST run validation before persisting the change.

#### Scenario: Valid value is saved immediately
- **WHEN** a user enters a valid value for a setting
- **THEN** validation MUST pass and the value MUST be saved to ConfigService

#### Scenario: Invalid value is blocked from saving
- **WHEN** a user enters an invalid value for a setting
- **THEN** validation MUST fail, the value MUST NOT be saved, and validation feedback MUST be displayed

#### Scenario: Required field validation
- **WHEN** a setting is marked as required (IsRequired=true) and the user clears the value
- **THEN** validation MUST fail with message "This field is required."

#### Scenario: String length validation
- **WHEN** a setting has MinLength or MaxLength constraints and the user enters a value outside the range
- **THEN** validation MUST fail with an appropriate message indicating the length requirement

#### Scenario: Numeric range validation
- **WHEN** a setting has MinValue or MaxValue constraints and the user enters a number outside the range
- **THEN** validation MUST fail with an appropriate message indicating the allowed range

#### Scenario: Regex pattern validation
- **WHEN** a setting has a Pattern constraint and the user enters a value that does not match
- **THEN** validation MUST fail with message "Value does not match required format."

### Requirement: Missing Setting ViewModels are implemented
The system MUST provide ViewModel implementations for all PluginSettingType values.

#### Scenario: PathSettingViewModel renders file/folder picker
- **WHEN** a plugin defines a setting with PluginSettingType.Path
- **THEN** the UI MUST render an appropriate control for path selection (folder browser or text input with browse button)

#### Scenario: IntegerSettingViewModel renders numeric input
- **WHEN** a plugin defines a setting with PluginSettingType.Integer
- **THEN** the UI MUST render a numeric input control with optional MinValue/MaxValue constraints

#### Scenario: SecretSettingViewModel renders masked input
- **WHEN** a plugin defines a setting with PluginSettingType.Secret
- **THEN** the UI MUST render a PasswordBox or masked text input

### Requirement: Validation results are displayed inline
The UI MUST show validation errors and warnings directly on the affected setting control.

#### Scenario: Error state displays red indicator
- **WHEN** a setting fails validation with Error severity
- **THEN** the control MUST display a red border or error icon, and ValidationMessage MUST be visible below the control

#### Scenario: Warning state displays yellow indicator
- **WHEN** a setting passes validation but triggers a warning (e.g., .exe extension in process name)
- **THEN** the control MUST display a yellow/warning indicator and the warning message MUST be visible

#### Scenario: Valid state displays green indicator
- **WHEN** a setting passes all validation rules
- **THEN** the control MAY display a green checkmark or simply have default styling (no error indication)
