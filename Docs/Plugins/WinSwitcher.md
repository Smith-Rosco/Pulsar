# App Switcher Plugin

**插件 ID**: `com.pulsar.winswitcher`  
**版本**: 1.0.0  
**类型**: Core Plugin  
**作者**: Pulsar Team

## 概述

App Switcher 是 Pulsar 的核心应用控制插件，用来切换到已运行的应用、直接启动应用，或先尝试切换再在缺失时启动。

## 功能特性

- **智能窗口切换**: 快速切换到正在运行的应用程序窗口
- **应用启动**: 如果目标应用未运行，自动启动
- **窗口预览**: 支持显示窗口缩略图（可配置）
- **进程过滤**: 可排除特定进程不参与切换

## 支持的动作

### 1. `activate` - Switch Existing App

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

### 2. `launch` - Launch App

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

### 3. `switch` - Switch Or Launch

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

## Recommended Usage

- Use `activate` when the app must already be running and no new instance should be launched.
- Use `switch` when the preferred workflow is "go to this app, or launch it if it is missing".
- Use `launch` when you always want a fresh process launch from an explicit path.
- Use `com.pulsar.command` + `run` instead when the target is a file, folder, URL, or generic shell-open target rather than app-window control.

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
