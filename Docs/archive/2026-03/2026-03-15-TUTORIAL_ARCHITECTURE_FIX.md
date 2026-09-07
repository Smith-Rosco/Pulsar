# Tutorial 架构修复记录

**日期**: 2026-03-15  
**问题**: Tutorial 描述与项目实际架构存在严重冲突  
**状态**: ✅ 已修复（第二次修正）

---

## 问题诊断

### 核心问题 1：快捷键完全错误

**实际快捷键映射**（从代码验证）：

```csharp
// ProfilesConfig.cs:68-69
["ShowGrid"] = new HotkeyConfig { Key = "Q", Modifiers = "Control,Shift" }      // Ctrl+Shift+Q
["ShowSwitcher"] = new HotkeyConfig { Key = "Q", Modifiers = "Control" }        // Ctrl+Q

// RadialMenuViewModel.cs:223-224
hotkeyService.RegisterAction("ShowGrid", () => Show(RadialMenuMode.Action));    // 动作模式
hotkeyService.RegisterAction("ShowSwitcher", () => Show(RadialMenuMode.Task));  // 任务模式
```

**正确的映射关系**：

| 模式 | 代码枚举 | 热键 | 用途 |
|------|---------|------|------|
| 任务模式 | `RadialMenuMode.Task` | **Ctrl+Q** | 快速切换窗口 |
| 动作模式 | `RadialMenuMode.Action` | **Ctrl+Shift+Q** | 执行应用命令 |

**第一次修复的错误**：我错误地认为是 `Ctrl+Shift+Q` 对应任务模式，实际上是反的！

### 核心问题 2：UI 结构理解错误

**Tutorial 错误描述**：
- ❌ "切换到 '切换模式' 标签页"
- ❌ "切换到 '命令模式' 标签页"
- ❌ "左侧进程列表"

**实际 UI 结构**（从 SettingsSlotsPage.xaml 验证）：
- ✅ 顶部有一个 **ComboBox 下拉框** 用于选择 Profile
- ✅ 没有标签页，所有槽位在同一个列表中显示
- ✅ 通过 [Add Slot] 按钮添加槽位，弹出 ContextMenu 选择插件
- ✅ 槽位配置通过 ExpandableCard 展开/收起

### 核心问题 3：配置结构混淆

**实际架构**（单层 Profile 系统）：
```
Profiles.json
├─ Global Profile
│  └─ Slots (所有槽位在一起，没有分类)
└─ Notepad Profile
   └─ Slots (所有槽位在一起，没有分类)
```

**关键发现**：
- Profile 内部的槽位**没有** SwitchMode/CommandMode 的分类
- 所有槽位都在同一个列表中
- 轮盘菜单根据触发模式（Task/Action）决定显示哪些槽位
- 这是通过插件类型和配置来区分的，不是通过文件夹结构

---

## 修复方案（第二次修正）

### 1. 修正快捷键（TutorialOrchestrator.cs）

#### Step 1: 欢迎界面
```diff
- • 任务模式 (Ctrl+Shift+Q)：快速切换窗口
- • 动作模式 (Ctrl+Q)：执行应用专属命令
+ • 任务模式 (Ctrl+Q)：快速切换窗口
+ • 动作模式 (Ctrl+Shift+Q)：执行应用专属命令
```

#### Step 5: 配置窗口切换槽位
```diff
- 1. 确保左侧选中 "Global" 配置
- 2. 切换到 "切换槽位 (SwitchMode)" 标签页
- 3. 点击右上角的 [+ 添加槽位] 按钮
+ 1. 确保顶部下拉框选中 "Global" Profile
+ 2. 点击底部的 [Add Slot] 按钮
+ 3. 在弹出菜单中选择 "Window Switcher" 插件

- 💡 提示：这个槽位会在任务模式 (Ctrl+Shift+Q) 时显示
+ 💡 提示：这个槽位会在任务模式 (Ctrl+Q) 时显示
```

#### Step 6: 测试任务模式
```diff
- 1. 按下 Ctrl+Shift+Q 触发任务模式
+ 1. 按下 Ctrl+Q 触发任务模式

- • 任务模式 (Ctrl+Shift+Q) 用于快速切换窗口
+ • 任务模式 (Ctrl+Q) 用于快速切换窗口
```

#### Step 7: 配置应用专属命令槽位
```diff
- 2. 在左侧进程列表中选择 "Notepad"
- 3. 切换到 "命令槽位 (CommandMode)" 标签页
- 4. 点击 [+ 添加槽位]
+ 2. 在顶部下拉框中选择 "Notepad" Profile
+ 3. 点击底部的 [Add Slot] 按钮
+ 4. 选择任意插件并配置参数

- 💡 提示：命令槽位会在动作模式 (Ctrl+Q) 激活记事本时显示
+ 💡 提示：这个槽位会在动作模式 (Ctrl+Shift+Q) 激活记事本时显示
```

#### Step 8: 测试动作模式
```diff
- 2. 按下 Ctrl+Q 触发动作模式
+ 2. 按下 Ctrl+Shift+Q 触发动作模式

- 💡 提示：动作模式 (Ctrl+Q) 会根据当前激活的应用显示专属命令
+ 💡 提示：动作模式 (Ctrl+Shift+Q) 会根据当前激活的应用显示专属命令
```

