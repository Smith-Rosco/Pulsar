# UI 自动化方案调研报告

> 日期：2026-09-04
> 目的：为 Pulsar 评估"内置 debug 模式 + 键鼠/截图/录屏自动化工作流"的可行方案，调研 2026 年主流技术现状，并结合项目实际给出选型建议。

---

## 1. 主流方案现状（2026 年时间点）

### 1.1 UI 驱动框架

| 方案 | 形态 | 现状 | 新项目适用性 |
|---|---|---|---|
| **FlaUI** (UIA3) | .NET 库，进程内封装 Microsoft UI Automation | MIT 开源，v5.0.0（2025-02）发布，新增 .NET 8 支持，约 2.7k stars，活跃维护 | ◎ 第一候选 |
| **WinAppDriver** | WebDriver 协议服务（微软） | 最终稳定版 v1.2.1 停留在 2020-11，闭源，1100+ 未解决 issue | △ 实质已停滞，避免新引入 |
| **Appium Windows Driver** | Appium 生态的 Windows 驱动 | Node.js 侧仍在维护，但服务端完全依赖已死的 WinAppDriver | △ 仅已有 Appium 资产时考虑 |
| **NovaWindows Driver** | Appium Windows 驱动的社区替代（Automate The Planet，AppiumConf 2025 发布） | 早期阶段，自称性能/稳定性优于 WinAppDriver | ○ 观望，尚不成熟 |
| **Coded UI** | Visual Studio 内置 | VS 2019 起弃用，新版已移除 | × 应迁移对象 |
| **商业工具**（TestComplete / Ranorex） | 录制回放 + 对象识别 | 成熟但授权费高昂 | × 个人/开源项目不划算 |
| **Pywinauto** | Python 的 UIA 封装 | 0.6.9（2025-01）仍在维护 | ○ 仅当测试栈是 Python 时 |

**关键结论**：WinAppDriver 系（含 Appium 包装）已死——闭源、无修复、HTTP 协议慢且 flaky。对 C#/.NET 项目，**FlaUI.UIA3 是当前事实标准**：进程内直调 UIA，无网络跳数，强类型控件包装（`AsButton()`），与 xUnit/MSTest 天然集成。

### 1.2 截图与录屏

| 方案 | 说明 | 适用性 |
|---|---|---|
| **FlaUI 内置 Capture** | `Capture.Screen()` / 元素级截图，GDI 实现 | ○ 简单断言够用 |
| **GDI `CopyFromScreen`** | 全屏/区域截图，零依赖 | ○ 基础截图 |
| **Windows.Graphics.Capture** | WinRT API，Win10 1903+，支持窗口级捕获（含 Popup） | ◎ 现代方案 |
| **ScreenRecorderLib** | Media Foundation + DirectX 的 .NET 封装（MIT，v6.6.0，.NET 6+），直出 H.264 MP4，依赖 VC++ Redist | ◎ 推荐录屏方案 |
| **进程内 `RenderTargetBitmap`** | WPF 可视树截图 | △ **截不到 ContextMenu/Popup**（独立视觉树），慎用 |

**关键结论**：截图断言用 GDI/FlaUI 即可；录屏直接用 **ScreenRecorderLib**，不要手写编码器。注意 ContextMenu/Popup 渲染在独立视觉树/独立 Win32 窗口中——这与本仓库 `Docs/lessons/CONTEXTMENU_RESOURCE_INHERITANCE.md` 记录的坑同源，进程内截图方案必然漏拍，必须走屏幕级或窗口级捕获。

### 1.3 AI 驱动方案（Computer Use Agents）

2025–2026 年的新兴方向：多模态 LLM 通过"截图 → 理解 → 动作（SendInput）→ 再截图"的循环操控桌面。代表：Claude Computer Use、OpenAI Operator/CUA、UI-TARS-2、Microsoft Fara-7B。

