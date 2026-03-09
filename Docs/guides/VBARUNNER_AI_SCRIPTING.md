# VbaRunner - AI Scripting Guide

**Document Type**: Guide  
**Audience**: AI Agents  
**Last Updated**: 2026-03-09  
**Version**: 2.0.0 (Smart Directive System)

---

## Overview

This guide provides instructions for AI agents on how to generate VBA scripts compatible with the Pulsar **VbaRunner** plugin. The plugin features a **Smart Directive System** that validates prerequisites and adapts execution flow based on script requirements.

---

## 1. Core Mechanism

The **VbaRunner** plugin executes scripts by **dynamically injecting** code into the active Excel or WPS Office instance via COM Automation.

### Execution Flow

1. **Injection**: Script content is added as a temporary standard module (e.g., `Pulsar_abc12345`) in the active workbook
2. **Validation**: Prerequisites are checked before main execution (if declared)
3. **Execution**: The specified macro (entry point) is called immediately
4. **Cleanup**: The injected module is **deleted** immediately after execution (success or failure)

### Context

Scripts run within the user's active Excel/WPS session, having full access to:
- `ActiveWorkbook`
- `ActiveSheet`
- `Selection`
- All standard Excel/WPS VBA objects

### Critical Prerequisite

Users must enable **"Trust access to the VBA project object model"** in their Excel/WPS Trust Center settings:
- Path: `File > Options > Trust Center > Trust Center Settings > Macro Settings`
- Setting: Check **"Trust access to the VBA project object model"**

If not enabled, script injection will fail with security error.

---

## 2. Smart Directive System

### Overview

Directives are metadata comments in the **first 100 lines** that control plugin behavior. The plugin validates prerequisites before execution and can run setup routines automatically.

### Execution Phases

```
Phase 1: Parse Directives
    ↓
Phase 2: Validate Prerequisites (@Requires)
    ↓ (if missing)
    Run @OnMissing macro → Exit
    ↓ (if valid)
Phase 3: Show UI (@Runner) + Execute @Macro
```

### Available Directives

| Directive | Purpose | Default | Example |
|-----------|---------|---------|---------|
| `@Runner` | UI interaction mode | `None` | `@Runner: ShowSheetSelector` |
| `@Macro` | Entry point name | `Main` | `@Macro: ProcessData` |
| `@Requires` | Prerequisites to validate | (none) | `@Requires: Sheet="_Config"` |
| `@OnMissing` | Setup macro when prerequisites fail | `Setup` | `@OnMissing: Initialize` |
| `@SheetFilter` | Filter selector options | (none) | `@SheetFilter: exclude:_Config_*` |
| `@AutoSelectSingle` | Skip selector if 1 option | `false` | `@AutoSelectSingle: true` |

### Directive Details

#### @Runner

Controls UI interaction before script execution.

**Values**:
- `None` - No UI, direct execution (default)
- `ShowSheetSelector` - Display sheet selection dialog

**Example**:
```vba
' @Runner: ShowSheetSelector
```

#### @Macro

Specifies the entry point macro name.

**Default**: `Main`

**Example**:
```vba
' @Macro: ProcessReport
```

#### @Requires

Declares prerequisites that must exist before main execution. If missing, `@OnMissing` macro is called instead.

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

#### @OnMissing

Specifies the macro to call when prerequisites are not met.

**Default**: `Setup`

**Example**:
```vba
' @OnMissing: InitializeWorkbook
```

#### @SheetFilter

Filters sheets displayed in selector dialog.

**Formats**:
- `exclude:pattern` - Hide sheets matching pattern
- `include:pattern` - Show only sheets matching pattern

**Wildcards**: `*` (any chars), `?` (single char)

**Examples**:
```vba
' @SheetFilter: exclude:_Config_*
' @SheetFilter: include:Data*
```

#### @AutoSelectSingle

Automatically select if only one valid sheet exists (skips selector).

**Values**: `true` or `false`  
**Default**: `false`

**Example**:
```vba
' @AutoSelectSingle: true
```

---

## 3. Script Templates

### Template A: Standard Execution (No UI, No Prerequisites)

Use for general tasks like formatting, data processing on active sheet.

```vba
' @Macro: Main
Option Explicit

Public Sub Main()
    On Error GoTo ErrorHandler
    
    ' Your logic here
    Dim rng As Range
    Set rng = Selection
    rng.Interior.Color = RGB(200, 200, 200)
    
    MsgBox "Processing complete", vbInformation
    Exit Sub
    
ErrorHandler:
    MsgBox "Error: " & Err.Description, vbCritical
End Sub
```

