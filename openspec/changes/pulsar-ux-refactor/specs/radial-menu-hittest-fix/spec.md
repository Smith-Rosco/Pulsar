## ADDED Requirements

### Requirement: 圆形精确 HitTest
JellyOrb 控件 SHALL 重写 HitTestCore，使命中检测区域为圆形（以控件中心为圆心，以 Size/2 为半径），而非默认的矩形边界框。

#### Scenario: 点击圆形区域内触发命中
- **WHEN** 用户点击 JellyOrb 视觉圆形范围内的任意位置
- **THEN** 控件响应点击事件（hover、command 触发）

#### Scenario: 点击圆形区域外不触发命中
- **WHEN** 用户点击 JellyOrb 边界框内但圆形外的角落区域
- **THEN** 控件不响应，事件穿透到下层

#### Scenario: Accessibility 不受影响
- **WHEN** Accessibility 工具（如 Windows Narrator）枚举控件
- **THEN** AutomationPeer 仍返回矩形边界，不影响无障碍功能
