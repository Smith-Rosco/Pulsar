# 🔍 test_error.js 执行检查清单

> ⚠️ **ARCHIVED DOCUMENT**  
> **Archived Date**: 2026-03-08  
> **Original Date**: 2026-03-04  
> **Status**: Historical reference only, no longer maintained  
> **Reason**: Testing completed. For current plugin documentation, see [Docs/Plugins/BookmarkletRunner.md](../../Plugins/BookmarkletRunner.md)

---

## 请按以下步骤操作：

### 步骤 1: 清空日志（可选）
```bash
del %APPDATA%\Pulsar\Logs\pulsar-20260304.log
```

### 步骤 2: 启动 Pulsar
```bash
cd G:\0_Playground\Pulsar_Project\Pulsar\Pulsar
dotnet run
```

### 步骤 3: 打开浏览器
- 打开任意浏览器（Chrome/Edge/Firefox）
- 打开任意网页

### 步骤 4: 执行 test_error.js
1. 按 Pulsar 快捷键（显示径向菜单）
2. **点击 slot 3**（应该标记为 "New Script"）
3. 注意观察：
   - 🔊 是否听到错误提示音？
   - 💬 右下角是否弹出通知气泡？
   - 🖱️ 浏览器是否获得焦点？（不应该）

### 步骤 5: 检查日志
```bash
notepad %APPDATA%\Pulsar\Logs\pulsar-20260304.log
```

搜索关键字：
- "BookmarkletRunner"
- "validation failed"
- "test_error"

### 预期结果：

**如果正常工作，日志应该包含：**
```
[BookmarkletRunner] Script path: G:\0_Playground\Pulsar_Project\Scripts\test_error.js
[ScriptPreprocessor] Syntax error: Line 6:3 - Expected ')': console
[BookmarkletRunner] Script validation failed
```

**如果日志中没有这些内容，说明：**
- 您可能点击了错误的 slot
- 或者配置文件中的路径不正确
- 或者插件没有被正确加载

---

## 故障排除

### 问题 1: 不确定哪个是 slot 3
**解决**: 查看 Profiles.json
```bash
notepad %APPDATA%\Pulsar\Profiles.json
```
搜索 "test_error.js"，查看它的 "slot" 值

### 问题 2: 点击后完全没反应
**可能原因**: 
- 路径错误
- 插件未加载
- 配置文件格式错误

**检查方法**: 
```bash
# 查看插件是否加载
findstr /i "BookmarkletRunner.*Initialized" %APPDATA%\Pulsar\Logs\pulsar-20260304.log
```

### 问题 3: 有反应但不是错误
**可能原因**: 点击了错误的 slot

**解决**: 确认 slot 3 的配置

---

## 快速测试命令

运行此命令查看最近的 Pulsar 活动：
```bash
powershell -Command "Get-Content $env:APPDATA\Pulsar\Logs\pulsar-20260304.log -Tail 50 | Select-String -Pattern 'Bookmarklet|validation|error' -Context 2"
```
