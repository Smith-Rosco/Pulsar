# Tutorial 架构修复 - 最终版本

**日期**: 2026-03-15  
**状态**: ✅ 已完成（经过两次修正）  
**问题**: Tutorial 描述与项目实际架构完全不符

---

## 🔴 我犯的错误

作为 AI 架构师，我在第一次修复时犯了严重错误：

1. **没有先查看实际代码就凭想象写描述**
2. **快捷键完全写反了**
3. **UI 结构理解错误**
4. **配置结构理解错误**

这是一个深刻的教训：**必须先研究实际代码，再写文档**。

---

## ✅ 实际架构（经过代码验证）

### 1. 快捷键映射

```csharp
// ProfilesConfig.cs:68-69
["ShowGrid"] = new HotkeyConfig { Key = "Q", Modifiers = "Control,Shift" }      
["ShowSwitcher"] = new HotkeyConfig { Key = "Q", Modifiers = "Control" }        

// RadialMenuViewModel.cs:223-224
hotkeyService.RegisterAction("ShowGrid", () => Show(RadialMenuMode.Action));    
hotkeyService.RegisterAction("ShowSwitcher", () => Show(RadialMenuMode.Task));  
```

**正确的映射**：

| 模式 | 代码枚举 | 热键 | 用途 |
|------|---------|------|------|
| 任务模式 | `RadialMenuMode.Task` | **Ctrl+Q** | 快速切换窗口 |
| 动作模式 | `RadialMenuMode.Action` | **Ctrl+Shift+Q** | 执行应用命令 |

### 2. 配置结构

```csharp
// ProfilesConfig.cs:140-181
public class ProcessProfile
{
    public List<PluginSlot> CommandMode { get; set; } = new();  // 动作模式槽位
    public List<PluginSlot> SwitchMode { get; set; } = new();   // 任务模式槽位
    
    public List<PluginSlot> GetSlots(bool isCommandMode)
    {
        return isCommandMode ? CommandMode : SwitchMode;
    }
}
```

**实际结构**：
```
Profiles.json
├─ Global Profile
│  ├─ CommandMode: []  // 动作模式槽位（Ctrl+Shift+Q）
│  └─ SwitchMode: []   // 任务模式槽位（Ctrl+Q）
└─ Notepad Profile
   ├─ CommandMode: []  // 记事本专属动作
   └─ SwitchMode: []   // 记事本专属切换（通常为空）
```

### 3. UI 结构（SettingsSlotsPage.xaml + SettingsViewModel.cs）

**顶部下拉框显示的 Context**：

```csharp
// SettingsViewModel.cs:419-430
if (ctx.Key == "Launcher")
{
    // 显示 Global.SwitchMode（任务模式槽位）
    slots = _config.Profiles["Global"].SwitchMode;
}
else if (ctx.Key == "Global")
{
    // 显示 Global.CommandMode（全局动作槽位）
    slots = _config.Profiles["Global"].CommandMode;
}
else
{
    // 显示特定应用的 CommandMode（应用专属动作）
    slots = _config.Profiles[ctx.Key].CommandMode;
}
```

**UI 特点**：
- ✅ 顶部有 **ComboBox 下拉框** 选择 Context
- ✅ 没有标签页，所有槽位在同一个列表中
- ✅ 通过 [Add Slot] 按钮添加，弹出 ContextMenu 选择插件
- ✅ 使用 ExpandableCard 展开/收起配置

---

## 📝 最终修正的 Tutorial 内容

### Step 1: 欢迎
```
• 任务模式 (Ctrl+Q)：快速切换窗口
• 动作模式 (Ctrl+Shift+Q)：执行应用专属命令
```

### Step 5: 配置窗口切换槽位
```
1. 在顶部下拉框中选择 "Launcher" Context
   （这会显示 Global Profile 的 SwitchMode 槽位）
2. 点击底部的 [Add Slot] 按钮
3. 在弹出菜单中选择 "Window Switcher" 插件
4. 在 app 参数中输入：notepad
5. 在 Display Label 中输入：记事本

💡 提示：这个槽位会在任务模式 (Ctrl+Q) 时显示
```

### Step 6: 测试任务模式
```
1. 按下 Ctrl+Q 触发任务模式
2. 移动鼠标到 "记事本" 槽位
3. 释放按键，Pulsar 会打开/切换到记事本

💡 提示：任务模式 (Ctrl+Q) 用于快速切换窗口
```

### Step 7: 配置应用专属命令槽位
```
1. 回到设置窗口
2. 在顶部下拉框中选择 "Notepad" Profile
3. 点击底部的 [Add Slot] 按钮
4. 选择任意插件并配置参数

💡 提示：这个槽位会在动作模式 (Ctrl+Shift+Q) 激活记事本时显示
```

