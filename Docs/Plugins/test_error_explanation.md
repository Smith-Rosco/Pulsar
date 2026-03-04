# test_error.js 测试说明

## 问题：执行 test_error.js 后"没有反应"

### 实际情况

test_error.js **确实有反应**，只是反应方式可能不明显：

1. **🔊 音效反馈**: 播放 Windows 错误提示音（系统"Hand"音效）
2. **💬 通知气泡**: 在系统托盘（右下角）显示 Windows 通知
3. **📝 日志记录**: 错误详情写入日志文件

### 为什么可能"看不到"反应

#### 原因 1: Windows 通知被禁用
- Windows 10/11 默认可能禁用某些应用的通知
- 通知可能被"专注助手"屏蔽

#### 原因 2: 通知显示时间太短
- Windows 通知默认显示 5-10 秒后自动消失
- 如果您在看其他地方，可能错过

#### 原因 3: 音量静音
- 错误音效可能因系统静音而听不到

---

## 如何验证 test_error.js 正常工作

### 方法 1: 检查日志文件（最可靠）

1. 打开日志文件：
   ```
   %AppData%\Pulsar\Logs\pulsar-20260304.log
   ```

2. 搜索关键字：`BookmarkletRunner` 或 `validation failed`

3. 应该看到类似内容：
   ```
   [BookmarkletRunner] Script path: G:\0_Playground\Pulsar_Project\Scripts\test_error.js
   [ScriptPreprocessor] Syntax error: Line 4:3 - Expected ')': console
   [BookmarkletRunner] Script validation failed
   ```

### 方法 2: 启用 Windows 通知

1. 打开 Windows 设置：`Win + I`
2. 导航到：`系统 > 通知`
3. 确保以下设置已启用：
   - ✅ 通知
   - ✅ 在锁屏界面上显示通知
4. 找到 Pulsar 应用，确保其通知权限已启用

### 方法 3: 观察浏览器行为

**正确行为**:
- ❌ 浏览器**不会**获得焦点
- ❌ 浏览器地址栏**不会**有任何变化
- ✅ Pulsar 径向菜单关闭

**对比 test.js（正常脚本）**:
- ✅ 浏览器**会**获得焦点
- ✅ 地址栏会短暂显示 `javascript:...`
- ✅ 弹出 alert 对话框

---

## 测试对比表

| 脚本 | 验证结果 | 浏览器焦点 | 错误音效 | 通知气泡 | Alert 弹窗 |
|------|---------|-----------|---------|---------|-----------|
| test.js | ✅ 通过 | ✅ 获得 | ❌ 无 | ❌ 无 | ✅ 显示 |
| test_error.js | ❌ 失败 | ❌ 不获得 | ✅ 播放 | ✅ 显示 | ❌ 不显示 |
| test_complex.js | ✅ 通过 | ✅ 获得 | ❌ 无 | ❌ 无 | ✅ 显示 |

---

## 通知内容示例

当执行 test_error.js 时，应该看到：

```
┌─────────────────────────────────────┐
│ 🔴 操作失败                          │
├─────────────────────────────────────┤
│ 脚本验证失败:                        │
│   • Line 4:3 - Expected ')': console│
└─────────────────────────────────────┘
```

---

## 手动测试步骤

### 步骤 1: 准备环境
```bash
# 1. 启动 Pulsar
cd Pulsar/Pulsar
dotnet run

# 2. 打开浏览器（任意网页）
# 3. 打开日志文件（实时监控）
notepad %AppData%\Pulsar\Logs\pulsar-20260304.log
```

### 步骤 2: 执行测试
1. 触发 Pulsar 快捷键
2. 选择 Bookmarklet Runner 动作（test_error.js）
3. **立即观察**：
   - 🔊 听：是否有错误提示音
   - 👀 看：右下角是否有通知气泡
   - 🖱️ 检查：浏览器是否获得焦点（不应该）

### 步骤 3: 验证日志
刷新日志文件，应该看到新增的错误日志。

---

## 故障排除

### 问题: 完全没有任何反应

**可能原因**:
1. Pulsar 配置中没有正确设置 test_error.js 的路径
2. 插件执行失败（查看日志）

**解决方法**:
```bash
# 检查 Profiles.json 配置
notepad %AppData%\Pulsar\Profiles.json

# 确认 scriptPath 正确：
"Args": {
  "scriptPath": "G:\\0_Playground\\Pulsar_Project\\Scripts\\test_error.js"
}
```

### 问题: 有音效但没有通知

**原因**: Windows 通知被禁用

**解决方法**: 按照"方法 2"启用通知

### 问题: 有通知但内容不对

**可能原因**: 脚本路径错误，执行了其他脚本

**解决方法**: 检查日志中的 `Script path:` 行

---

## 预期的完整日志输出

```log
2026-03-04 15:30:00.123 [Information] [BookmarkletRunner] Initialized successfully
2026-03-04 15:30:05.456 [Debug] [BookmarkletRunner] Script path: G:\0_Playground\Pulsar_Project\Scripts\test_error.js
2026-03-04 15:30:05.478 [Error] [ScriptPreprocessor] Syntax error: Line 4:3 - Expected ')': console
2026-03-04 15:30:05.480 [Error] [BookmarkletRunner] Script validation failed
2026-03-04 15:30:05.482 [Warning] [PluginRegistry] Plugin execution failed (logic error): 脚本验证失败:
  • Line 4:3 - Expected ')': console
```

---

## 总结

**test_error.js 的"反应"是正确的**：

1. ✅ 语法错误被正确检测
2. ✅ 错误信息被记录到日志
3. ✅ 通知气泡显示错误详情
4. ✅ 播放错误音效
5. ✅ 阻止脚本执行到浏览器

这正是我们期望的行为！验证系统工作正常。

如果您仍然"看不到"反应，请：
1. 检查日志文件（最可靠的验证方法）
2. 启用 Windows 通知
3. 确保音量未静音
