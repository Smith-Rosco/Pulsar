# WinSwitcher Plugin

**插件 ID**: `com.pulsar.winswitcher`  
**版本**: 1.0.0  
**类型**: Core Plugin  
**作者**: Pulsar Team

## 概述

WinSwitcher 是 Pulsar 的核心窗口管理插件，提供智能窗口切换和应用程序启动功能。

## 功能特性

- **智能窗口切换**: 快速切换到正在运行的应用程序窗口
- **应用启动**: 如果目标应用未运行，自动启动
- **窗口预览**: 支持显示窗口缩略图（可配置）
- **进程过滤**: 可排除特定进程不参与切换

## 支持的动作

### 1. `activate` - 激活窗口

切换到指定进程的窗口。

**参数**:
- `app` (必需): 进程名称（如 "chrome", "excel"）

**示例**:
```json
{
  "PluginId": "com.pulsar.winswitcher",
  "Action": "activate",
  "Args": {
    "app": "chrome"
  }
}
```

### 2. `launch` - 启动应用

启动指定路径的应用程序。

**参数**:
- `path` (必需): 可执行文件路径
- `arguments` (可选): 启动参数

**示例**:
```json
{
  "PluginId": "com.pulsar.winswitcher",
  "Action": "launch",
  "Args": {
    "path": "C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe",
    "arguments": "--new-window"
  }
}
```

### 3. `switch` - 智能切换

如果进程正在运行则切换，否则启动。

**参数**:
- `app` (必需): 进程名称
- `path` (可选): 可执行文件路径（用于启动）
- `arguments` (可选): 启动参数

**示例**:
```json
{
  "PluginId": "com.pulsar.winswitcher",
  "Action": "switch",
  "Args": {
    "app": "chrome",
    "path": "C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe"
  }
}
```

## 配置选项

### ShowPreviews (Boolean)
- **默认值**: `true`
- **说明**: 是否在切换器中显示窗口预览缩略图

### ExcludeProcesses (String)
- **默认值**: `""`
- **说明**: 逗号分隔的进程名列表，这些进程将被排除在切换器之外
- **示例**: `"explorer,taskmgr,pulsar"`

## 依赖服务

- `IWindowService`: 窗口管理服务

## 注意事项

1. 进程名称不区分大小写
2. 被排除的进程无法通过 `activate` 或 `switch` 动作访问
3. `launch` 动作不受 `ExcludeProcesses` 限制

## 故障排除

**问题**: 无法切换到某个窗口  
**解决**: 检查该进程是否在 `ExcludeProcesses` 列表中

**问题**: 启动失败  
**解决**: 确认 `path` 参数指向有效的可执行文件

---

**最后更新**: 2026-03-01
