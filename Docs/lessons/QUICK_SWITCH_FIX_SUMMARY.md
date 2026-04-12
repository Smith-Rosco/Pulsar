# Quick Switch 修复总结

## 2026-04 架构更新

Quick Switch 逻辑不再作为 `WindowService` 里的偶发状态块存在，而是迁移为独立的 `QuickSwitchEngine`。

新的结构有几个关键点：

- Quick Switch 的 history、pair、timeout 由 `QuickSwitchEngine` 单独拥有
- 目标解析后统一走共享激活路径 `WindowActivator`
- `WindowService` 只保留 facade 职责，对外集成点不变
- 回退到 previous window 的逻辑仍保留，但现在是显式 resolution 结果，而不是隐式副作用

这次更新的目标不是改变肌肉记忆，而是把行为语义钉死：

- 有有效 pair 时，在 pair 两端之间来回切换
- pair 超时或失效时，退回历史栈
- 历史栈无效时，再退回 previous window

对应测试已补齐：

- pair 反向切换
- timeout 失效
- tracked window 无效时回退 previous window

**日期**: 2026-03-08  
**问题**: 第一次 Quick Switch 在当前窗口闪烁  
**修复**: 一行代码的优雅解决方案

---

## 问题症状

用户在窗口A工作，切换到窗口B后，第一次按 Quick Switch：
- ❌ 在窗口B闪烁（没有切换到窗口A）
- ✅ 第二次及后续 Quick Switch 正常工作

---

## 根本原因

**`WindowService.cs:584` 的逻辑错误**：

```csharp
// [错误] 只在非 Pulsar 窗口时才弹出当前窗口
if (!currentIsPulsar && _windowHistory.Count > 0 && _windowHistory.Peek() == current)
{
    _windowHistory.Pop();
}
```

**问题**：当 Pulsar 菜单显示时（`currentIsPulsar = true`），代码不会弹出"真正的当前窗口"（`_previousWindowHandle`），导致：

1. 历史栈顶部是用户正在使用的窗口B
2. 从历史栈 Pop 出来的也是窗口B
3. Source = B, Target = B → 在同一窗口闪烁

---

## 优雅的解决方案

**引入 `realCurrentWindow` 概念**：

```csharp
// [修复] 正确识别"真正的当前窗口"
IntPtr realCurrentWindow = currentIsPulsar ? _previousWindowHandle : current;

if (realCurrentWindow != IntPtr.Zero && 
    _windowHistory.Count > 0 && 
    _windowHistory.Peek() == realCurrentWindow)
{
    _windowHistory.Pop();
}
```

**核心洞察**：
- 如果当前是 Pulsar → 真正的当前窗口是 `_previousWindowHandle`
- 如果当前不是 Pulsar → 真正的当前窗口是 `current`

---

## 为什么优雅？

1. **一行核心代码**：`IntPtr realCurrentWindow = currentIsPulsar ? _previousWindowHandle : current;`
2. **语义清晰**：明确区分"前台窗口"和"用户真正在使用的窗口"
3. **统一逻辑**：无论 Pulsar 是否显示，都使用相同的处理逻辑
4. **根本解决**：从源头上确保历史栈的正确性

---

## 工作流程（修复后）

```
用户在窗口B按下 Quick Switch：

1. Pulsar 菜单显示
2. currentIsPulsar = true
3. realCurrentWindow = _previousWindowHandle = B ✅
4. 历史栈：[A, B] → Pop B → [A] ✅
5. 从历史栈找到窗口A ✅
6. Source = B, Target = A ✅
7. 切换到窗口A ✅
8. 第一次就成功！✅
```

---

## 辅助修复

为了完整性，还做了两个辅助修复：

### 1. 更新 `_previousWindowHandle`

在切换后更新 `_previousWindowHandle`，确保下次切换时能正确识别当前窗口：

```csharp
ForceForegroundWindow(targetWindow);
_previousWindowHandle = targetWindow;  // 更新
```

### 2. 防御性检查

防止建立相同窗口的切换对（理论上不应该发生）：

```csharp
if (sourceWindow != targetWindow && sourceWindow != IntPtr.Zero)
{
    _quickSwitchSource = sourceWindow;
    _quickSwitchTarget = targetWindow;
}
```

---

## 测试场景

| 场景 | 期望结果 | 状态 |
|------|----------|------|
| 窗口A → 窗口B → Quick Switch | 立即切换到A | ✅ |
| 连续 Quick Switch 10次 | 在A和B之间来回切换 | ✅ |
| A → B → C → Quick Switch | 切换到B | ✅ |
| 只有一个窗口 → Quick Switch | 无操作或提示 | ✅ |

---

## 经验教训

### 1. 语义清晰是王道

使用 `realCurrentWindow` 而不是复杂的条件判断，让代码意图一目了然。

### 2. 从根本上解决问题

不要依赖后续的补偿逻辑（如 Switch Pair），而是从源头上确保数据结构的正确性。

### 3. 一行代码的力量

有时候，最优雅的解决方案只需要一行代码：

```csharp
IntPtr realCurrentWindow = currentIsPulsar ? _previousWindowHandle : current;
```

这一行代码解决了困扰用户的"第一次闪烁"问题。

---

## 相关文档

- [完整分析文档](./QUICK_SWITCH_REMOTE_DESKTOP_FIX.md) - 详细的问题分析和解决方案
- [架构文档](../architecture/PLUGIN_SYSTEM.md) - 插件系统架构
- [窗口服务](../../Pulsar/Pulsar/Services/WindowService.cs) - 实现代码

---

*Last Updated: 2026-03-08 20:30*  
*Author: AI Architecture Analysis*  
*Version: 1.0.0 - Final Elegant Solution*
