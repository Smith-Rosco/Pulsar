# ADR-020: 右拖拽手势编排归位 MenuSession（VM 回到 input-source adapter）

**Status**: Accepted (2026-09-04)
**Date**: 2026-09-04
**Deciders**: Pulsar Development Team
**Related**: architecture review 2026-09-04 (candidate L); ADR-008（决策 2「VM = input-source adapter + binding projection」——本 ADR 是该决策的收尾）; ADR-017（AppStartupCoordinator 混合注入先例）; `RightDragGestureDetector`（纯状态机，本次不动）

## Problem

第二轮架构审查（候选 L）核实：右拖拽召唤手势的编排约 465 行（字段区 + 方法区）全部落在 `RadialMenuViewModel`，占该文件（876 行）的 53%。六项职责——修饰键判别、pending 吞没（LEAK-FIX）、延迟应用（D3）、isolation 过滤（D4）、replay、阈值位移召唤——全部由 VM 承担，`FeedRightDragGesture` 单方法 165 行。

这直接违反 ADR-008 决策 2 的既定方向（VM 只做 input-source adapter + 绑定投影，会话决策归 MenuSession）。测试成本同样实锤：`RightDragGestureIsolationTests.CreateHarness` = 7 mock + 真 MenuSession（内再 7 mock + `DirectUiDispatcher`）；`DirectUiDispatcher` 在 9 个测试文件中逐字重复定义。

## Decision

1. **编排整体搬迁**：手势字段区与全部编排方法（`RefreshGestureConfig`、`ApplyGestureConfig`、`ApplyPendingGestureConfig`、`BuildIsolationSettingsSnapshot`、`FeedRightDragGesture`、`ResolvePendingGestureUp`、`OnGlobalMouseMove`→`FeedGlobalMouseMove`、`IsModifierHeld`）逐字搬入 `MenuSession`，[DEBUG-RDX] 日志原样保留。会话内部引用（`_session.IsVisible`、`SetInvocationPointScreen` 等）改为直接成员访问。
2. **VM 只留两个薄适配器**：`OnGlobalMouseEvent` 首行转发 `_session.FeedRightDragGesture(e)`；`OnGlobalMouseMove` 转发 `_session.FeedGlobalMouseMove(e)`。VM 手势状态字段全部删除。
3. **配置刷新单入口**：`RefreshGestureConfig` 并入 `MenuSession.RefreshConfig`（并从 `Initialize()` 加载初始值）。VM 的 `OnConfigUpdated` 不再单独刷新手势配置；D3（手势进行中延迟应用）语义不变。
4. **召唤路径**：session 私有 `SummonGestureMenu()` 在 UI 线程先调渲染预热回调、再 `BeginSessionAsync(mode, MenuInvocationSource.RightDragGesture)`，与热键路径（VM.ShowAsync = ApplyRadialRendering + BeginSessionAsync）保持一致。
5. **新增 seam（全部可空，默认 null，既有测试/旧 DI 不破坏）**：MenuSession ctor 增 `IGestureIsolationService?`、`IGlobalMouseService?`（replay 用）、`Action<RadialMenuMode>?`（渲染预热回调）。组合根以 `sp => mode => sp.GetRequiredService<RadialMenuViewModel>().ApplyRadialRendering(mode)` 注册回调——VM 在首次召唤时才惰性解析，无构造环；`ApplyRadialRendering` 相应从 private 提为 internal。
6. **`IUiDispatcher` 增 `InvokeWithInputPriority(Action)`**（D4 优先级语义，等价于原 VM 私有 `InvokeOnUiInput`）：`WpfUiDispatcher` 实现为 `DispatcherPriority.Input` 的 null-safe 非阻塞分发；测试 fake 直调。VM 内部仅为 `SlotsPerPageChangedMessage` 保留原私有 helper。
7. **测试收敛**：9 个测试文件的私有 `DirectUiDispatcher` 收敛为 `Pulsar.Tests/TestHelpers/DirectUiDispatcher.cs`；Isolation/Leak 两套手势测试迁移为 session 面（直接驱动 `session.FeedRightDragGesture`，config/hotkey/globalMouse mock 与 session 同源）。

## Consequences

Positive:
- ADR-008 决策 2 收尾完成：VM 从 876 行减至 ~410 行，手势决策全部可脱离 VM 直接在 session 面测试（harness 少一层 VM 转发与 7 个 mock）。
- 六项手势职责与 D2/D3/D4/LEAK-FIX 语义逐字保留，行为零变化；时序知识（先 warmup 后 BeginSession、先配置后订阅）在 session 内聚合。
- `DirectUiDispatcher` 单一定义；后续 dispatcher seam 演进只改一处。

Negative:
- MenuSession ctor 参数继续增长（+3 可空）；行数增加约 470（深模块方向上的已知取舍，见 ADR-008）。
- 渲染预热回调经组合根惰性解析 VM，是 session→VM 的唯一反向触点——若未来渲染预热脱离 VM，应将 `ApplyRadialRendering` 的 seams 抽为独立 warm-up 模块并删除该回调。
- 大搬迁 diff 审查成本高（手势区是 [DEBUG-RDX] 高频调试区）；逐字搬迁 + 全量回归缓解。

## Implementation

- `Pulsar/Pulsar/ViewModels/MenuSession.cs`（手势字段区 + 编排方法区 + ctor 3 参数 + `RefreshConfig`/`Initialize` 挂接 + `IUiDispatcher.InvokeWithInputPriority`）
- `Pulsar/Pulsar/ViewModels/RadialMenuViewModel.cs`（删 465 行手势区；两个薄转发；删 `gestureIsolationService` ctor 参数）
- `Pulsar/Pulsar/ViewModels/WpfUiDispatcher.cs`（`InvokeWithInputPriority`）
- `Pulsar/Pulsar/App.xaml.cs`（`Action<RadialMenuMode>` 预热回调注册）
- `Pulsar/Pulsar.Tests/TestHelpers/DirectUiDispatcher.cs`（新；9 处收敛）
- `Pulsar/Pulsar.Tests/ViewModels/RightDragGestureIsolationTests.cs`、`RightDragGestureLeakTests.cs`（迁移 session 面）
- 其余 7 个 MenuSession/VM 测试文件（using + 删本地 dispatcher）

## Verification

- `dotnet build Pulsar.sln` → 0 错误；警告全部来自既有基线文件（E2E Recorder CS8622、AppStartupCoordinator 系 CS8625/CS8603 等），候选 L 改动文件零新增。
- 定向测试（RightDragGesture*/MenuSession*/CascadeSubMenu*/GroupedSlotInteraction*/RadialMenuWindowHandleWiring*/RadialMenuRendererSelection*）→ 89/89 通过。
- 全量 `dotnet test` → 见 journal 当日追记（以实际数字为准）。
