# 🚀 Pulsar 深度实例切换器 (Deep-Dive Switcher) 需求规格说明书

## 1. 项目愿景与核心理念 (Vision & Philosophy)

**愿景**：将 Pulsar 的 windwow switcher 模式从“应用启动器”进化为“窗口精准定位器”，完全替代并超越系统 `Alt + Tab` 在多实例场景下的体验。

* **高张力交互 (High-Tension Interaction)**：维持“物理确定性”，只要 `Ctrl` 键不放，所有操作均为可逆的预览态；松开即触发，绝不拖泥带水。
* **确定性逻辑**：利用空间记忆（扇区位置）与时间序列（启动顺序）的映射，让用户通过肌肉记忆实现盲操。

## 2. 交互共识与操作流 (Interaction Protocol)

### 2.1 触发机制 (The Trigger)

* **唯一触发动作**：**松开 `Ctrl` 键**。
* **执行条件**：当前必须处于一个有效的 `Slot` 选中状态且松开 `Ctrl`。
* **安全区域**：若鼠标位于**中心圆 (JellyOrb)** 时松开 `Ctrl`，视为取消操作，关闭 Pulsar 且不执行任何跳转。

### 2.2 双层递归导航 (Recursive Navigation)

1. **一级轮盘 (Root Level)**：展示应用/插件图标。
2. **降级进入 (Drill-down)**：在选中某应用 Slot 时按下**鼠标左键**。轮盘立即在**当前位置原位展开**子轮盘（见 3.1 节自适应偏移）。
3. **子轮盘 (Sub-menu)**：展示该进程下的所有窗口实例。
4. **回退 (Roll-back)**：在子轮盘模式下，点击**中心圆**，视图返回一级轮盘。

## 3. 功能详细描述 (Functional Deep Dive)

### 3.1 动态布局与自适应 (Smart Layout)

* **启动顺序排序**：窗口 Slot 严谨遵循启动时间序列，从 **12 点钟方向**开始顺时针排列。
* **吸附式中心偏移 (Pivot Adjustment)**：若子轮盘展开位置靠近屏幕边缘，UI 应自动向屏幕中心微移，确保圆环不被切断。
* **空间记忆**：即便只有 2 个窗口，也应保持固定的物理方位感，而非随机分布。

### 3.2 视觉辨识系统 (Visual Identification)

* **混合预览镜头 (The Lens)**：
* **中心圆**：保持圆形，作为操作锚点和“返回”按钮。
* **溢出画框**：当鼠标悬停在子 Slot 时，中心圆后方浮现一个 **16:9 的圆角矩形毛玻璃背板**，展示 DWM 实时缩略图。


* **动态标题指示器**：复用现有的底部标题栏。
* **一级态**：显示 Profile 名称。
* **二级态**：实时滚动显示当前悬停窗口的**完整 Title**（如：`192.168.1.10 - Remote Desktop`）。



### 3.3 性能与快照 (Performance & Snapshot)

* **快照机制**：所有窗口数据仅在 Pulsar 唤起的一瞬间（`Capture()`）进行捕获。
* **不可变性**：在轮盘开启期间，即便外部窗口关闭，子轮盘也不进行动态刷新，点击已失效窗口时进行“失败提示”即可。

## 4. 技术实现指南 (Technical Implementation)

### 4.1 核心组件扩展 (Core Extensions)

* **IWindowService**：新增接口获取指定进程的所有可见窗口句柄。
* **PulsarContext**：需承载 `IEnumerable<WindowInfo>` 而不仅仅是主窗口。
* **RadialMenuViewModel**：需维护一个简单的状态机（`Root` / `SubMenu`）及 `ParentSlot` 引用。

### 4.2 关键代码路径

* **Slot 判定**：外围 Slot 采用**极坐标扇区判定**；中心圆采用 **Hover 判定**。
* **UI 主题**：必须严格遵循 `Styles/ButtonStyles.xaml` 中的 `PulsarPrimaryButtonStyle` 等自定义样式，严禁直接使用 Wpf.Ui 的 `Appearance="Primary"` 属性，以防止主题注入时的颜色回退问题。

## 5. 限制与边界 (Constraints & Guardrails)

* **禁止锁存**：严禁在进入子轮盘后自动锁存状态；必须强制用户维持 `Ctrl` 按下。
* **单键简约**：右键点击在 Root 级定义为“关闭 Pulsar”，在 Sub 级暂不定义复杂功能。
* **焦点保护**：降级进入子轮盘时，鼠标位置必须保持在原本 Slot 的位置，不能发生跳变。

---

**AI 协作备忘录**：
在实现 `Sub-menu` 的动画时，请参考 `RadialMenuWindow.xaml` 中的层级结构。所有的预览预览图像抓取应封装在 `NativeMethods` 中，确保不对主 UI 线程产生阻塞。

---

**下一步建议**：
既然 PRD 已经完备，是否需要我为你生成 `IWindowService` 中用于抓取多实例窗口的 **C# 核心代码片段**？