## Why

Pulsar 是自绘的无边框径向菜单 + 常驻托盘应用，UI 回归只能靠人工手工验证，且每次 UI 改动后"元素遮挡、样式退化"等视觉问题无法被现有 100+ 单元测试发现。纯视觉 AI Agent（CUA）直接操控桌面做回归的成功率只有 19.5%–38.1%（人类约 72%），不可作为验证基线。本项目需要一条以确定性 UIA 验证为骨架、以视觉 AI 为开发迭代驱动力的闭环：**AI 只提方案，确定性框架验收**。

## What Changes

- 新增应用内 **debug 模式**（启动参数 `--ui-debug`）：隔离配置目录、禁用全局热键/鼠标手势钩子、绕过单实例互斥、Verbose 日志、PKI 界面脱敏。应用本身只暴露"可被外部驱动"的能力，不含任何自动化逻辑。
- 新增**外部驱动进程** `Pulsar.E2E`（控制台项目）：解析 JSON 工作流（launch / wait / hotkey / click / assert / screenshot / record / exit），用 FlaUI.UIA3 驱动与断言，用真实 SendInput 覆盖全局热键路径。
- 新增**命名管道状态钩子**：debug 实例把内部状态（菜单已开、激活槽位等）发布到命名管道，驱动进程据此等待状态而非轮询 UI。
- 新增**诊断包协议**：任一用例失败时输出标准诊断包（失败断言、UIA 树快照、截图、录像、日志片段），作为视觉 AI 的输入契约。
- 新增**视觉 AI 迭代循环**：消费诊断包 → LLM 修改 XAML/样式 → 重新 build 并重跑该用例 → 直至绿。AI 不自证结果，由 E2E 框架验收。
- 新增**视觉遮挡检测**：稳定截图 + UIA 边界框投影 → 视觉 AI 输出结构化遮挡报告（只报可交互区域重叠），作为 `visual-regression` 检查并入诊断包与迭代验收。
- 核心控件补 `AutomationId` + 自定义 `AutomationPeer`，使自绘径向菜单在 UIA 树中可定位、可断言（断言一律用 AutomationId，禁止本地化文本定位）。

## Capabilities

### New Capabilities

- `ui-debug-mode`: 应用内 `--ui-debug` 调试模式——隔离配置、禁用钩子、绕过单实例、命名管道状态发布、PKI 脱敏、Verbose 日志。
- `e2e-automation-framework`: 外部驱动进程 + JSON 工作流 + FlaUI 断言 + 截图/录屏 + 诊断包协议。
- `visual-ai-iteration-loop`: 视觉 AI 迭代循环 + 遮挡检测（消费诊断包、提议修复、由 E2E 验收收敛）。

### Modified Capabilities

（无——本次全部为新增能力；不修改现有 spec 的既有需求。）

## Impact

- **应用侧**：`App.xaml.cs`（启动参数解析、debug 模式服务注册）、`AppStartupCoordinator.cs`（debug 分支：跳过热键/钩子初始化）、`ConfigService`（配置路径重定向，构造已支持 `configPath`）、`RadialMenuWindow`/径向菜单控件（`AutomationId`、`AutomationPeer`）、新增 NamedPipe 状态发布服务与 PKI 脱敏。
- **新增项目**：`Pulsar/Pulsar.E2E`（控制台驱动进程，FlaUI.UIA3 依赖）。
- **依赖**：NuGet 新增 `FlaUI.UIA3`（E2E 项目）、`ScreenRecorderLib`（录屏，可选阶段）。
- **不涉及**：不改变任何现有插件协议、`ConfigEditSession` 单写者机制、现有单元测试行为。debug 模式默认关闭，不影响正常启动路径。
- **注意**：当前代码中未发现真正的单实例 mutex（报告假设了"单实例守护"存在）；"绕过单实例"仅在确实引入互斥机制后需要，实现时需先核实。
