# 子轮盘窗口 Slot 灰色不可用问题

**日期：** 2026-03-18  
**严重程度：** 高  
**影响范围：** 子轮盘（Sub-Radial Menu）、多窗口进程切换  
**状态：** 已解决

---

## 问题描述

进入子轮盘（右键点击多窗口进程）后，所有窗口 slot 显示为灰色且无法触发，导致用户无法手动切换到想要的窗口。

### 症状

1. 右键点击有多个窗口的进程 slot（如 Chrome、VS Code）
2. 子轮盘展开，显示该进程的所有窗口
3. **所有窗口 slot 显示为灰色/半透明**（不可用状态）
4. 鼠标悬停无响应，点击无法切换窗口
5. 只有中心的 "Back" 按钮可用

### 用户期望行为

- 子轮盘中的窗口 slot 应该完全可见（不灰色）
- 鼠标悬停应该有高亮和磁吸效果
- 点击应该能够切换到对应的窗口

---

## 根本原因

### 技术细节

**问题代码位置：** `RadialMenuViewModel.cs` 的 `EnterSubMenuAsync` 方法（约 1065-1087 行）

```csharp
// ❌ 错误实现
for (int i = 0; i < _slotsPerPage; i++)
{
    var slot = Slots.FirstOrDefault(s => s.SlotIndex == i + 1);
    if (slot == null) continue;

    if (i < sortedWindows.Count)
    {
        var win = sortedWindows[i];
        slot.Label = win.Title;
        slot.IconImage = win.AppIcon;
        slot.Type = SlotType.Window;
        slot.DataContext = win;
        slot.ActionStrategy = new WindowSwitchStrategy(win, ...);
        // ❌ 缺失：没有调用 ResetAnimation()
    }
}
```

### 根本原因分析

**1. 动画状态污染**

Slot 的可见性由 `CurrentOpacity` 属性控制（XAML 第 57 行）：

```xaml
<controls:JellyOrb Opacity="{Binding CurrentOpacity}">
```

- `CurrentOpacity = 1.0`：完全可见
- `CurrentOpacity = 0.0`：完全透明（灰色/不可见）

**2. 状态未重置**

在 Root Menu 中，某些 slot 可能是空的（`Type = SlotType.None`），它们的 `CurrentOpacity` 可能被设置为 0。

当进入子轮盘时：
1. `ClearVisuals()` 被调用（第 1006 行）
   - 清空 `Label`, `IconKey` 等视觉内容
   - **但不重置 `CurrentOpacity` 和 `CurrentScale`**
2. 重新填充窗口数据（第 1070-1078 行）
   - 设置新的 `Label`, `IconImage`, `DataContext`
   - **但没有调用 `ResetAnimation()`**
3. 结果：Slot 保留了之前的 `CurrentOpacity = 0`
   - 即使有新的窗口数据，slot 仍然不可见

**3. 职责分离不清晰**

查看 `ClearVisuals()` 方法（第 413-430 行）：

```csharp
public void ClearVisuals()
{
    foreach (var slot in Slots)
    {
        slot.Label = "";
        slot.LoadIconData(string.Empty);
        slot.IsActive = false;
        // ❌ 没有重置 CurrentOpacity 和 CurrentScale
    }
}
```

而 `ResetAnimation()` 方法（`SlotViewModel.cs` 第 202-214 行）：

```csharp
public void ResetAnimation()
{
    CurrentScale = 1.0;   // 完全可见
    CurrentOpacity = 1.0; // 完全可见
    _magneticOffsetX = 0;
    _magneticOffsetY = 0;
    // ... 重置其他动画属性
}
```

`ClearVisuals()` 负责清空**内容**，`ResetAnimation()` 负责重置**动画状态**。两者职责不同，但在子轮盘场景中都需要调用。

**4. 与现有模式不一致**

查看其他地方的 `ClearVisuals()` 调用模式（第 607-611 行）：

```csharp
foreach (var slot in Slots) 
{
    slot.IsActive = false;
    slot.ResetAnimation(); // ← 这里有调用！
}
```

