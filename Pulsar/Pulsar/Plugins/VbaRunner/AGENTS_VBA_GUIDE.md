# Pulsar VBA Runner - AI Scripting Guide

This document provides instructions for AI agents on how to generate VBA scripts compatible with the Pulsar **VbaRunner** plugin.

## 1. Core Mechanism

The **VbaRunner** plugin executes scripts by **dynamically injecting** code into the active Excel or WPS Office instance via COM Automation.

- **Injection**: The script content is added as a temporary standard module (e.g., `Module1`) in the active workbook.
- **Execution**: The specified macro (entry point) is called immediately.
- **Cleanup**: The injected module is **deleted** immediately after execution (Success or Failure).
- **Context**: Scripts run within the user's active Excel/WPS session, having full access to `ActiveWorkbook`, `Selection`, etc.

**Critical Prerequisite**:
Users must enable **"Trust access to the VBA project object model"** in their Excel/WPS Trust Center settings. If this is not enabled, script injection will fail with a security error.

## 2. Script Directives (Metadata)

AI generated scripts MUST include specific metadata comments in the **first 100 lines** of the file to control plugin behavior.

### Supported Directives

| Directive | Value | Description |
| :--- | :--- | :--- |
| `' @Macro:` | `[Name]` | Specifies the entry point Sub name. Defaults to `Main` if omitted. |
| `' @Runner:` | `ShowSheetSelector` | If present, Pulsar shows a UI for the user to select a sheet *before* running the script. The selected sheet name is passed as a string argument to the entry Sub. |

## 3. Scripting Rules & Templates

### Scenario A: Standard Execution (No UI)

Use this for general tasks like formatting, data processing, or report generation on the active sheet.

**Requirements**:
- Entry Point: `Public Sub Main()` (No arguments).
- Convention: Use `ActiveSheet` or `Selection`.

**Template**:
```vba
' @Macro: Main
Option Explicit

Public Sub Main()
    On Error GoTo ErrorHandler
    
    ' Your logic here
    Dim rng As Range
    Set rng = Selection
    rng.Interior.Color = RGB(200, 200, 200)
    
    Exit Sub
ErrorHandler:
    MsgBox "Error: " & Err.Description, vbCritical
End Sub
```

### Scenario B: Targeted Sheet Operation (With Selector)

Use this when the user needs to pick a specific sheet to process (e.g., "Delete a specific sheet", "Summarize data from...").

**Requirements**:
- **Directive**: Must include `' @Runner: ShowSheetSelector`.
- **Entry Point**: Must accept **one string argument** (the sheet name).
- **Signature**: `Public Sub Main(targetSheetName As String)`

**Template**:
```vba
' @Runner: ShowSheetSelector
' @Macro: ProcessSheet
Option Explicit

' Note: The function name must match the @Macro directive
Public Sub ProcessSheet(targetSheetName As String)
    On Error GoTo ErrorHandler
    
    Dim ws As Worksheet
    Set ws = ActiveWorkbook.Sheets(targetSheetName)
    
    ' Logic utilizing the selected sheet
    ws.Activate
    MsgBox "You selected: " & ws.Name
    
    Exit Sub
ErrorHandler:
    MsgBox "Error accessing sheet '" & targetSheetName & "': " & Err.Description, vbCritical
End Sub
```

## 4. Best Practices for AI

1.  **Self-Contained Logic**: Since modules are transient, do not rely on `Public` variables persisting between runs.
2.  **Error Handling**: Always include `On Error` blocks. The plugin catches unhandled COM exceptions, but a VBA `MsgBox` provides a better user experience for logic errors.
3.  **Compatibility**: Write standard VBA compatible with both Microsoft Excel and WPS Office. Avoid referencing external DLLs/TypeLibs unless using Late Binding (`CreateObject`).
4.  **Cleanliness**: Do not modify the `ThisWorkbook` or other existing modules. The plugin handles the lifecycle of your code in its own module.
5.  **User Feedback**: Use `MsgBox` to confirm completion for long-running tasks, as the plugin UI is hidden during execution.
6.  **No Busy-Wait Needed**: The runner automatically handles Excel "Busy" states (e.g., cell editing mode, open dialogs) by retrying the injection and execution. **Do not** add complex retry loops or `Application.Ready` checks in your VBA script; focus on the business logic.

## 5. Troubleshooting Common Errors

- **"Programmatic access to Visual Basic Project is not trusted"**:
  - **Cause**: Excel security setting.
  - **Fix**: User must go to `File > Options > Trust Center > Trust Center Settings > Macro Settings` and check **"Trust access to the VBA project object model"**.

- **"Macro not found"**:
  - **Cause**: The `@Macro` directive name does not match the actual `Sub` name in the code.
  - **Fix**: Ensure exact spelling match.

- **"Wrong number of arguments"**:
  - **Cause**: Using `@Runner: ShowSheetSelector` but the `Sub` does not accept a string argument (or vice-versa).
  - **Fix**: Update the `Sub` signature to match the runner context.
