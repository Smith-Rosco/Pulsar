# VbaRunner Directive Reference

## Overview

Directives are special comments at the top of VBA scripts that control how the VbaRunner plugin executes your script. They enable smart behavior like prerequisite validation, conditional UI display, and automatic setup routines.

## Directive Syntax

Directives must appear in the first 100 lines of your VBA script and follow this format:

```vba
' @DirectiveName: value
```

## Available Directives

### @Runner

**Purpose**: Controls UI interaction mode before script execution.

**Values**:
- `None` - No UI interaction, direct execution (default)
- `ShowSheetSelector` - Display sheet selection dialog

**Example**:
```vba
' @Runner: ShowSheetSelector
```

---

### @Macro

**Purpose**: Specifies the entry point macro name.

**Default**: `Main`

**Example**:
```vba
' @Macro: ProcessData
```

---

### @Requires

**Purpose**: Declares prerequisites that must exist before main execution. If prerequisites are missing, the `@OnMissing` macro is called instead.

**Formats**:
- `Sheet=SheetName` - Requires specific sheet to exist
- `Cell=A1` - Requires cell to have non-empty value
- `Range=A1:B10` - Requires at least one non-empty cell in range

**Multiple Requirements**: Use multiple `@Requires` lines.

**Examples**:
```vba
' @Requires: Sheet="_Config_Input"
' @Requires: Cell=A1
' @Requires: Range=B5:B10
```

---

### @OnMissing

**Purpose**: Specifies the macro to call when prerequisites are not met. This macro should handle setup/initialization logic.

**Default**: `Setup`

**Example**:
```vba
' @OnMissing: InitializeWorkbook
```

**Implementation Pattern**:
```vba
Public Sub Setup()
    ' Create missing sheets, templates, etc.
    CreateConfigTemplate ActiveWorkbook
    MsgBox "Setup completed. Please configure and run again.", vbInformation
End Sub
```

---

### @SheetFilter

**Purpose**: Filters sheets displayed in the selector dialog.

**Formats**:
- `exclude:pattern` - Hide sheets matching pattern
- `include:pattern` - Show only sheets matching pattern

**Wildcards**:
- `*` - Matches any characters
- `?` - Matches single character

**Examples**:
```vba
' @SheetFilter: exclude:_Config_*
' @SheetFilter: exclude:_*
' @SheetFilter: include:Data*
```

---

### @AutoSelectSingle

**Purpose**: Automatically select if only one valid sheet exists (skips selector dialog).

**Values**: `true` or `false`

**Default**: `false`

**Example**:
```vba
' @AutoSelectSingle: true
```

---

## Complete Example

### Scenario: Script with Configuration Prerequisites

```vba
' @Runner: ShowSheetSelector
' @Requires: Sheet="_Config_Input"
' @OnMissing: Setup
' @SheetFilter: exclude:_Config_*
' @AutoSelectSingle: false
' @Macro: Main
Option Explicit

' Setup macro - called when _Config_Input sheet is missing
Public Sub Setup()
    Dim wb As Workbook
    Set wb = ActiveWorkbook
    
    If wb Is Nothing Then
        MsgBox "Error: No active workbook", vbCritical
        Exit Sub
    End If
    
    ' Create configuration sheet
    Dim ws As Worksheet
    Set ws = wb.Worksheets.Add(Before:=wb.Worksheets(1))
    ws.Name = "_Config_Input"
    
    ' Add template data
    ws.Cells(1, 1).Value = "Configuration Template"
    ws.Cells(2, 1).Value = "Please fill in the required values"
    
    MsgBox "Configuration sheet created. Please fill it and run again.", vbInformation
End Sub

' Main macro - called when prerequisites are met
Public Sub Main(targetSheetName As String)
    Dim wb As Workbook
    Dim wsConfig As Worksheet
    Dim wsTarget As Worksheet
    
    Set wb = ActiveWorkbook
    Set wsConfig = wb.Worksheets("_Config_Input")
    Set wsTarget = wb.Worksheets(targetSheetName)
    
    ' Your main logic here
    MsgBox "Processing sheet: " & targetSheetName, vbInformation
End Sub
```

---

## Execution Flow

### Without Prerequisites

1. Plugin reads script and parses directives
2. Connects to Excel/WPS
3. Shows UI (if `@Runner: ShowSheetSelector`)
4. Executes `@Macro` (default: `Main`)

### With Prerequisites

1. Plugin reads script and parses directives
2. Connects to Excel/WPS
3. **Validates prerequisites** (`@Requires`)
4. **If prerequisites missing**:
   - Executes `@OnMissing` macro (default: `Setup`)
   - Shows message to user
   - **Exits** (does not show selector or run main macro)
5. **If prerequisites met**:
   - Shows UI (if `@Runner: ShowSheetSelector`)
   - Executes `@Macro` (default: `Main`)

---

## Best Practices

### 1. Always Provide Setup Macro

If you use `@Requires`, always implement the setup macro:

```vba
' @Requires: Sheet="_Config"
' @OnMissing: Setup

Public Sub Setup()
    ' Create missing prerequisites
End Sub
```

### 2. Filter Configuration Sheets

Prevent users from selecting configuration sheets:

```vba
' @SheetFilter: exclude:_*
```

### 3. Use Descriptive Macro Names

```vba
' @Macro: GenerateReport
' @OnMissing: CreateReportTemplate
```

### 4. Validate Multiple Prerequisites

```vba
' @Requires: Sheet="_Config"
' @Requires: Cell=A1
' @Requires: Range=B5:B10
```

### 5. Auto-Select for Single-Sheet Operations

```vba
' @AutoSelectSingle: true
```

---

## Backward Compatibility

Scripts without directives continue to work as before:

```vba
' Old script without directives
Public Sub Main()
    ' Direct execution
End Sub
```

---

## Troubleshooting

### Issue: Setup macro not called

**Cause**: Prerequisite validation failed silently (fail-open behavior)

**Solution**: Check logs for validation errors

### Issue: Sheet filter not working

**Cause**: Invalid pattern syntax

**Solution**: Use valid wildcards (`*`, `?`) and correct format (`exclude:pattern`)

### Issue: Selector shows all sheets despite filter

**Cause**: Filter pattern doesn't match any sheets

**Solution**: Verify pattern matches sheet names (case-insensitive)

---

## Migration Guide

### Updating Existing Scripts

**Before**:
```vba
' @Runner: ShowSheetSelector
' @Macro: Main

Public Sub Main(targetSheetName As String)
    ' Check if config exists
    If ConfigSheet Is Nothing Then
        CreateConfig
        Exit Sub
    End If
    ' Main logic
End Sub
```

**After**:
```vba
' @Runner: ShowSheetSelector
' @Requires: Sheet="_Config"
' @OnMissing: Setup
' @Macro: Main

Public Sub Setup()
    CreateConfig
    MsgBox "Config created. Run again.", vbInformation
End Sub

Public Sub Main(targetSheetName As String)
    ' Main logic (config guaranteed to exist)
End Sub
```

---

## See Also

- [VbaRunner Plugin Documentation](./VbaRunner.md)
- [Plugin Development Guide](../../PLUGIN_DEVELOPMENT.md)
- [Architecture Overview](../../ARCHITECTURE.md)
