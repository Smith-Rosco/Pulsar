# 🧪 Bookmarklet Runner v2.0.0 测试指南

## 快速测试步骤

### 1. 启动 Pulsar
```bash
cd Pulsar/Pulsar
dotnet run
```

### 2. 测试原始问题脚本 (test.js)

**脚本位置**: `Scripts/test.js`

**预期结果**:
- ✅ 脚本成功执行
- ✅ 浏览器显示 alert 弹窗
- ✅ 控制台输出日志
- ✅ 无语法错误

**验证方法**:
1. 打开任意浏览器（Chrome/Edge/Firefox）
2. 打开任意网页
3. 触发 Pulsar 快捷键
4. 选择 Bookmarklet Runner 动作
5. 观察是否弹出 alert

---

### 3. 测试复杂现代脚本 (test_complex.js)

**脚本位置**: `Scripts/test_complex.js`

**预期结果**:
- ✅ 所有链接被高亮（黄色背景 + 红色边框）
- ✅ 显示高亮链接数量的 alert
- ✅ 支持 ES6+ 语法（const、箭头函数、模板字符串）

**验证方法**:
1. 打开有链接的网页（如 https://www.baidu.com）
2. 触发 Pulsar 执行 test_complex.js
3. 观察链接是否被高亮
4. 检查 alert 信息

---

### 4. 测试语法错误检测 (test_error.js)

**脚本位置**: `Scripts/test_error.js`

**预期结果**:
- ❌ 脚本验证失败
- 🔊 播放错误音效（Windows 错误提示音）
- 💬 显示 Windows 通知气泡：
  - 标题：`操作失败`
  - 内容：`脚本验证失败: • Line 4:3 - Expected ')': console`
- ✅ 不会执行到浏览器（浏览器不会获得焦点）

**验证方法**:
1. 确保 Windows 通知未被禁用（检查系统托盘区域）
2. 触发 Pulsar 执行 test_error.js
3. **听**：应该听到 Windows 错误提示音
4. **看**：系统托盘应该弹出通知气泡（右下角）
5. **检查日志**：`%AppData%\Pulsar\Logs\pulsar-yyyyMMdd.log` 应包含：
   ```
   [BookmarkletRunner] Script validation failed
   [ScriptPreprocessor] Syntax error: Line 4:3 - Expected ')': console
   ```

**如果没有看到通知**:
- 检查 Windows 通知设置：`设置 > 系统 > 通知`
- 确保 Pulsar 的通知权限已启用
- 查看日志文件确认错误确实被检测到

---

## 日志检查

### 查看详细日志
```
位置: %AppData%\Pulsar\Logs\pulsar-yyyyMMdd.log
```

### 关键日志信息

**成功执行**:
```
[BookmarkletRunner] NUglify minification successful (147 chars)
[BookmarkletRunner] Script validated successfully (147 chars)
[BookmarkletRunner] Bookmarklet executed successfully
```

**语法错误**:
```
[ScriptPreprocessor] Syntax error: Line 2:37 - Expected ')'
[BookmarkletRunner] Script validation failed
```

**回退模式**:
```
[ScriptPreprocessor] NUglify failed, using fallback
[ScriptPreprocessor] Using regex fallback
```

---

## 对比测试

### 验证修复效果

**测试脚本**: `Scripts/test.js`

**旧版本 (v1.0.0) 预期问题**:
- 可能出现语法错误（尾随逗号）
- 某些浏览器可能无法执行

**新版本 (v2.0.0) 预期结果**:
- ✅ 完美执行
- ✅ 所有浏览器兼容
- ✅ 压缩率提升 ~21%

---

## 性能测试

### 压缩效果对比

**test.js**:
- 原始: 186 字符
- 压缩后: ~147 字符
- 压缩率: ~21%

**test_complex.js**:
- 原始: ~700 字符（含注释）
- 压缩后: ~400 字符
- 压缩率: ~43%

---

## 浏览器兼容性测试

### 推荐测试浏览器

| 浏览器 | 版本 | UIA 支持 | 测试状态 |
|--------|------|---------|---------|
| Chrome | 最新 | ✅ | 待测试 |
| Edge | 最新 | ✅ | 待测试 |
| Firefox | 最新 | ✅ | 待测试 |
| Brave | 最新 | ✅ | 待测试 |

### 测试步骤
1. 在每个浏览器中打开测试页面
2. 执行 test.js
3. 执行 test_complex.js
4. 记录结果

---

## 故障排除

### 问题: "未检测到运行中的浏览器"
**解决**: 确保至少有一个浏览器窗口打开

### 问题: 脚本未执行
**检查**:
1. 查看 Pulsar 日志
2. 检查浏览器控制台是否有错误
3. 确认网站没有严格的 CSP 策略

### 问题: LSP 显示 NUglify 错误
**说明**: 这是 LSP 的误报，实际构建成功
**验证**: 运行 `dotnet build` 确认 0 个错误

---

## 回归测试清单

- [ ] test.js 在 Chrome 中执行成功
- [ ] test.js 在 Edge 中执行成功
- [ ] test.js 在 Firefox 中执行成功
- [ ] test_complex.js 正确高亮链接
- [ ] test_error.js 正确显示错误信息
- [ ] 日志中包含 "NUglify minification successful"
- [ ] 压缩后的脚本长度符合预期
- [ ] 无剪贴板污染（剪贴板内容未改变）
- [ ] Pulsar 窗口正确隐藏
- [ ] 浏览器窗口正确聚焦

---

## 性能基准

### 构建时间
- Debug: ~2.5 秒
- Release: ~3.0 秒

### 运行时性能
- 脚本验证: < 50ms
- 脚本压缩: < 100ms
- 总执行时间: < 500ms（含窗口切换）

---

## 报告问题

如果发现问题，请提供以下信息：

1. **Pulsar 版本**: 2.0.0
2. **操作系统**: Windows 版本
3. **浏览器**: 名称和版本
4. **测试脚本**: test.js / test_complex.js / test_error.js
5. **错误日志**: 从 `%AppData%\Pulsar\Logs\` 复制相关日志
6. **预期行为**: 应该发生什么
7. **实际行为**: 实际发生了什么

---

**测试完成后，请在 ADR-002 文档中更新验收状态！**