这说明在清空视觉后，**确实需要**调用 `ResetAnimation()`。但在 `EnterSubMenuAsync` 中遗漏了这一步。

---

## 问题流程图

```
Root Menu 显示
  ↓
某些 slot 是空的 (Type = None)
  ↓
这些空 slot 的 CurrentOpacity = 0 (不可见)
  ↓
用户右键点击多窗口进程 slot
  ↓
EnterSubMenuAsync() 被调用
  ↓
ClearVisuals() - 清空 Label/Icon
  ↓ (CurrentOpacity 仍然是 0)
  ↓
重新填充 slot 数据（窗口信息）
  ↓
❌ 问题：没有调用 ResetAnimation()
  ↓
Slot 保留 CurrentOpacity = 0
  ↓
结果：窗口 slot 显示为灰色/不可见
```

---

## 解决方案

### 架构修复

**核心思路：** 在填充窗口数据后，显式重置每个 slot 的动画状态

#### 修改 `EnterSubMenuAsync` 方法

**文件：** `RadialMenuViewModel.cs` 第 1065-1087 行

```csharp
for (int i = 0; i < _slotsPerPage; i++)
{
    var slot = Slots.FirstOrDefault(s => s.SlotIndex == i + 1);
    if (slot == null) continue;

    if (i < sortedWindows.Count)
    {
        var win = sortedWindows[i];
        slot.Label = win.Title.Length > 15 ? win.Title.Substring(0, 12) + "..." : win.Title;
        slot.IconImage = win.AppIcon;
        slot.Type = SlotType.Window;
        slot.DataContext = win;
        slot.BadgeCount = 0;
        slot.ActionStrategy = new WindowSwitchStrategy(win, _usageTracker, _healthMonitor);
        
        // ✅ 修复：重置动画状态，确保 slot 完全可见
        // 没有这一步，slot 可能保留 CurrentOpacity = 0 的旧状态
        slot.ResetAnimation();
    }
    else
    {
        slot.Label = "";
        slot.LoadIconData(string.Empty);
        slot.Type = SlotType.None;
        slot.ActionStrategy = new NoOpStrategy();
        
        // ✅ 修复：空 slot 也重置状态（保持一致性）
        slot.ResetAnimation();
    }
}
```

### 修复原理

1. **显式重置动画状态**
   - `ResetAnimation()` 将 `CurrentOpacity` 设置为 1.0
   - 将 `CurrentScale` 设置为 1.0
   - 清空磁吸偏移量和速度

2. **职责清晰**
   - `ClearVisuals()` 负责清空内容
   - `ResetAnimation()` 负责重置动画状态
   - 两者分工明确，互不干扰

3. **与现有模式一致**
   - 其他地方（如第 607-611 行）也是 `ClearVisuals()` 后调用 `ResetAnimation()`
   - 保持代码库的一致性

---

## 验证方法

### 功能测试

1. **进入子轮盘**
   - 打开有多个窗口的应用（如 Chrome、VS Code）
   - 右键点击该进程的 slot
   - **预期：** 所有窗口 slot 完全可见（不灰色）

2. **鼠标交互**
   - 鼠标悬停在窗口 slot 上
   - **预期：** Slot 高亮，有磁吸效果

3. **窗口切换**
   - 点击任意窗口 slot
   - **预期：** 成功切换到对应窗口，子轮盘关闭

4. **返回根轮盘**
   - 点击中心的 "Back" 按钮
   - **预期：** 返回根轮盘，无视觉残留或状态污染

### 日志检查

修复后，子轮盘创建时应该看到：

```
[DBG] [EnterSubMenuAsync] Displaying 3 windows across 8 slots
```

不应该有任何关于 opacity 或动画状态的警告。

---

## 关键知识点

### Slot 动画状态管理

Slot 的可见性由多个属性控制：

| 属性 | 用途 | 默认值 | 重置方法 |
|------|------|--------|----------|
| `CurrentOpacity` | 整体透明度（0-1） | 0.0 | `ResetAnimation()` |
| `CurrentScale` | 缩放比例（0-1） | 0.0 | `ResetAnimation()` |
| `IsEnabled` | 启用状态（用于 Action Mode） | true | 手动设置 |
| `IsActive` | 高亮状态 | false | `ClearVisuals()` |

