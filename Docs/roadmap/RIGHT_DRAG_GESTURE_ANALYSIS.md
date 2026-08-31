# 专项分析: 右键拖拽唤起 — 释放穿透与不丝滑问题根因

> **状态**: 深入分析完成 | **日期**: 2026-08-31
> **问题**: 右键拖动后释放意外发送右键到原始程序;右键菜单唤起/关闭不如快捷键丝滑。
> **对比基准**: `E:\8_Project\10_C#\Ref\StarPie-main\WinPieGestures`

---

## 1. 问题现象复述

1. **释放穿透**: 右键拖动唤出 Pulsar 菜单,释放后原始程序意外收到了右键(弹出原生右键菜单/粘贴菜单)。
2. **不丝滑**: 右键唤起的菜单,其打开与关闭的跟手程度、时序明显劣于热键唤起(`Ctrl+Q`)。

---

## 2. 两个项目的"右键事件所有权"模型对比(根因)

### 2.1 StarPie: 无条件拦截 + 位移阈值 + 释放重放

`MouseHook.HookCallback` 对**所有**右键按下(匹配 `TriggerButton`)一律触发 `OnTriggerButtonDown`,由 `GestureController` 决定所有权:

```csharp
// GestureController.Hook_OnTriggerButtonDown
if (!CheckIsIsolated(out _))   // 黑/白名单、修饰键、全屏 前置过滤
{
    _startPoint = e.Position;
    _isWaitingForThreshold = true;   // 进入阈值等待
    e.Handled = true;                // 吞掉右键按下
}
```

**核心**: StarPie 先无条件吞下右键按下,再在释放时根据"是否跨越位移阈值"分派:

| 释放时状态 | 处理 | 结果 |
|---|---|---|
| 位移 < 阈值(短按/普通右键) | `_mouseHook.ReplayTriggerClick(btn)` | **重放**"按下+抬起"给源程序 → 原生右键菜单照常弹出 |
| 位移 ≥ 阈值(手势激活) | 吞掉释放,`ActionExecutor.Execute` | 手势动作,源程序感知不到 |

`ReplayTriggerClick` 的关键防递归设计:

```csharp
public void ReplayTriggerClick(string? triggerButton = null)
{
    _ignoreNextButtonDown = true;      // 重放的下一次 Down 直接放行
    _ignoreNextButtonUp = true;        // 重放的下一次 Up 直接放行
    mouse_event(MOUSEEVENTF_RIGHTDOWN, ...);   // 注入一次完整点击
    mouse_event(MOUSEEVENTF_RIGHTUP, ...);
}
```

- **为什么丝滑**: 普通右键(未达阈值)永远能得到原生菜单——Pulsar 缺的正是这个"重放"分支。
- 位移阈值在 **hook 线程无锁计算**(`ProcessMove` 中 `Math.Atan2`/勾股),`BeginInvoke(DispatcherPriority.Input)` 极速派发 UI,跟手度高。

### 2.2 Pulsar: 修饰键条件拦截 + 无阈值 + 无重放

`RightDragGestureDetector` 是纯状态机,所有权判定在**右键按下瞬间**按修饰键决定:

```csharp
public RightDragGestureDecision OnRightDown(bool switcherModifierHeld, bool actionModifierHeld)
{
    if (actionModifierHeld)  { IsPressed=true; IsSummoned=true; return ActionSummon; }
    if (switcherModifierHeld){ IsPressed=true; IsSummoned=true; return SwitcherSummon; }
    return None;   // 无修饰键 → 完全穿透
}
```

`RadialMenuViewModel.FeedRightDragGesture` 按判定结果:
- `ActionSummon`/`SwitcherSummon`: `e.Handled = true` + `ShowAsync(...)` → **吞掉右键按下,立即唤出菜单**(无位移阈值)。
- 释放时 `OnRightUp()` 若 `IsSummoned` 为真 → `GestureRelease` → 吞掉并 `HandleGestureRightReleaseAsync()`。

---

## 3. 逐条根因分析

### 3.1 根因 A: 缺少"未达阈值的右键重放" → 用户想普通右键却唤出菜单 / 或反之

- Pulsar 一旦按下修饰键+右键,**无论是否拖动**立即唤出菜单并吞掉事件 → 用户原本只想"带修饰键的普通右键"(如浏览器里 Shift+右键),却被迫唤出 Pulsar 菜单,源程序失去右键。
- 反方向: 用户**没按修饰键**直接右键拖动 → `None` 完全穿透 → 拖动过程中源程序可能已收到 Down/Up,产生"意外右键"。

> **StarPie 解法**: 引入 `DragThreshold`(默认 25px),未达阈值则 `ReplayTriggerClick` 补偿源程序,两侧都正确。

### 3.2 根因 B: 释放竞态 — `Reset()` 清空 `IsSummoned` 导致释放穿透

`RefreshGestureConfig()` 在配置更新时:

```csharp
if (!_gestureEnabled)
{
    _gestureDetector.Reset();   // IsPressed/IsSummoned 全部清空
}
```

若**手势进行中**(菜单已唤出)恰好发生配置刷新(如 `ConfigUpdated` 事件、其他线程写入配置),`IsSummoned` 被清空 → 释放时 `OnRightUp()` 返回 `None` → **右键 Up 穿透给源程序**,弹出原生菜单。这是"释放意外发送右键到原始程序"最可能的直接代码级根因。