**Signature**: `Public Sub Main()` (no arguments)

---

### Template B: Sheet Selector (No Prerequisites)

Use when user needs to pick a specific sheet to process.

```vba
' @Runner: ShowSheetSelector
' @Macro: ProcessSheet
Option Explicit

Public Sub ProcessSheet(targetSheetName As String)
    On Error GoTo ErrorHandler
    
    Dim ws As Worksheet
    Set ws = ActiveWorkbook.Sheets(targetSheetName)
    
    ' Logic utilizing the selected sheet
    ws.Activate
    MsgBox "Processing sheet: " & ws.Name, vbInformation
    
    Exit Sub
    
ErrorHandler:
    MsgBox "Error accessing sheet '" & targetSheetName & "': " & Err.Description, vbCritical
End Sub
```

**Signature**: `Public Sub ProcessSheet(targetSheetName As String)`

---

### Template C: Smart Script with Prerequisites (Recommended)

Use when script requires configuration or setup before main execution.

```vba
' @Runner: ShowSheetSelector
' @Requires: Sheet="_Config_Input"
' @OnMissing: Setup
' @SheetFilter: exclude:_Config_*
' @AutoSelectSingle: false
' @Macro: Main
Option Explicit

Private Const CONFIG_SHEET As String = "_Config_Input"

' Setup macro - called when prerequisites are missing
Public Sub Setup()
    Dim wb As Workbook
    Dim ws As Worksheet
    
    Set wb = ActiveWorkbook
    If wb Is Nothing Then
        MsgBox "Error: No active workbook", vbCritical
        Exit Sub
    End If
    
    ' Create configuration sheet
    Set ws = wb.Worksheets.Add(Before:=wb.Worksheets(1))
    ws.Name = CONFIG_SHEET
    
    ' Add template data
    ws.Cells(1, 1).Value = "Configuration Template"
    ws.Cells(2, 1).Value = "Please fill in required values:"
    ws.Cells(4, 1).Value = "Setting 1:"
    ws.Cells(5, 1).Value = "Setting 2:"
    
    MsgBox "Configuration sheet created." & vbCrLf & _
           "Please fill it and run again.", vbInformation
End Sub

' Main macro - called when prerequisites are met
Public Sub Main(targetSheetName As String)
    On Error GoTo ErrorHandler
    
    Dim wb As Workbook
    Dim wsConfig As Worksheet
    Dim wsTarget As Worksheet
    
    Set wb = ActiveWorkbook
    
    ' Config sheet guaranteed to exist (validated by plugin)
    Set wsConfig = wb.Worksheets(CONFIG_SHEET)
    Set wsTarget = wb.Worksheets(targetSheetName)
    
    ' Read configuration
    Dim setting1 As String
    Dim setting2 As String
    setting1 = wsConfig.Cells(4, 2).Value
    setting2 = wsConfig.Cells(5, 2).Value
    
    ' Process target sheet with configuration
    wsTarget.Activate
    MsgBox "Processing " & targetSheetName & vbCrLf & _
           "Using config: " & setting1 & ", " & setting2, vbInformation
    
    Exit Sub
    
ErrorHandler:
    MsgBox "Error: " & Err.Description, vbCritical
End Sub
```

**Key Points**:
- Setup runs automatically when config missing
- Main execution only happens when prerequisites are met
- No need to check for config existence in Main
- Sheet selector excludes config sheets

---

## 4. Best Practices for AI

### Do:

1. **Use Smart Directives** - Leverage `@Requires` and `@OnMissing` for scripts with prerequisites
2. **Self-Contained Logic** - Modules are transient; don't rely on `Public` variables persisting
3. **Error Handling** - Always include `On Error GoTo ErrorHandler` blocks
4. **User Feedback** - Use `MsgBox` for completion/error messages
5. **Compatibility** - Write standard VBA compatible with both Excel and WPS Office
6. **Filter Config Sheets** - Use `@SheetFilter: exclude:_*` to hide internal sheets
7. **Validate in Setup** - Check if setup completed successfully before exiting

### Don't:

1. **Don't Check Prerequisites in Main** - Plugin validates before calling Main
2. **Don't Modify Existing Modules** - Plugin handles lifecycle in its own module
3. **Don't Use Busy-Wait Loops** - Plugin handles Excel "Busy" states automatically
4. **Don't Reference External DLLs** - Use Late Binding (`CreateObject`) if needed
5. **Don't Show Selector for Config Sheets** - Always filter them out

---

## 5. Common Patterns

### Pattern: Multi-Step Setup

