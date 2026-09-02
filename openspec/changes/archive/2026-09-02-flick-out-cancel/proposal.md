# flick-out-cancel — Proposal

## Why

手势唤出的菜单释放时按"中心=快速切换 / 槽位=选择 / 空白=关闭"的空间解析，用户如果想放弃当前手势，只能把光标拉回中心空白区再释放——不符合"外甩即取消"的肌肉记忆，误触误选率高。StarPie 的 `ProcessMove` 中 `_lastEscapedState` 用"光标 > 外径×1.5 即虚化取消"的实时逃逸态解决此问题，Pulsar 缺这一层。

## What Changes

- 在手势唤出的菜单可见期间，实时跟踪光标与菜单中心（唤起点）的距离；光标移出外径×1.5 时进入**逃逸态（escaped）**：菜单视觉虚化（dim/fade），作为"释放即取消"的预览。
- 逃逸态下释放右键 → **取消**当前手势：关闭菜单、不执行任何槽位/快速切换。
- 光标拉回外径内 → 逃逸态**退出**，菜单恢复视觉，释放按原有空间解析（中心/槽位/空白）。
- 阈值可配置（默认 外径×1.5），仅作用于手势唤出路径；热键路径不受影响。
- 逃逸态基于**实时位移**判定（move 驱动），不是释放点的一次性判定，与 StarPie 模型一致。

## Capabilities

### New Capabilities

- `flick-out-cancel`: 手势唤出菜单的实时逃逸态（外甩取消）——光标超出外径×1.5 进入逃逸态并虚化菜单，逃逸态释放取消手势，拉回则恢复。

### Modified Capabilities

- `right-drag-threshold-replay`: "手势释放执行选择"场景需补充前置条件——释放时处于逃逸态则取消而非执行；逃逸取消的释放不得投递给源应用。

## Impact

- `ViewModels/MenuSession.cs` — `HandleGestureRightReleaseAsync` 增加逃逸态释放分支；新增逃逸态跟踪与视觉状态（move 驱动）。
- `ViewModels/RadialMenuViewModel.cs` — `OnMousePositionChanged` 路径转发光标距离给会话做逃逸判定。
- `ViewModels/RadialMenuVisualStateCoordinator.cs` — 逃逸态虚化动画。
- `Models/ProfilesConfig.cs` / `ProfileSettings` — 新增 `GestureFlickOutCancelEnabled`（默认开启或可配）与逃逸半径倍数配置。
- `Tests` — `MenuSessionGestureTests` 补逃逸态释放/拉回/取消场景。
