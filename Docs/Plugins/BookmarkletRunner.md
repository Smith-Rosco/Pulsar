# BookmarkletRunner Plugin

**插件 ID**: `com.pulsar.bookmarklet`  
**版本**: 2.0.0  
**类型**: Extension Plugin  
**作者**: Pulsar Team

## 概述

BookmarkletRunner 允许在浏览器中执行 JavaScript Bookmarklet 脚本，提供快速的网页自动化和增强功能。

## 功能特性

- **智能浏览器检测**: 自动识别当前活动的浏览器窗口
- **UI Automation 注入**: 使用 UIA 实现瞬时注入，无剪贴板污染
- **回退机制**: UIA 失败时自动回退到模拟输入
- **NUglify 引擎**: 使用 Microsoft NUglify 进行 JavaScript 验证和压缩
- **语法验证**: 执行前自动检测语法错误，提供详细错误定位
- **智能压缩**: 保留语义，移除冗余空白和注释
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

插件现在支持**现代多行格式**和**传统单行格式**，所有格式都会被自动优化为最小化的单行脚本。

### ✅ 现代多行格式（推荐）

```javascript
// HighlightLinks.js
(function () {
  const links = document.querySelectorAll('a');
  links.forEach((link) => {
    link.style.backgroundColor = 'yellow';
  });
})();
```

**优势**: 
- 可读性强，易于维护
- 支持注释和良好的视觉层级
- 自动压缩为最优单行脚本

### ✅ 带注释的开发版本

```javascript
(function () {
  // Find all links on the page
  const links = document.querySelectorAll('a');
  
  /* 
   * Highlight each link with yellow background
   * for better visibility
   */
  links.forEach((link) => {
    link.style.backgroundColor = 'yellow';
  });
})();
```

**注意**: 注释会被自动移除，不影响执行。

### ✅ 传统单行格式

```javascript
(function(){var links=document.querySelectorAll('a');links.forEach(function(link){link.style.backgroundColor='yellow';});})();
```

### ✅ 带 javascript: 前缀（可选）

```javascript
javascript:(function() {
    alert('Hello from Bookmarklet!');
})();
```

**注意**: 插件会自动添加 `javascript:` 前缀，无需手动添加。

## 执行流程

1. **验证脚本路径**: 检查路径安全性（防止路径遍历攻击）
2. **读取脚本**: 加载脚本文件内容
3. **语法验证与压缩**: 
   - 使用 NUglify 验证 JavaScript 语法
   - 检测语法错误并报告详细位置
   - 移除注释和冗余空白
   - 压缩为最优单行脚本
4. **检测浏览器**: 智能选择目标浏览器窗口
5. **隐藏 Pulsar**: 隐藏径向菜单窗口
6. **聚焦浏览器**: 激活浏览器窗口（保持最大化状态）
7. **注入脚本**:
   - 发送 `Ctrl+L` 聚焦地址栏
   - 尝试 UI Automation 直接设置文本
   - 失败则使用模拟输入
   - 发送回车执行

## 脚本处理引擎

### NUglify Minifier (Primary)

插件使用 **Microsoft NUglify** (v1.21.17) 进行 JavaScript 处理，提供:

- ✅ **语法验证**: 执行前检测语法错误
- ✅ **智能压缩**: 保留语义，移除冗余空白
- ✅ **注释移除**: 自动清理注释，减小体积
- ✅ **错误定位**: 精确报告错误行号和列号
- ✅ **变量保留**: 不重命名变量，保持可读性

### Regex Fallback (Secondary)

如果 NUglify 处理失败，自动回退到改进的正则表达式处理:

- 移除单行和多行注释
- 压缩空白字符
- 清理操作符周围的空格
- 保持基本语法结构

### 处理示例

**输入 (test.js):**
```javascript
javascript: (function () {
  alert(
    "Hello from Pulsar Bookmarklet Runner!\n当前的标题是: " + document.title,
  );
  console.log("Script executed via Pulsar Plugin");
})();
```

**输出 (NUglify 压缩后):**
```javascript
javascript:(function(){alert("Hello from Pulsar Bookmarklet Runner!\n当前的标题是: "+document.title);console.log("Script executed via Pulsar Plugin")})();
```

**优化效果:**
- 原始: 186 字符
- 压缩后: 147 字符
- 减少: 21% 体积

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
- 仅允许 `.js` 和 `.txt` 文件扩展名
- 使用绝对路径验证

### 脚本验证
- NUglify 语法检查，防止恶意代码注入
- 移除 BOM 标记
- 验证脚本非空

### 执行安全
- 不使用 `eval()` 或动态代码执行
- 通过浏览器地址栏执行（浏览器沙箱保护）
- 无剪贴板污染

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

**问题**: "脚本验证失败: Line 2:33 - Expected ')'"  
**解决**: 
- 检查 JavaScript 语法错误
- 使用浏览器控制台或在线工具验证代码
- 查看错误信息中的行号和列号定位问题

**示例错误信息:**
```
脚本验证失败:
  • Line 3:45 - Expected ';' but found '}'
  • Line 5:12 - Unterminated string constant
```

**问题**: 脚本未执行  
**解决**: 
- 检查浏览器控制台是否有 JavaScript 错误
- 确认网站没有严格的 CSP 策略
- 尝试在简单页面（如 about:blank）测试

**问题**: 脚本输入缓慢  
**解决**: 这是模拟输入回退模式的正常现象，UIA 模式会更快

**问题**: "脚本路径包含不安全字符"  
**解决**: 使用绝对路径，避免使用 `..` 或 `~`

**问题**: "Used fallback regex processing" 警告  
**解决**: 这是非致命警告，表示 NUglify 处理失败，使用了正则表达式回退。脚本仍可正常执行，但建议检查语法。

## 性能对比

| 注入方式 | 速度 | 剪贴板影响 | 可靠性 |
|---------|------|-----------|--------|
| UI Automation | ⚡ 瞬时 | ✅ 无影响 | ⭐⭐⭐⭐⭐ |
| 模拟输入 | 🐌 较慢 | ✅ 无影响 | ⭐⭐⭐⭐ |

| 处理引擎 | 语法验证 | 压缩率 | 错误定位 | 可靠性 |
|---------|---------|--------|---------|--------|
| NUglify | ✅ 完整 | ~20-30% | ✅ 行列号 | ⭐⭐⭐⭐⭐ |
| Regex Fallback | ❌ 无 | ~10-15% | ❌ 无 | ⭐⭐⭐ |

## 浏览器 URL 长度限制

不同浏览器对 URL 长度有限制:

| 浏览器 | 最大长度 | 推荐长度 |
|--------|---------|---------|
| Chrome | ~2MB | < 2000 字符 |
| Firefox | ~65,536 字符 | < 2000 字符 |
| Edge | ~2MB | < 2000 字符 |
| Safari | ~80,000 字符 | < 2000 字符 |

**建议**: 保持 Bookmarklet 在 2000 字符以内以确保跨浏览器兼容性。NUglify 压缩可显著减小脚本体积。

## 开发建议

1. **使用 IIFE**: 将代码包裹在立即执行函数中，避免污染全局作用域
2. **错误处理**: 添加 try-catch 块捕获异常
3. **用户反馈**: 使用 `alert()` 或 `console.log()` 提供执行反馈
4. **测试**: 先在浏览器控制台测试代码，再保存为 Bookmarklet

---

**最后更新**: 2026-03-04  
**版本**: 2.0.0 - 添加 NUglify 引擎和语法验证