**基准现实**（Microsoft Windows Agent Arena、OSWorld）：
- 桌面复杂任务最佳成绩 **19.5%–38.1%**（人类约 72%），不可用于回归测试——flake 率不可接受。
- 每步需截图 + LLM 推理，秒级延迟 + 高成本。
- 无内置断言框架，LLM 自己验证结果 = 又一个失败点。
- 微软研究结论：**能走 UIA 树的 Agent 全面优于纯视觉方案**。

**定位**：CUA 适合**探索性测试辅助、测试步骤生成**，不适合回归执行。可作为远期增强（如用 LLM 从自然语言描述生成工作流 JSON），不是本次的基础。

---

## 2. 结合 Pulsar 的实际情况分析

### 2.1 有利条件

1. **技术栈完全匹配**：.NET 8 + C# + xUnit（现有 100+ 单测），FlaUI.UIA3 可直接放进现有 `Pulsar.Tests` 体系或平行的 `Pulsar.E2E` 项目，零语言/生态摩擦。
2. **DI 架构成熟**：`AppStartupCoordinator` 引导链清晰，debug 模式可以作为一组可选服务注册注入，不污染主流程。
3. **配置是单文件**：`Profiles.json` 单一事实源，测试可以用预制的 fixture 配置启动隔离实例。

### 2.2 Pulsar 特有的挑战（必须针对性设计）

| 挑战 | 影响 | 对策 |
|---|---|---|
| **径向菜单是全局热键/手势触发的无边框悬浮窗** | 核心路径无法用 UIA `Invoke` 触发，必须真实 SendInput | FlaUI 的 `Keyboard`/`Mouse` 类就是 SendInput 封装，直接覆盖；工作流步骤里保留"热键触发"原语 |
| **自定义渲染的径向菜单 UIA 树可能很弱**（自绘控件） | UIA 找不到菜单项元素 | 给菜单项补 `AutomationProperties.AutomationId` + `Name`；同时命名管道状态钩子兜底（导出"当前激活槽位"等内部状态） |
| **ContextMenu/Popup 独立视觉树** | 进程内截图漏拍；UIA 反而能看到 | 断言用 UIA，截图用屏幕级捕获 |
| **常驻托盘 + 单实例守护** | debug 实例被正式实例顶掉 | debug 模式绕过单实例 mutex，或用不同的 mutex 名 |
| **`Profiles.json` 污染风险**（含 `ConfigEditSession` 并发机制） | 自动化运行会改真实配置 | debug 模式重定向到独立配置目录（如 `%AppData%\Pulsar.Debug`） |
| **全局热键/鼠标钩子自触发** | 测试注入的输入会被自己的钩子捕获，测试结果失真 | debug 模式默认禁用热键监听与手势钩子，改为工作流显式注入 |
| **双语本地化（Strings.resx）** | UIA 按文本 Name 查找会随语言切换失效 | 强制规则：断言一律用 AutomationId（稳定标识），禁止用本地化文本定位 |
| **PKI 密钥管理是敏感面** | 截图/录屏可能泄漏密钥 | debug 模式下 PKI 相关界面默认脱敏（遮罩层）或不纳入录制范围 |

---

## 3. 选型结论

### 3.1 技术栈

| 层 | 选择 | 理由 |
|---|---|---|
| UI 驱动与断言 | **FlaUI.UIA3**（NuGet） | .NET 原生、活跃维护、进程内无协议开销 |
| 键鼠输入 | FlaUI `Keyboard`/`Mouse`（底层 SendInput） | 真实系统输入，覆盖热键/手势路径 |
| 截图 | FlaUI Capture / GDI `CopyFromScreen` | 简单可靠，覆盖 Popup |
| 录屏 | **ScreenRecorderLib**（NuGet，MIT） | Media Foundation 硬编码 MP4，一行 API |
| 工作流定义 | JSON 步骤文件（launch / wait / hotkey / click / assert / screenshot / record） | 与 `Profiles.json` 的配置文化一致，未来可让 LLM 生成 |
| 应用内 debug 模式 | 启动参数 `--ui-debug`：隔离配置目录 + 禁用钩子 + 绕过单实例 + 命名管道状态钩子 + Verbose 日志 | 使能外部驱动，本身不含自动化逻辑 |
| 架构 | **外部驱动进程 + 应用内 debug 模式** | 应用不驱动自己：避免输入进自己消息队列掩盖时序问题，且核心场景（全局热键）本来就是进程外触发 |