**关键点：**
- `CurrentOpacity` 和 `CurrentScale` 是**动画属性**，不是内容属性
- `ClearVisuals()` 只清空**内容**，不重置**动画状态**
- 必须显式调用 `ResetAnimation()` 来重置动画属性

### 子轮盘状态转换

子轮盘不是独立的菜单实例，而是同一个 RadialMenu 的**状态转换**：

```
MenuState.Root (根轮盘)
    ↓ EnterSubMenuAsync()
MenuState.SubMenu (子轮盘)
    ↓ RestoreRootMenu()
MenuState.Root (根轮盘)
```

在状态转换时，必须确保：
1. 清空旧的视觉内容（`ClearVisuals()`）
2. 重置动画状态（`ResetAnimation()`）
3. 填充新的数据（窗口信息或插件配置）

### 职责分离原则

**规则：** 内容管理和动画管理应该分离

```csharp
// ✅ 正确模式
ClearVisuals();        // 清空内容
ResetAnimation();      // 重置动画
FillData();            // 填充新数据

// ❌ 错误模式
ClearVisuals();        // 清空内容
FillData();            // 填充新数据
// 缺失：没有重置动画状态
```

---

## 相关文件

| 文件 | 修改内容 |
|------|----------|
| `ViewModels/RadialMenuViewModel.cs` | 在 `EnterSubMenuAsync` 中添加 `ResetAnimation()` 调用（1078, 1086 行） |
| `ViewModels/SlotViewModel.cs` | 无需修改（`ResetAnimation()` 方法已存在） |
| `Views/RadialMenuWindow.xaml` | 无需修改（`Opacity` 绑定已正确） |

---

## 参考资料

- [WPF Animation Overview](https://docs.microsoft.com/en-us/dotnet/desktop/wpf/graphics-multimedia/animation-overview)
- [ObservableObject Pattern](https://learn.microsoft.com/en-us/dotnet/communitytoolkit/mvvm/observableobject)
- [MULTI_WINDOW_SWITCHING_LOGIC.md](./MULTI_WINDOW_SWITCHING_LOGIC.md) - 相关的窗口切换问题

---

## 经验教训

1. **动画状态是持久的** - 动画属性（如 `CurrentOpacity`）不会自动重置，必须显式调用 `ResetAnimation()`
2. **职责分离很重要** - `ClearVisuals()` 和 `ResetAnimation()` 职责不同，不能混淆
3. **状态转换要彻底** - 从 Root Menu 到 SubMenu 的转换，必须清理所有旧状态
4. **保持代码一致性** - 如果其他地方都是 `ClearVisuals()` + `ResetAnimation()`，新代码也应该遵循
5. **XAML 绑定要理解** - `Opacity="{Binding CurrentOpacity}"` 意味着 ViewModel 的 `CurrentOpacity` 直接控制可见性

---

## 设计建议

### 未来改进方向

**1. 封装状态重置逻辑**

创建一个 `ResetSlotState()` 方法，同时清空内容和重置动画：

```csharp
public void ResetSlotState(SlotViewModel slot)
{
    // 清空内容
    slot.Label = "";
    slot.LoadIconData(string.Empty);
    slot.IsActive = false;
    slot.BadgeCount = 0;
    
    // 重置动画
    slot.ResetAnimation();
}
```

**2. 使用状态机模式**

明确定义 Slot 的状态转换：

```csharp
enum SlotState
{
    Empty,      // 空 slot
    Loading,    // 加载中
    Ready,      // 准备就绪
    Active      // 激活状态
}
```

**3. 添加运行时检查**

在 `EnterSubMenuAsync` 中添加断言：

```csharp
Debug.Assert(slot.CurrentOpacity == 1.0, "Slot opacity should be 1.0 after reset");
Debug.Assert(slot.CurrentScale == 1.0, "Slot scale should be 1.0 after reset");
```

---

**最后更新：** 2026-03-18  
**修复版本：** Pulsar v1.0.0+  
**相关 Issue：** 子轮盘窗口 Slot 灰色不可用