### Step 8: 测试动作模式
```
1. 确保记事本窗口处于激活状态
2. 按下 Ctrl+Shift+Q 触发动作模式
3. 移动鼠标到配置的槽位
4. 释放按键，执行对应操作

💡 提示：动作模式 (Ctrl+Shift+Q) 会根据当前激活的应用显示专属命令
```

### Step 9: 总结
```
✅ 任务模式 (Ctrl+Q)
   快速切换窗口，外圈静态配置 + 中心 MRU

✅ 动作模式 (Ctrl+Shift+Q)
   执行当前应用的专属命令

✅ Profile 配置
   每个应用可配置独立的槽位
   Global Profile 在所有应用中可用
```

---

## 🔧 代码修改清单

### 1. TutorialOrchestrator.cs
- ✅ Step 1: 修正快捷键说明
- ✅ Step 3: 修正配置说明
- ✅ Step 4: 修正槽位说明
- ✅ Step 5: 修正 UI 操作步骤和快捷键
- ✅ Step 6: 修正快捷键 `Ctrl+Shift+Q` → `Ctrl+Q`
- ✅ Step 7: 修正 UI 操作步骤和快捷键
- ✅ Step 8: 修正快捷键 `Ctrl+Q` → `Ctrl+Shift+Q`
- ✅ Step 9: 修正总结内容

### 2. RadialMenuViewModel.cs
- ✅ 暴露 `CurrentMode` 公共属性

### 3. RadialMenuShownTriggerHandler.cs
- ✅ 使用 `CurrentMode` 属性检测模式

### 4. TutorialTrigger.cs
- ✅ 更新注释说明

---

## 📊 架构洞察

### 关键发现

1. **Context 虚拟化**
   - UI 通过 Context 概念虚拟化了 Profile 和 Mode 的组合
   - `"Launcher"` Context = `Global.SwitchMode`
   - `"Global"` Context = `Global.CommandMode`
   - 其他 Context = `Profile.CommandMode`

2. **双层配置结构**
   ```
   RadialMenu 层（运行时）
   ├─ Task Mode (Ctrl+Q)
   └─ Action Mode (Ctrl+Shift+Q)
   
   Profile 层（配置文件）
   ├─ SwitchMode Slots → 供 Task Mode 使用
   └─ CommandMode Slots → 供 Action Mode 使用
   ```

3. **UI 设计哲学**
   - 通过 Context 抽象简化用户理解
   - 避免暴露底层的 CommandMode/SwitchMode 概念
   - 用户只需知道：选择 Context → 添加 Slot

---

## ✅ 验证结果

### 编译测试
```bash
dotnet build Pulsar/Pulsar/Pulsar.csproj --no-incremental
```
**结果**: ✅ 编译成功，0 个警告，0 个错误

### 功能验证清单

- [ ] Step 1: 欢迎界面显示正确的快捷键
- [ ] Step 5: 在 "Launcher" Context 添加 WinSwitcher 槽位
- [ ] Step 6: 按 `Ctrl+Q` 触发任务模式
- [ ] Step 7: 在 "Notepad" Context 添加任意槽位
- [ ] Step 8: 按 `Ctrl+Shift+Q` 触发动作模式
- [ ] Step 9: 总结界面显示正确信息

---

## 💡 经验教训

### 对 AI 开发者的建议

1. **永远先看代码，再写文档**
   - 不要凭想象或常识推测
   - 使用 `read`、`grep`、`glob` 工具深入研究
   - 验证每一个假设

2. **关注实际的用户交互流程**
   - 阅读 XAML 了解 UI 结构
   - 阅读 ViewModel 了解数据绑定
   - 阅读配置模型了解数据结构

3. **验证关键映射关系**
   - 快捷键 → 模式
   - UI 操作 → 数据变化
   - 配置结构 → 运行时行为

4. **承认错误，快速迭代**
   - 第一次错了不可怕
   - 重要的是发现问题后立即修正
   - 记录错误，避免重复

---

## 📚 相关文档

- [ARCHITECTURE.md](../../ARCHITECTURE.md) - 系统架构概览
- [AGENTS.md](../../AGENTS.md) - AI Agent 操作指南
- [SettingsSlotsPage.xaml](../../Pulsar/Pulsar/Views/Pages/SettingsSlotsPage.xaml) - UI 结构
- [ProfilesConfig.cs](../../Pulsar/Pulsar/Models/ProfilesConfig.cs) - 配置模型

---

**修复完成时间**: 2026-03-15  
**总修改次数**: 2 次（第一次错误，第二次正确）  
**审核状态**: 待用户验证
