# BookmarkletRunner Plugin

**插件 ID**: `com.pulsar.bookmarklet`  
**版本**: 1.0.0  
**类型**: Extension Plugin  
**作者**: Pulsar Team

## 概述

BookmarkletRunner 允许在浏览器中执行 JavaScript Bookmarklet 脚本，提供快速的网页自动化和增强功能。

## 功能特性

- **智能浏览器检测**: 自动识别当前活动的浏览器窗口
- **UI Automation 注入**: 使用 UIA 实现瞬时注入，无剪贴板污染
- **回退机制**: UIA 失败时自动回退到模拟输入
- **脚本预处理**: 自动添加 `javascript:` 前缀和安全检查
- **多浏览器支持**: Chrome, Edge, Firefox, Brave 等

## 支持的动作

### `run` - 运行 Bookmarklet

在当前浏览器中执行指定的 JavaScript Bookmarklet。

**参数**:
- `scriptPath` (必需): Bookmarklet 脚本文件路径（.js）

**示例**:
```json
{
  "PluginId": "com.pulsar.bookmarklet",
  "Action": "run",
  "Args": {
    "scriptPath": "G:\\Scripts\\Bookmarklets\\HighlightLinks.js"
  }
}
```

## 脚本格式

### 标准格式

```javascript
// HighlightLinks.js
(function() {
    var links = document.querySelectorAll('a');
    links.forEach(function(link) {
        link.style.backgroundColor = 'yellow';
    });
})();
```

### 带 javascript: 前缀（可选）

```javascript
javascript:(function() {
    alert('Hello from Bookmarklet!');
})();
```

**注意**: 插件会自动添加 `javascript:` 前缀，无需手动添加。

## 执行流程

1. **验证脚本路径**: 检查路径安全性（防止路径遍历攻击）
2. **读取脚本**: 加载并预处理脚本内容
3. **检测浏览器**: 智能选择目标浏览器窗口
4. **隐藏 Pulsar**: 隐藏径向菜单窗口
5. **聚焦浏览器**: 激活浏览器窗口（保持最大化状态）
6. **注入脚本**:
   - 发送 `Ctrl+L` 聚焦地址栏
   - 尝试 UI Automation 直接设置文本
   - 失败则使用模拟输入
   - 发送回车执行

## 浏览器支持

| 浏览器 | UIA 支持 | 模拟输入 | 备注 |
|--------|---------|---------|------|
| Chrome | ✅ | ✅ | 完全支持 |
| Edge | ✅ | ✅ | 完全支持 |
| Firefox | ✅ | ✅ | 完全支持 |
| Brave | ✅ | ✅ | 完全支持 |
| Opera | ⚠️ | ✅ | UIA 可能不稳定 |

## 依赖服务

- `IWindowService`: 窗口管理服务

## 依赖插件

- `com.pulsar.winswitcher`: 提供 `IWindowService`

## 安全特性

### 路径验证
- 阻止路径遍历攻击（`..`, `~`）
- 阻止访问系统目录（`C:\Windows`, `C:\Program Files`）
- 仅允许访问用户目录和明确指定的路径

### 脚本预处理
- 移除 BOM 标记
- 压缩空白字符
- 验证脚本非空

## 常见 Bookmarklet 示例

### 1. 高亮所有链接

```javascript
(function() {
    document.querySelectorAll('a').forEach(a => {
        a.style.outline = '2px solid red';
    });
})();
```

### 2. 显示图片 Alt 文本

```javascript
(function() {
    document.querySelectorAll('img').forEach(img => {
        img.title = img.alt || 'No alt text';
        img.style.border = '2px solid blue';
    });
})();
```

### 3. 编辑页面内容

```javascript
javascript:document.body.contentEditable='true';
```

### 4. 查看密码字段

```javascript
(function() {
    document.querySelectorAll('input[type="password"]').forEach(input => {
        input.type = 'text';
    });
})();
```

## 注意事项

1. **浏览器必须运行**: 执行前必须有打开的浏览器窗口
2. **CSP 限制**: 某些网站的内容安全策略可能阻止 Bookmarklet 执行
3. **HTTPS 限制**: 在 HTTPS 页面上，某些操作可能受限
4. **焦点要求**: 地址栏必须能够获得焦点

## 故障排除

**问题**: "未检测到运行中的浏览器"  
**解决**: 打开任意浏览器窗口后重试

**问题**: 脚本未执行  
**解决**: 
- 检查浏览器控制台是否有 JavaScript 错误
- 确认网站没有严格的 CSP 策略
- 尝试在简单页面（如 about:blank）测试

**问题**: 脚本输入缓慢  
**解决**: 这是模拟输入回退模式的正常现象，UIA 模式会更快

**问题**: "脚本路径包含不安全字符"  
**解决**: 使用绝对路径，避免使用 `..` 或 `~`

## 性能对比

| 注入方式 | 速度 | 剪贴板影响 | 可靠性 |
|---------|------|-----------|--------|
| UI Automation | ⚡ 瞬时 | ✅ 无影响 | ⭐⭐⭐⭐⭐ |
| 模拟输入 | 🐌 较慢 | ✅ 无影响 | ⭐⭐⭐⭐ |

## 开发建议

1. **使用 IIFE**: 将代码包裹在立即执行函数中，避免污染全局作用域
2. **错误处理**: 添加 try-catch 块捕获异常
3. **用户反馈**: 使用 `alert()` 或 `console.log()` 提供执行反馈
4. **测试**: 先在浏览器控制台测试代码，再保存为 Bookmarklet

---

**最后更新**: 2026-03-01
