## ADDED Requirements

### Requirement: DPI 感知窗口尺寸
Radial Menu 窗口 SHALL 在 Loaded 事件时读取系统 DPI 缩放系数，并据此动态调整窗口尺寸和菜单布局半径，确保在 100%、125%、150%、200% 缩放比例下视觉尺寸一致。

#### Scenario: 100% DPI 下正常显示
- **WHEN** 系统 DPI 缩放为 100% 时 Radial Menu 被触发
- **THEN** 窗口尺寸为 500×500，布局半径使用基准值

#### Scenario: 150% DPI 下正确缩放
- **WHEN** 系统 DPI 缩放为 150% 时 Radial Menu 被触发
- **THEN** 窗口物理像素尺寸适配 DPI，Slot 视觉尺寸与 100% 下保持相同的物理屏幕大小

### Requirement: Slot 数量自适应半径
Radial Menu 布局系统 SHALL 根据当前激活的 Slot 数量动态调整布局半径：Slot 数 ≤ 4 时使用缩减半径（基准值 × 0.8），Slot 数 > 4 时使用标准半径。

#### Scenario: 少量 Slot 时菜单更紧凑
- **WHEN** 当前 Context 只有 2 个 Slot
- **THEN** Slot 圆心到菜单中心的距离为标准半径的 80%，菜单整体更紧凑

#### Scenario: 多 Slot 时使用标准半径
- **WHEN** 当前 Context 有 6 个 Slot
- **THEN** Slot 圆心到菜单中心的距离使用标准半径，Slot 间距均匀分布

### Requirement: 多显示器切换时重新计算
Radial Menu 窗口 SHALL 监听 DpiChanged 事件，在显示器切换或 DPI 变化时重新计算并应用新的布局参数。

#### Scenario: 窗口移动到高 DPI 显示器
- **WHEN** 用户将 Radial Menu 所在窗口移动到 DPI 不同的显示器
- **THEN** 系统触发 DpiChanged 事件，窗口尺寸和布局半径重新计算