```vba
' @Requires: Sheet="_Config"
' @Requires: Cell=A1
' @OnMissing: Setup

Public Sub Setup()
    ' Step 1: Create sheet
    CreateConfigSheet
    
    ' Step 2: Validate
    If Not ValidateSetup() Then
        MsgBox "Setup incomplete. Please fill required fields.", vbExclamation
        Exit Sub
    End If
    
    MsgBox "Setup complete. Run again to process.", vbInformation
End Sub
```

### Pattern: Auto-Select Single Sheet

```vba
' @Runner: ShowSheetSelector
' @SheetFilter: include:Data*
' @AutoSelectSingle: true

' If only one "Data*" sheet exists, auto-selects and runs
Public Sub Main(targetSheetName As String)
    ' Process the auto-selected or user-selected sheet
End Sub
```

### Pattern: Multiple Prerequisites

```vba
' @Requires: Sheet="_Config"
' @Requires: Sheet="_Template"
' @Requires: Cell=A1
' @OnMissing: Setup

Public Sub Setup()
    ' Create all missing prerequisites
    CreateConfigSheet
    CreateTemplateSheet
    InitializeCell
End Sub
```

---

## 6. Troubleshooting

### Error: "Programmatic access to Visual Basic Project is not trusted"

**Cause**: Excel security setting not enabled.

**Fix**: User must enable "Trust access to the VBA project object model" in Trust Center.

---

### Error: "Macro not found"

**Cause**: `@Macro` directive name doesn't match actual `Sub` name.

**Fix**: Ensure exact spelling match between directive and Sub name.

---

### Error: "Wrong number of arguments"

**Cause**: Mismatch between `@Runner` directive and Sub signature.

**Fix**:
- `@Runner: ShowSheetSelector` → `Sub Main(targetSheetName As String)`
- No `@Runner` or `@Runner: None` → `Sub Main()`

---

### Issue: Setup macro not called

**Cause**: Prerequisites validation passed (sheet already exists).

**Fix**: Delete the prerequisite sheet to trigger setup, or check logs for validation errors.

---

### Issue: Selector shows config sheets

**Cause**: Missing or incorrect `@SheetFilter` directive.

**Fix**: Add `@SheetFilter: exclude:_*` to hide sheets starting with underscore.

---

## 7. Migration Guide

### Upgrading Old Scripts

**Before** (Manual prerequisite checking):
```vba
' @Runner: ShowSheetSelector
' @Macro: Main

Public Sub Main(targetSheetName As String)
    ' Check if config exists
    Dim wsConfig As Worksheet
    On Error Resume Next
    Set wsConfig = ActiveWorkbook.Worksheets("_Config")
    On Error GoTo 0
    
    If wsConfig Is Nothing Then
        CreateConfig
        MsgBox "Config created. Run again."
        Exit Sub
    End If
    
    ' Main logic...
End Sub
```

**After** (Smart Directives):
```vba
' @Runner: ShowSheetSelector
' @Requires: Sheet="_Config"
' @OnMissing: Setup
' @SheetFilter: exclude:_*
' @Macro: Main

Public Sub Setup()
    CreateConfig
    MsgBox "Config created. Run again.", vbInformation
End Sub

Public Sub Main(targetSheetName As String)
    ' Config guaranteed to exist
    ' Main logic...
End Sub
```

**Benefits**:
- Cleaner separation of setup vs. main logic
- No selector shown when setup needed
- Plugin handles validation automatically

---

## 8. Reference

### Complete Directive List

```vba
' @Runner: ShowSheetSelector
' @Macro: Main
' @Requires: Sheet="_Config"
' @Requires: Cell=A1
' @Requires: Range=B5:B10
' @OnMissing: Setup
' @SheetFilter: exclude:_*
' @AutoSelectSingle: true
```

### Signature Reference

| Scenario | Signature |
|----------|-----------|
| No UI | `Public Sub Main()` |
| Sheet Selector | `Public Sub Main(targetSheetName As String)` |
| Setup Macro | `Public Sub Setup()` |

---

## See Also

- **[VbaRunner Plugin Documentation](./VbaRunner.md)** - User-facing plugin documentation
- **[VbaRunner Directive Reference](./VbaRunner_Directives.md)** - Complete directive specification
- **[Smart Directives Implementation](./VbaRunner_SmartDirectives_Implementation.md)** - Architecture details
- **[Plugin Development Guide](../../PLUGIN_DEVELOPMENT.md)** - General plugin development

---

**Maintained by**: Pulsar Development Team  
**Version History**:
- v2.0.0 (2026-03-09): Added Smart Directive System with prerequisite validation
- v1.0.0 (2025-12-01): Initial AI scripting guide