**明确排除**：WinAppDriver / Appium（项目已死）、商业工具（成本）、AI CUA 直接做回归（成功率不足）。

### 3.2 工作流 JSON 示例

```json
{
  "name": "radial-menu-open-via-hotkey",
  "steps": [
    { "type": "launch", "exe": "Pulsar.exe", "args": "--ui-debug --profile=fixtures/minimal.json" },
    { "type": "record", "action": "start", "output": "artifacts/radial-open.mp4" },
    { "type": "hotkey", "keys": "Ctrl+Alt+Space" },
    { "type": "waitForState", "pipe": "menu-open", "timeout": "3000" },
    { "type": "assert", "uia": "AutomationId:RadialWindow.Slot.0.Name" },
    { "type": "screenshot", "output": "artifacts/radial-open.png" },
    { "type": "record", "action": "stop" },
    { "type": "exit", "code": 0 }
  ]
}
```

### 3.3 分阶段实施

| 阶段 | 内容 | 规模 |
|---|---|---|
| **一：debug 模式** | `--ui-debug` 参数解析、隔离配置目录、禁用钩子/热键、绕过单实例、Verbose 日志 | 小（AppStartupCoordinator 增量改动） |
| **二：外部驱动 + E2E 骨架** | `Pulsar.E2E` 控制台项目、JSON 工作流解析、FlaUI 集成、截图断言；给核心控件补 AutomationId；覆盖"热键打开径向菜单 → 选择槽位 → 执行动作"主路径 | 中 |
| **三：录屏 + CI** | ScreenRecorderLib 集成（失败时自动保留录像作诊断）、接 GitHub Actions（注意 CI 需要交互式桌面会话，`windows-latest` runner 可用） | 中 |
| **四（可选远期）** | 命名管道状态钩子精细化；LLM 从自然语言生成工作流 JSON | 大 |

### 3.4 风险清单

1. **SendInput 需要前台焦点**：CI 环境（无头会话）里全局输入可能失败，需保证测试在交互式桌面会话运行；虚拟显示器/自动登录方案在阶段三再评估。
2. **DPI / 多显示器**：坐标点击对 DPI 缩放敏感，优先用 UIA 元素坐标（`BoundingRectangle`）而非硬编码坐标。
3. **录屏依赖 VC++ Redist 与 Win10 1903+**：CI runner 需确认镜像包含。
4. **真实输入 E2E 天然较慢且偶发 flaky**：控制数量——只覆盖核心路径（打开菜单、执行插件、设置页保存），能用 UIA 模式驱动的不要模拟鼠标；重试 + 录像诊断兜底。

---

## 4. 参考来源

- FlaUI 官方（v5.0.0，2025-02，.NET 8 支持）：https://github.com/FlaUI/FlaUI
- Microsoft Learn — WinUI 测试指南（官方确认 WinAppDriver 不再活跃开发）：https://learn.microsoft.com/en-us/windows/apps/develop/testing/
- Appium 官方驱动列表（Windows driver 标注 "not maintained since 2022"，推荐 NovaWindows）：https://appium.io/docs/ecosystem/drivers
- Automate The Planet — NovaWindows Driver 发布文（WinAppDriver 停更细节）：https://www.automatetheplanet.com/windows-tests-running-slow-maybe-not-for-long
- Qate — AI-Powered Windows Desktop Test Automation 技术指南（UIA vs 视觉 Agent 基准数据）：https://qate.ai/blog/ai-windows-desktop-testing-technical-guide
- ScreenRecorderLib（NuGet v6.6.0，Media Foundation 封装）：https://www.nuget.org/packages/ScreenRecorderLib
