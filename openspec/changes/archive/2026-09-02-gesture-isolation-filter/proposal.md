# gesture-isolation-filter — Proposal

## Why

Pulsar 的右键手势唤起目前**没有任何前置隔离过滤**：只要按住配置的修饰键 + 右键，无论前台处于什么环境都会吞掉事件并唤起菜单。在**全屏应用 / 游戏**（Win+右键常见于游戏内操作）、**桌面 / 任务栏**（StarPie 已踩过 `Progman`/`Shell_TrayWnd` 误判坑）、或**指定不希望被手势打扰的进程**中，用户会误唤出菜单且右键事件被吞，体验断裂。StarPie 用 `GestureController.CheckIsIsolated` 的"黑白名单 + 修饰键 + 全屏"三层前置过滤解决了这个问题，Pulsar 缺这一层。

## What Changes

- 新增**手势作用域的隔离过滤**：在 `FeedRightDragGesture` 吞下右键 Down 之前，先判定当前前台窗口是否允许手势唤出。
- 新增**全屏防误触**：前台窗口为全屏（且非 Pulsar 自身）时，手势不接管（可配置开关），并正确旁路 `Progman`/`WorkerW`/`Shell_TrayWnd` 类名误判。
- 新增**进程隔离双模态**：`IsolationMode` 支持 `Allowlist`（白名单）/ `Blocklist`（黑名单）两种模式，按前台进程名判定手势是否接管。
- 现有 `IWindowEligibilityEvaluator` 的 ADR-010 接缝**只覆盖窗口切换**（WinSwitcher 黑名单），本 change 为手势新增一条独立的判定通道，不与窗口切换共享误判逻辑。
- 配置项进入 `ProfileSettings`，默认关闭（保持现有行为），用户在设置中开启。

## Capabilities

### New Capabilities

- `gesture-isolation-filter`: 右键手势唤起的前置隔离判定——全屏检测（含 `Progman`/`WorkerW`/`Shell_TrayWnd` 旁路）与进程黑白名单双模态，决定手势是否接管右键事件。

### Modified Capabilities

- `right-drag-threshold-replay`: 其"手势释放执行选择"场景需补充前置条件——仅当手势通过隔离过滤接管后，释放才解析为菜单选择；被隔离拒绝的右键按普通右键处理。

## Impact

- `ViewModels/RadialMenuViewModel.cs` — `FeedRightDragGesture` 吞 Down 前插入隔离判定。
- `Services/WindowSwitching/WindowEligibilityEvaluator.cs` 及 `IWindowEligibilityEvaluator` — 新增手势作用域评估（装饰器或独立实现），复用 ADR-010 接缝但不改窗口切换语义。
- `Models/ProfilesConfig.cs` / `ProfileSettings` — 新增 `IsolationMode`、进程名单、全屏开关配置。
- `ViewModels/SettingsViewModel` 系列 — 设置 UI 暴露手势隔离配置。
- `Tests` — `RightDragGestureDetectorTests` / `MenuSessionGestureTests` 模式补隔离过滤测试。
