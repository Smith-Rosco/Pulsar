# BasicCommand Plugin

**插件 ID**: `com.pulsar.command`  
**版本**: 1.0.0  
**类型**: Extension Plugin  
**作者**: Pulsar Team

## 概述

BasicCommand 是一个通用工具插件，提供简单的命令执行和键盘模拟功能。

## 功能特性

- **命令执行**: 启动外部程序、打开文件或 URL
- **键盘模拟**: 发送键盘按键序列到活动窗口
- **工作目录支持**: 指定命令的工作目录
- **延迟控制**: 可配置按键发送延迟

## 支持的动作

### 1. `run` - 运行命令

执行外部命令或打开文件/URL。

**参数**:
- `path` (必需): 可执行文件路径、文件路径或 URL
- `arguments` (可选): 命令行参数
- `workingDir` (可选): 工作目录

**示例**:

```json
// 打开网页
{
  "PluginId": "com.pulsar.command",
  "Action": "run",
  "Args": {
    "path": "https://github.com"
  }
}

// 启动程序
{
  "PluginId": "com.pulsar.command",
  "Action": "run",
  "Args": {
    "path": "notepad.exe",
    "arguments": "C:\\temp\\notes.txt"
  }
}

// 打开文件夹
{
  "PluginId": "com.pulsar.command",
  "Action": "run",
  "Args": {
    "path": "explorer.exe",
    "arguments": "C:\\Users\\YourName\\Documents"
  }
}
```

### 2. `sendkeys` - 发送按键

向当前活动窗口发送键盘按键序列。

**参数**:
- `keys` (必需): 按键序列（SendKeys 格式）
- `delay` (可选): 执行前延迟（毫秒），默认 50ms

**示例**:

```json
// 发送 Ctrl+S 保存
{
  "PluginId": "com.pulsar.command",
  "Action": "sendkeys",
  "Args": {
    "keys": "^s"
  }
}

// 输入文本
{
  "PluginId": "com.pulsar.command",
  "Action": "sendkeys",
  "Args": {
    "keys": "Hello World{ENTER}"
  }
}

// 带延迟的按键
{
  "PluginId": "com.pulsar.command",
  "Action": "sendkeys",
  "Args": {
    "keys": "{F5}",
    "delay": "200"
  }
}
```

## SendKeys 语法参考

### 特殊键

| 键 | 代码 | 键 | 代码 |
|----|------|----|----|
| Enter | `{ENTER}` | Tab | `{TAB}` |
| Backspace | `{BACKSPACE}` | Delete | `{DELETE}` |
| Escape | `{ESC}` | Home | `{HOME}` |
| End | `{END}` | Page Up | `{PGUP}` |
| Page Down | `{PGDN}` | Insert | `{INSERT}` |
| 上箭头 | `{UP}` | 下箭头 | `{DOWN}` |
| 左箭头 | `{LEFT}` | 右箭头 | `{RIGHT}` |
| F1-F12 | `{F1}`-`{F12}` | | |

### 修饰键

| 修饰键 | 符号 | 示例 |
|--------|------|------|
| Shift | `+` | `+a` = Shift+A |
| Ctrl | `^` | `^c` = Ctrl+C |
| Alt | `%` | `%{F4}` = Alt+F4 |

### 重复按键

```
{键 数量}
```

示例:
- `{DOWN 3}` - 按 3 次下箭头
- `{TAB 5}` - 按 5 次 Tab

## 使用场景

### 场景 1: 快速打开常用文件夹

```json
{
  "Label": "📁 Documents",
  "PluginId": "com.pulsar.command",
  "Action": "run",
  "Args": {
    "path": "explorer.exe",
    "arguments": "%USERPROFILE%\\Documents"
  }
}
```

### 场景 2: 快速搜索

```json
{
  "Label": "🔍 Google Search",
  "PluginId": "com.pulsar.command",
  "Action": "run",
  "Args": {
    "path": "https://www.google.com/search?q=pulsar+launcher"
  }
}
```

### 场景 3: 自动化工作流

```json
{
  "Label": "💾 Save All",
  "PluginId": "com.pulsar.command",
  "Action": "sendkeys",
  "Args": {
    "keys": "^+s"
  }
}
```

### 场景 4: 启动开发环境

```json
{
  "Label": "🚀 Start Dev Server",
  "PluginId": "com.pulsar.command",
  "Action": "run",
  "Args": {
    "path": "cmd.exe",
    "arguments": "/k npm run dev",
    "workingDir": "C:\\Projects\\MyApp"
  }
}
```

## 注意事项

1. **UseShellExecute**: 所有命令使用 Shell 执行，支持打开文件关联
2. **环境变量**: `path` 和 `workingDir` 支持环境变量（如 `%USERPROFILE%`）
3. **SendKeys 限制**: 
   - 目标窗口必须有焦点
   - 某些应用可能阻止 SendKeys
   - 特殊字符需要转义（`{}[]()^%~+`）
4. **安全性**: 避免执行不受信任的命令

## 故障排除

**问题**: 命令执行失败  
**解决**: 
- 检查 `path` 是否正确
- 确认文件存在或 URL 有效
- 查看日志获取详细错误信息

**问题**: SendKeys 无效  
**解决**:
- 确保目标窗口有焦点
- 增加 `delay` 参数
- 检查按键语法是否正确

**问题**: 工作目录无效  
**解决**: 确保 `workingDir` 路径存在且可访问

## 与其他插件的对比

| 功能 | BasicCommand | WinSwitcher | SystemCommand |
|------|-------------|-------------|---------------|
| 启动程序 | ✅ | ✅ | ❌ |
| 窗口切换 | ❌ | ✅ | ❌ |
| 发送按键 | ✅ | ❌ | ❌ |
| 打开 URL | ✅ | ❌ | ❌ |
| 系统控制 | ❌ | ❌ | ✅ |

**选择建议**:
- 需要窗口切换 → 使用 `WinSwitcher`
- 需要发送按键 → 使用 `BasicCommand`
- 需要打开文件/URL → 使用 `BasicCommand`
- 需要控制 Pulsar → 使用 `SystemCommand`

---

**最后更新**: 2026-03-01
