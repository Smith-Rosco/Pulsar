# Pulsar Control Plugin

**插件 ID**: `com.pulsar.system`  
**版本**: 1.0.0  
**类型**: Core Plugin  
**作者**: Pulsar Team

## 概述

Pulsar Control 是 Pulsar 的内建系统控制插件，提供对 Pulsar 自身功能的显式动作访问，例如打开设置或快速添加当前应用的配置。

## 功能特性

- **设置界面控制**: 打开和导航到特定设置页面
- **快速配置**: 基于当前上下文快速添加配置
- **窗口管理**: 控制设置窗口的显示状态

## 支持的动作

### `open-settings` - Open Settings

打开 Pulsar 设置窗口并导航到全局设置页面。

**参数**: 无

**示例**:
```json
{
  "PluginId": "com.pulsar.system",
  "Action": "pulsar.system.open_settings"
}
```

### `quick-add-profile` - Quick Add Current App

打开设置窗口并导航到当前进程的 Slots 配置页面，方便快速添加新的径向菜单项。

**参数**: 无（自动从 `PulsarContext` 获取当前进程）

**示例**:
```json
{
  "PluginId": "com.pulsar.system",
  "Action": "pulsar.system.quick_add_profile"
}
```

**行为**:
- 如果当前有活动窗口，打开该进程的配置页面
- 如果无法识别进程，返回错误

## Compatibility

- New slots should use the canonical actions `open-settings` and `quick-add-profile`.
- Existing slots using `pulsar.system.open_settings` or `pulsar.system.quick_add_profile` continue to work.
- Legacy wrapper actions such as `run` or `execute` with a nested `command` argument are still resolved at runtime for compatibility, but they are no longer the primary authoring model.

## 实现细节

### UI 线程调度
所有系统命令都在 WPF UI 线程上执行，确保窗口操作的线程安全。

### 窗口状态管理
- 如果设置窗口已打开，激活并恢复（如果最小化）
- 如果设置窗口未打开，创建新实例

### 消息传递
使用 `WeakReferenceMessenger` 发送 `OpenSettingsMessage` 来导航到特定页面。

## 依赖服务

- `IServiceProvider`: 用于创建 `SettingsWindow` 实例
- `WeakReferenceMessenger`: 用于页面导航消息传递

## 使用场景

### 场景 1: 全局设置快捷方式
在任何进程的径向菜单中添加"打开设置"按钮：

```json
{
  "Label": "⚙️ Settings",
  "PluginId": "com.pulsar.system",
  "Action": "pulsar.system.open_settings"
}
```

### 场景 2: 快速配置当前应用
在 Global 配置中添加"快速配置"按钮：

```json
{
  "Label": "➕ Quick Add",
  "PluginId": "com.pulsar.system",
  "Action": "pulsar.system.quick_add_profile"
}
```

当在 Chrome 中触发时，会自动打开 Chrome 的 Slots 配置页面。

## 注意事项

1. **不可禁用**: 作为核心插件，Pulsar Control 无法被禁用
2. **UI 依赖**: 需要 WPF UI 线程，不能在无 UI 环境中使用
3. **上下文感知**: `quick-add-profile` 依赖 `PulsarContext.TargetProcessName`

## 扩展开发

如需添加新的系统命令：

1. 在 `ExecuteAsync` 的 `switch` 语句中添加新的 case
2. 使用 `pulsar.system.` 前缀命名
3. 确保在 UI 线程上执行窗口操作

---

**最后更新**: 2026-03-01