#### Step 9: 总结
```diff
- ✅ 任务模式 (Ctrl+Shift+Q)
+ ✅ 任务模式 (Ctrl+Q)
   快速切换窗口，外圈静态配置 + 中心 MRU

- ✅ 动作模式 (Ctrl+Q)
+ ✅ 动作模式 (Ctrl+Shift+Q)
   执行当前应用的专属命令

- ✅ 槽位配置
-    每个应用可配置两类槽位：
-    • 切换槽位 (SwitchMode) - 用于任务模式
-    • 命令槽位 (CommandMode) - 用于动作模式
+ ✅ Profile 配置
+    每个应用可配置独立的槽位
+    Global Profile 在所有应用中可用
```

---

### 2. 暴露 CurrentMode 属性（RadialMenuViewModel.cs）

**问题**: `_currentMode` 是私有字段，触发器无法访问

**修复**:
```csharp
// 添加公共属性
public RadialMenuMode CurrentMode => _currentMode;
```

**位置**: `RadialMenuViewModel.cs:46-49`

---

### 3. 修复模式检测（RadialMenuShownTriggerHandler.cs）

**问题**: 无法检测当前模式，所有菜单显示都会触发

**修复**:
```csharp
// 使用新暴露的 CurrentMode 属性
var currentMode = _radialMenuViewModel.CurrentMode.ToString();
if (currentMode == expectedMode)
{
    _onTriggered?.Invoke();
}
```

**位置**: `RadialMenuShownTriggerHandler.cs:29-55`

---

### 4. 增强槽位匹配逻辑（SlotAddedTriggerHandler.cs）

**已有功能**: 支持部分 JSON 匹配

**验证**: 
- ✅ 支持只指定 `Profile`
- ✅ 支持只指定 `PluginId` + `Profile`
- ✅ 不区分大小写匹配
- ✅ 立即检查现有槽位（防止竞态条件）

**位置**: `SlotAddedTriggerHandler.cs:90-206`

---

### 5. 更新文档注释（TutorialTrigger.cs）

**修正**:
```csharp
// 修正前
/// - RadialMenuShown: 模式类型 ("command" 或 "switch")

// 修正后
/// - RadialMenuShown: 模式类型 ("Task" 或 "Action")
```

**位置**: `TutorialTrigger.cs:15-27`

---

## 修改文件清单

1. ✅ `Pulsar/Pulsar/Services/Tutorial/TutorialOrchestrator.cs`
   - 修正所有步骤的术语和描述
   - 简化触发器匹配条件

2. ✅ `Pulsar/Pulsar/ViewModels/RadialMenuViewModel.cs`
   - 暴露 `CurrentMode` 公共属性

3. ✅ `Pulsar/Pulsar/Services/Tutorial/TriggerHandlers/RadialMenuShownTriggerHandler.cs`
   - 使用 `CurrentMode` 属性检测模式

4. ✅ `Pulsar/Pulsar/Models/Tutorial/TutorialTrigger.cs`
   - 更新注释说明

---

## 验证结果

### 编译测试
```bash
dotnet build Pulsar/Pulsar/Pulsar.csproj --no-incremental
```

**结果**: ✅ 编译成功，0 个警告，0 个错误

### 功能验证清单

- [ ] Step 1: 欢迎界面显示正确术语
- [ ] Step 5: 添加 WinSwitcher 槽位后自动进入下一步
- [ ] Step 6: 按 `Ctrl+Shift+Q` 触发任务模式后进入下一步
- [ ] Step 7: 在 Notepad Profile 添加任何槽位后进入下一步
- [ ] Step 8: 按 `Ctrl+Q` 触发动作模式后进入下一步
- [ ] Step 9: 总结界面显示完整说明

---

## 架构洞察

### 关键发现

1. **双层模式设计**
   - RadialMenu 层：`Task` / `Action` (用户触发)
   - Profile 层：`SwitchMode` / `CommandMode` (配置结构)
   - 这是优雅的分层设计，但需要在 Tutorial 中明确说明

2. **触发器匹配策略**
   - 使用部分匹配而非精确匹配
   - 提高容错性，降低用户操作门槛
   - 避免硬编码插件参数

3. **Profile 动态创建**
   - Profile 在应用运行时动态创建
   - Tutorial 需要引导用户先运行目标应用
   - 或使用 "Smart Profile Creator" 功能

---

## 后续优化建议

### 短期（已完成）
- ✅ 统一术语
- ✅ 修正触发器
- ✅ 暴露必要属性

### 中期（可选）
- [ ] 在 Step 6 和 Step 7 之间添加说明，解释 Profile 创建机制
- [ ] 为 Step 7 提供默认插件选择（如 PKI）
- [ ] 添加视觉高亮，引导用户点击正确的标签页

### 长期（架构改进）
- [ ] 考虑在 UI 中统一使用 "任务模式/动作模式" 术语
- [ ] 或者在代码中重命名为 `SwitchMode`/`CommandMode` 以匹配 UI
- [ ] 创建 Tutorial 设计文档，记录每个步骤的设计意图

---

## 参考文档

- [ARCHITECTURE.md](../../ARCHITECTURE.md) - 系统架构概览
- [AGENTS.md](../../AGENTS.md) - AI Agent 操作指南
- [PLUGIN_DEVELOPMENT.md](../../PLUGIN_DEVELOPMENT.md) - 插件开发指南

---

**修复完成时间**: 2026-03-15  
**修复人员**: AI Architect  
**审核状态**: 待用户验证