### 3.3 根因 C: 唤起/关闭的派发时序不如热键

| 路径 | 线程跳转 | 结果 |
|---|---|---|
| 热键唤起 | `OnHotkeyInvoked` → `ShowAsync`(同步) | 直达,跟手 |
| 手势唤起 | hook 线程 → `InvokeOnUi`(默认 `DispatcherPriority.Normal`)→ `ShowAsync` | 多一跳,优先级低 |
| 热键关闭 | `HandleKeyUp` 同步 `IsVisible=false` | 即时 |
| 手势关闭 | `HandleGestureRightReleaseAsync`(异步)+ `IsVisible=false` 在 await 之后 | 异步延迟 |

- `ShowAsync` 里还有 `FirstFrameBudgetMsDefault = 50` 的 deadline-bounded show 路径,窗口先 `Opacity=0` 显示再 fade in,视觉上比热键多一次透明帧。
- StarPie 用 `DispatcherPriority.Input`(且位移在 hook 线程计算),跟手度更高。

### 3.4 根因 D: 加载中释放的降级路径与菜单未显示冲突

`HandleGestureRightReleaseAsync` 在 `!_isLoading` 时直接快速切换,若菜单尚未显示而用户已释放,行为变成"立即切回上一窗口"——与用户"看到菜单再选"的预期不符,感觉不丝滑。

---

## 4. StarPie 值得移植的机制清单

| # | 机制 | StarPie 位置 | 价值 |
|---|---|---|---|
| 1 | 位移阈值 + 未达阈值重放点击 | `GestureController` + `MouseHook.ReplayTriggerClick` | 根治 3.1/用户主诉 |
| 2 | `_ignoreNextButtonDown/Up` 防递归 | `MouseHook` | 重放安全 |
| 3 | hook 健康检查定时器(卡死自动重装) | `MouseHook.CheckHookHealth` | 钩子失效自愈 |
| 4 | 位移计算放 hook 线程 + `Input` 优先级派发 | `GestureController.ProcessMove` | 跟手度 |
| 5 | 前置隔离过滤(黑白名单/修饰键/全屏) | `GestureController.CheckIsIsolated` | 避免误唤 |
| 6 | 修饰键释放不影响已唤出手势 | `IsModifierKey` 判定 | 释放稳定 |

---

## 5. 建议修复方案(Pulsar 侧)

### 5.1 引入"阈值 + 重放"手势状态机(根治主诉)
- 扩展 `RightDragGestureDetector`(或新增 `GestureThresholdState`),由"修饰键瞬间判定"改为:
  - Down: 记录 `_startPoint`,吞下**所有**匹配修饰键的右键按下,进入 `WaitingForThreshold`。
  - Move: 位移 ≥ `TriggerDistance`(复用现有 `ProfileSettings.TriggerDistance`,默认 100)时激活菜单;否则保持等待。
  - Up: 若激活 → 执行选择(现状逻辑);若仍在 `WaitingForThreshold` → **重放右键点击**给源程序(实现 `MouseHook.ReplayTriggerClick` 等价物 + `_ignoreNext` 防递归)。
- 注意: `MouseTrackingService` 采样节流 16ms,位移判定建议在 hook 回调内做(同 StarPie),避免渲染循环延迟。

### 5.2 修复释放竞态(根因 B)
- `Reset()` 只应在**非手势进行中**(`!IsPressed && !IsSummoned`)时被 `RefreshGestureConfig` 调用;进行中需等释放完成后再应用新配置。
- 或者: `FeedRightDragGesture` 中,若 `IsSummoned` 因外部 Reset 丢失,释放时按 `_session.IsVisible` 兜底吞掉(菜单还开着说明手势尚未结束)。

### 5.3 提升派发优先级(根因 C)
- 手势唤出路径改用 `DispatcherPriority.Input`(参照 `WpfUiDispatcher`/`IUiDispatcher` 现有接缝),关闭路径改为同步隐藏再异步执行动作。

### 5.4 补测试
- `RightDragGestureDetectorTests` 新增: 阈值内释放→重放;阈值外释放→执行;Reset 竞态→释放不穿透;修饰键中途松开→释放仍归属手势。
- `MenuSessionGestureTests` 新增: 加载中释放降级路径。

---

## 6. 与既有设计的兼容性

- `ProfileSettings.TriggerDistance` 已有(默认 100),可直接复用为阈值;若需更小的盲操阈值,新增 `GestureDragThreshold`(默认 25,对齐 StarPie)。
- "位移唤出"与"按下即唤出"并存: 提供 `GestureSummonMode` 配置(Immediate / OnThreshold)。
- 重放机制与 `GlobalMouseHook` 的 `Handled` 语义兼容,需在 `GlobalMouseEventArgs` 增加重放识别位防递归。

---

## 7. 结论

Pulsar 右键手势不丝滑的**三个可落地的修复点**:
1. **补"未达阈值→重放点击"分支**(根治"意外右键") — 高价值,改动集中在 `RightDragGestureDetector` + `GlobalMouseHook`。
2. **修复 `Reset()` 释放竞态** — 低风险高回报,一条 guard 即可。
3. **派发优先级提升 + 关闭同步化** — 优化跟手度。

以上均不影响热键路径与插件架构,可与方向一(手势细节深化)合并为一个迭代落地。
