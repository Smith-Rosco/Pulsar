# VbaRunner Plugin

**插件 ID**: `com.pulsar.vbarunner`  
**版本**: 1.0.0  
**类型**: Extension Plugin  
**作者**: Pulsar Team

## 概述

VbaRunner 是一个强大的自动化插件，允许在 Excel 或 WPS Office 中执行 VBA 脚本，支持交互式工作表选择和上下文感知执行。

## 功能特性

- **COM 互操作**: 通过 COM 接口连接到 Excel/WPS
- **进程感知**: 自动连接到当前活动的 Excel 实例
- **交互式选择**: 支持弹出对话框让用户选择目标工作表
- **脚本指令**: 通过注释指令控制脚本行为
- **错误处理**: 完善的异常捕获和用户反馈

## 支持的动作

### `run` - 运行 VBA 脚本

在当前 Excel/WPS 实例中执行指定的 VBA 脚本。

**参数**:
- `scriptPath` (必需): VBA 脚本文件路径（.vba 或 .bas）
- `macro` (可选): 要执行的宏名称，默认为 "Main"

**示例**:
```json
{
  "PluginId": "com.pulsar.vbarunner",
  "Action": "run",
  "Args": {
    "scriptPath": "%USERPROFILE%\\Documents\\Scripts\\FormatTable.vba",
    "macro": "Main"
  }
}
```

## 脚本指令

在 VBA 脚本顶部使用注释指令控制行为：

### `' @Directive: ShowSheetSelector`

显示工作表选择对话框，用户选择的工作表名称会作为参数传递给宏。

**示例脚本**:
```vba
' @Directive: ShowSheetSelector

Sub Main(sheetName As Variant)
    If IsEmpty(sheetName) Then Exit Sub
    
    Dim ws As Worksheet
    Set ws = ThisWorkbook.Sheets(sheetName)
    
    ' 在选定的工作表上执行操作
    ws.Range("A1").Value = "Hello from " & sheetName
End Sub
```

## 执行流程

1. **验证脚本**: 检查脚本文件是否存在
2. **读取脚本**: 加载脚本内容并解析指令
3. **隐藏 Pulsar**: 隐藏径向菜单窗口
4. **恢复焦点**: 将焦点返回到 Excel 窗口
5. **连接 Excel**: 通过 COM 连接到目标进程
6. **处理指令**: 如果有 `ShowSheetSelector`，显示选择对话框
7. **执行脚本**: 注入并运行 VBA 代码
8. **恢复焦点**: 将焦点返回到原始窗口

## 依赖服务

- `IWindowService`: 窗口管理服务
- `ScriptEngine`: VBA 脚本执行引擎

## 依赖插件

- `com.pulsar.winswitcher`: 提供 `IWindowService`

## 支持的应用

- Microsoft Excel (2010+)
- WPS Office Spreadsheets

## 脚本开发指南

### 基本模板

```vba
Sub Main()
    ' 你的代码
    MsgBox "Script executed successfully!"
End Sub
```

### 带参数的模板

```vba
' @Directive: ShowSheetSelector

Sub Main(sheetName As Variant)
    If IsEmpty(sheetName) Then
        MsgBox "No sheet selected"
        Exit Sub
    End If
    
    Dim ws As Worksheet
    Set ws = ThisWorkbook.Sheets(sheetName)
    
    ' 操作工作表
End Sub
```

### 错误处理

```vba
Sub Main()
    On Error GoTo ErrorHandler
    
    ' 你的代码
    
    Exit Sub
    
ErrorHandler:
    MsgBox "Error: " & Err.Description
End Sub
```

## 注意事项

1. **Excel 必须运行**: 脚本执行前必须有打开的 Excel/WPS 实例
2. **宏安全性**: 确保 Excel 的宏安全设置允许运行 VBA 代码
3. **STA 线程**: COM 操作在 UI 线程上执行，避免线程问题
4. **环境变量**: `scriptPath` 支持环境变量展开（如 `%USERPROFILE%`）

## 故障排除

**问题**: "No active Excel/WPS instance found"  
**解决**: 在运行脚本前打开 Excel 并创建或打开一个工作簿

**问题**: 脚本执行但无效果  
**解决**: 检查宏名称是否正确，默认为 "Main"

**问题**: "Operation cancelled by user"  
**解决**: 用户在工作表选择对话框中点击了取消，这是正常行为

**问题**: COM 异常  
**解决**: 
- 确保 Excel/WPS 已正确安装
- 检查是否有多个 Excel 实例运行
- 尝试重启 Excel

## 性能优化

- 脚本内容会被缓存读取，避免重复 I/O
- COM 连接会尝试重用现有实例
- 使用 `Application.ScreenUpdating = False` 提升脚本执行速度

---

**最后更新**: 2026-03-01
