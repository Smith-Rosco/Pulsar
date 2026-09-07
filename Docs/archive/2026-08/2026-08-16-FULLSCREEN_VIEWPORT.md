# 全屏透明视口重构

日期：2026-08-16
状态：已实现，待真机视觉 QA

## 目标

- 径向菜单打开期间覆盖鼠标所在显示器的工作区；
- 鼠标事件不再向下层应用放行；
- 命中测试以运行时鼠标中心按角度扇区延伸到整个工作区；
- 空闲时窗口缩回 1x1，避免常驻全屏 layered surface。

## 核心改动

### IMenuViewportService / MenuViewportService
- 空闲：窗口 1x1；
- 唤起：移动到鼠标所在显示器，先置为 1x1 让 WPF 重算 DPI，再铺满该显示器 `rcWork`；
- 菜单中心：鼠标位置按 `menuExtentDip` 钳制在工作区内；
- 需要时给出 `PointerWarpRequired`，由窗口把鼠标 `SetCursorPos` 到钳制后的中心；
- 关闭：缩回 1x1。

### RadialMenuWindow
- `ViewportRoot` Canvas 覆盖整个窗口；
- `MenuCanvas` 通过 `Canvas.Left/Top` 绑定 `MenuCanvasLeft/Top`，随运行时中心移动；
- 删除了旧的 500x500 居中定位和窗口平移动画；
- 子菜单进入/退出改为在窗口内平移 `MenuCanvas`，不再移动窗口。

### 输入契约
- 当前工作区内：左键按角度扇区实时命中；空白/禁用槽 no-op；中心=取消/返回；右键=取消/返回；
- 工作区外（其他显示器）：吞掉事件并取消菜单；
- 滚轮：仅在菜单半径附近翻页，其余区域吞掉；
- `GlobalMouseHook` 在菜单可见时不再向任何下层程序透传鼠标事件。

### 安全网
- 菜单可见超过 60 秒无完成交互时，Watchdog 自动关闭，避免全屏透明窗口卡死屏幕。

## 已知风险 / QA 重点

1. WPF 透明 layered window 铺满 4K 工作区会增加显存/合成开销，需在目标机器实测帧率。
2. 进程当前未强制 PerMonitorV2 清单；混合 DPI 显示器上窗口移动可能仍需系统 DPI 感知调整。真机需在两个不同缩放显示器间唤起验证。
3. 屏幕边缘会触发指针回让；确认鼠标移动量可接受。
4. 锁定/远程桌面/屏幕保护场景应触发关闭，避免覆盖屏幕。
5. PKI Focus Boomerang 路径必须重新验证：显示全屏窗口 → 隐藏 → 注入。

## 验证

- `dotnet build`（临时输出目录）：0 warning / 0 error
- `dotnet test`：394 / 394 passed
- 新增 `MenuViewportServiceTests`：中心跟随、边缘钳制、小工作区、指针回让判定
