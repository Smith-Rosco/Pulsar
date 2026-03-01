# PKI Plugin

**插件 ID**: `com.pulsar.pki`  
**版本**: 1.0.0  
**类型**: Core Plugin  
**作者**: Pulsar Team

## 概述

PKI (Public Key Infrastructure) 插件是 Pulsar 的核心安全组件，负责安全地管理和注入用户凭据（用户名/密码）到目标应用程序。

## 功能特性

- **加密存储**: 使用 Windows DPAPI 加密存储凭据
- **智能注入**: 优先使用 UI Automation，回退到 SendKeys
- **无剪贴板污染**: UIA 模式不会覆盖用户剪贴板
- **自动 Tab 切换**: 自动在用户名和密码字段间切换
- **可选自动回车**: 支持填充后自动提交

## 支持的动作

### `fill` / `inject` - 填充凭据

将存储的凭据注入到当前焦点窗口。

**参数**:
- `secretId` (必需): 凭据的 GUID 标识符
- `autoEnter` (可选): 是否在填充后自动按回车，默认 `false`

**示例**:
```json
{
  "PluginId": "com.pulsar.pki",
  "Action": "fill",
  "Args": {
    "secretId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
    "autoEnter": "true"
  }
}
```

## 注入流程

1. **加载凭据**: 从加密存储中读取并解密凭据
2. **隐藏 Pulsar**: 隐藏径向菜单窗口
3. **恢复焦点**: 将焦点返回到目标窗口
4. **注入用户名**: 
   - 尝试使用 UI Automation 直接设置文本
   - 失败则使用 SendKeys 模拟输入
5. **切换字段**: 发送 Tab 键
6. **注入密码**: 同上
7. **可选提交**: 如果 `autoEnter=true`，发送回车键

## 安全特性

### 加密机制
- 使用 Windows DPAPI (Data Protection API)
- 密钥绑定到当前用户账户
- 无法在其他用户或机器上解密

### 注入安全
- 优先使用 UI Automation（不经过剪贴板）
- SendKeys 模式会转义特殊字符
- 密码不会记录到日志

## 依赖服务

- `CredentialsManager`: 凭据加密/解密服务
- `SecretRepository`: 凭据存储服务
- `IWindowService`: 窗口管理服务

## 配置文件

凭据存储在 `%AppData%\Pulsar\secrets.json`（加密格式）。

**不要手动编辑此文件！** 请使用 Pulsar 设置界面管理凭据。

## 注意事项

1. **焦点要求**: 目标输入框必须已获得焦点
2. **字段顺序**: 假设用户名字段在密码字段之前
3. **特殊字符**: SendKeys 模式会自动转义 `{}[]()^%~+` 等字符
4. **浏览器兼容性**: 现代浏览器（Chrome/Edge/Firefox）完全支持 UIA 模式

## 故障排除

**问题**: 注入失败，提示 "Secret not found"  
**解决**: 检查 `secretId` 是否正确，或在设置中重新创建凭据

**问题**: 密码字段未填充  
**解决**: 确保目标窗口的输入框已获得焦点，尝试手动点击输入框后再触发

**问题**: 特殊字符显示错误  
**解决**: 这是 SendKeys 回退模式的已知限制，尝试在支持 UIA 的应用中使用

---

**最后更新**: 2026-03-01
