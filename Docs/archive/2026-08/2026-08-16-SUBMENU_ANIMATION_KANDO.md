# 子轮盘动画重构：Kando 风格持续变形

日期：2026-08-16
参考：`E:\8_Project\10_C#\Ref\kando-main`（Kando pie menu）

## 研究结论

Kando 的子菜单动画并不“隐藏旧内容再显示新内容”，而是让 DOM 节点在状态间持续换角色：

- 被选中的子节点移动到中心，尺寸从 child 过渡到 center；
- 旧中心节点变为 parent，移动到被选中节点的原位置；
- 原孙节点从小尺寸（15px）过渡为子节点尺寸（50px）；
- 根容器平移，使选中项在整个过渡期间始终位于指针下方；
- 过渡使用 `250ms cubic-bezier(0.775, 1.325, 0.535, 1)`，末端带轻微过冲；
- 输入在过渡期间不触发选择。

Pulsar 是 WPF 架构，中心与圆环槽不是同一棵视觉树，因此采用“等价映射”而非照搬 DOM：

| Kando | Pulsar 实现 |
|-------|-------------|
| 根容器平移 | 径向菜单窗口平滑滑向鼠标位置（220ms EaseInOutCubic） |
| 选中子节点移向中心并变大 | 被点击槽通过 `Scale + Translate` 滑到画布中心，缩放到 `centerSize / slotSize` |
| 旧中心移向父节点位置 | 窗口平移使旧中心自然滑到被点击槽原屏幕位置，同时收缩淡出 |
| 孙节点从小长大 | 新窗口槽从中心以 `EaseOutBack` 绽放（260ms，轻微过冲） |
| 返回反向变形 | 子轮盘槽收缩回中心，中心“Back”移回原父槽位置，根槽再绽放 |
| 过渡期锁定输入 | `_isTransitioning` 阻止点击、滚轮、悬停和热键释放 |

## 关键实现

- `RadialMenuViewModel.SlotPose` + `AnimateAsync/AnimateSlotsAsync`：
  VM 侧轻量补间器，可在 UI 线程并发驱动多组槽位。
- `EasingFunctions.EaseInCubic` / `EaseOutBack`：
  对应 Kando 的压缩阶段和过冲绽放阶段。
- `RadialMenuWindow.RepositionToCursorAnimatedAsync`：
  用 `Window.Left/TopProperty` 的 `DoubleAnimation` 替代瞬移。
- 中心 Orb 外层的 Scale/Opacity/Translate 绑定到 `CenterSlot` 动画属性。
- `IPageProvider.ClearSlots` 清空时显式隐藏空槽，避免旧动画状态泄漏到下一页。
- `RadialMenuSubMenuCoordinator.RestoreRootMenu` 改为同步刷新，避免退出动画中途内容“跳回”。

## 速度调优（2026-08-16 追加）

- 全局提速：压缩阶段 160ms → 110ms；返回绽放 260ms → 160ms。
- 进入子菜单采用距离自适应时长：
  - `velocity = 1.8 + distance * 0.002` DIP/ms；
  - 时长范围 110–240ms；
  - 点击点越远，DIP/ms 速度越高，同时用上限防止跨屏长距离拖沓。
- 新窗口槽绽放时长 = 进入时长 + 30ms，范围 150–230ms。

## 验证

- `dotnet build`：0 warning / 0 error（产物输出到临时目录，因为运行中的 Pulsar 占用 bin）
- `dotnet test`：385/385 通过

## 建议人工 QA

1. 打开 Switcher，左键点击一个多窗口进程：
   - 被点击图标应持续位于鼠标下，逐渐变大并成为中心；
   - 旧中心应滑向被点击槽原位并淡出；
   - 新窗口槽应从中心带轻微过冲弹出。
2. 点击中心“返回”或右键：
   - 反向收缩后根菜单重新绽放；
   - 过渡期间快速移动/点击不应误触发。
3. 屏幕边缘测试：窗口平滑平移后仍保持在当前显示器工作区内。
