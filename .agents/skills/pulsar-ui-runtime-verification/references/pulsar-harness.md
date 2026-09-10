# Pulsar 侧具体操作（harness）

本文件是本 skill 的落地细节：命令怎么跑、事件在哪、模板怎么用。所有路径以仓库根 `E:\8_Project\10_C#\Pulsar_Project` 为基准。

## 1. 构建与测试（沙箱环境）

必须在 Bash 里带 Windows 环境变量前缀，否则 `dotnet` **还原阶段**就崩。含括号的变量名（`ProgramFiles(x86)`）**只能走 `env` 前置**——bash 的 `export` 会报 `not a valid identifier`。

```bash
cd "E:/8_Project/10_C#/Pulsar_Project" && env \
  "APPDATA=C:\\Users\\milo\\AppData\\Roaming" \
  "LOCALAPPDATA=C:\\Users\\milo\\AppData\\Local" \
  "ProgramData=C:\\ProgramData" \
  "ProgramFiles=C:\\Program Files" \
  "ProgramFiles(x86)=C:\\Program Files (x86)" \
  "CommonProgramFiles=C:\\Program Files\\Common Files" \
  "CommonProgramFiles(x86)=C:\\Program Files (x86)\\Common Files" \
  dotnet build Pulsar/Pulsar.sln -v minimal > artifacts/<name>-build.log 2>&1; echo "exit=$?"; tail -15 artifacts/<name>-build.log
```

- **不要**从 bash 调 `scripts/dev.ps1`（跨 shell 调用被禁 / 不可用）。
- PowerShell 工具环境是 **pwsh**：没有 `powershell` 命令，脚本用 `& "…\x.ps1"` 直接调用；需要并发时用后台运行。
- PowerShell 工具偶发拿不到 stdout → 把输出重定向进 `artifacts/` 再用 Read 读回，比反复重试可靠。
- 定向跑测试：`dotnet test Pulsar/Pulsar.Tests/Pulsar.Tests.csproj --no-build --filter "FullyQualifiedName~<类名>"`
- 全量：`dotnet test Pulsar/Pulsar.Tests/Pulsar.Tests.csproj --no-build`（本仓库当前基线量级 ≈1500，报数时以实际输出为准）。

## 2. E2E 驱动界面（可脚本化，含真实鼠标）

```bash
dotnet "Pulsar/Pulsar.E2E/bin/x64/Debug/net8.0-windows/Pulsar.E2E.dll" run \
  --workflow "artifacts/<workflow>.json" --artifacts "artifacts/e2e-nav" --run-id <id>
```

要点：

- `click` 步骤走 FlaUI **真实鼠标输入**（`Mouse.MoveTo` + `Click`）→ 与用户手点同一条 `PreviewMouseLeftButtonUp` 路径。**会短暂移动用户的物理鼠标，运行前告知。**
- `launch` 步骤**固定耗时 15s**（`AppLauncher` 稳定期循环跑到 deadline），排流程别按「启动即返回」估时。
- `AppLauncher` 用 `--ui-debug` + `%AppData%\Pulsar.Debug\` 配置副本（fixture 机制）→ **不碰用户真实配置**。
- 设置窗口导航项自动化 ID：`Pulsar.Settings.Nav.<pageId>`，`pageId ∈ Slots | Plugins | General | Appearance | Analytics`；槽位编辑临时页为 `slot-editor:<Profile>:<n>`。
- 唤起设置窗口：`{ "type": "command", "command": "open-settings" }`。
- 支持的步骤类型：`launch` / `wait` / `command` / `assert` / `click` / `exit`（详见 `WorkflowRunner`）。
- 模板见 `assets/e2e-nav-click-workflow.json`。

## 3. 指示器 / 导航动画的关键位置（`Views/SettingsWindow.xaml.cs`）

汇聚函数（**防护唯一该加的地方**）：

- `RepositionNavIndicator()` —— 事件驱动的延迟重定位（调度到 `DispatcherPriority.Render`）。
- `RepositionNavIndicatorImmediate(bool clearHeldAnimations)` —— 同步写基础值；`clearHeldAnimations: true` 会 `BeginAnimation(prop, null)`，**把在跑的动画整段摘掉**——这就是「瞬态」类缺陷的元凶。

四条事件源（改任何一条都要回头看汇聚点是否有门）：

| 事件 | 挂钩点 | 处理函数 |
|---|---|---|
| `Window.DpiChanged` | `SettingsWindow.xaml.cs:197` | `OnWindowDpiChanged`（:231） |
| `NavPaneGrid.SizeChanged` | `:196` | `OnNavPaneSizeChanged`（:226） |
| `NavigationView.PaneOpened/PaneClosed` | `:194-195` | `OnNavPaneStateChanged`（:221） |
| `NavigationView.LayoutUpdated` | `:202` | `OnNavPaneLayoutUpdated`（:214）—— **唯一原本带 `_isNavAnimating` 门的** |

动画参数：两段（stretch 120ms + snap 130ms）、`CubicEase { EaseInOut }`、指示器 `Width=3 / Height=22`。
**坑**：动画起点必须显式给 `From`；`From=null` 的隐式起点（取属性当前基础值）会被并发的基础值写入改写 → 表现为「先跳后滑」。

## 4. 追踪通道插入方式（临时，用完整段拆除）

在 `SettingsWindow.xaml.cs` 加一个私有静态写文件方法，打到 `%TEMP%\pulsar-nav-trace.log`（追加、带毫秒时间戳），每条分支入口打点，例如：

```
11:46:24.388 [REPOS] dpi-changed animating=True
11:46:24.469 [ANIM] PHASE1 baseTop=207.0 baseH=22.0 → stretchTop=69.7 stretchH=159.3
11:46:24.470 [SNAP] caller=InitializeNavIndicator top=207.0→69.7 clearAnim=True animating=True
11:46:24.599 [ANIM] PHASE2 atTop=69.7 atH=22.0
```

分析套路：**同一事件序列内，`PHASE2` 的起步值是否还在起点附近** —— `atH=22.0`（= `IndicatorHeight`，终点值）= 零可见位移 = 瞬态；`atH=119/159/199` = 真的在动。

拆除残留自查：为日志加的 `CallerMemberName` 形参、被展开的单行守卫、多余空行。

### 4a. 行格式契约（分析脚本依赖，改动埋点前先看这里）

历史埋点出现过**同一语义、两种行样式**，解析时必须都兼容（否则统计会静默为 0，得出相反结论）：

| 语义 | 已知样式 |
|---|---|
| 导航开始 | `HH:mm:ss.fff [NAV] old='<A>' new='<B>' prevActive='<A>' animating=<bool>` |
| 事件驱动重定位 | `[REPOS] dpi-changed animating=<bool>` / `[REPOS] pane-size-changed <W>x<H>→<W>x<H> animating=<bool>` |
| 守卫抑制 | `[REPOS] SKIP animating-suppressed`（事后修复加了守卫后的样式；早期写在 `[ANIM]` 前缀下） |
| 动画阶段 1 | `[ANIM] PHASE1 baseTop=… baseH=… → …`（**早期**）／`[ANIM] PHASE1 startTop=… startH=… → …`（**后续**） |
| 直接落定 | `[SNAP] caller=<Caller> item='<tag>' top=A→B left=C→D clearAnim=<bool> animating=<bool>` |
| 动画阶段 2 | `[ANIM] PHASE2 atTop=… atH=…` |
| 放弃动画 | `[ANIM] SKIP boundsInvalid …`（其他 reason 同理） |

改埋点时**优先沿用已有样式**；若必须改（如 `baseTop` → `startTop`），同步更新 `scripts/analyze-nav-trace.py`。

### 4b. 一键分析（不要手工 awk）

```bash
python "<skill>/scripts/analyze-nav-trace.py" artifacts/<before>.log artifacts/<after>.log
```

传入两份日志即自动给出「关键签名计数 + 逐次导航对照（dpi / pane / skip / PHASE2 起步高度）+ 前后对照」。
判据：`BAD-1 clearAnim=True animating=True` 与 `BAD-2 PHASE2 起步高度 ≤22` 均为 **0** 才算 PASS。

## 5. 静态布局守卫

- 位置：`Pulsar/Pulsar.Tests/UI/SettingsLayoutGuardTests.cs`；现有关键用例 `Tab_hosted_dialog_contents_do_not_pin_a_fixed_dialog_size`。
- 思路见 SKILL.md「静态守卫」节：自维护解析承载集合 + 只扫根标签 + 剥注释 + 空集合 fail-fast。
- 相关 lesson：`Docs/lessons/WPF_SETTINGS_PANEL_WIDTH_CONTENT_DRIVEN.md`、`Docs/lessons/WPF_INDICATOR_ANIMATION_TORN_BY_LAYOUT_EVENT.md`。

## 6. 文档落点与提交纪律

- 会话正文 → `Docs/journal/YYYY-MM-DD.md`（ADR-019 单一来源；**不要**复制进 harness 原生 memory）。
- 待办 → `Docs/journal/NEXT.md`。
- 可复用坑位 → `Docs/lessons/<NAME>.md`（symptom → root cause → fix）。
- 结构化报告 → `Docs/reports/YYYY-MM-DD-<TOPIC>.html`。
- 提交：AI 只做本地 commit，**push 由用户执行**。
- 坑：报告 HTML 用预览打开会被注入 `data-page-node-id` 属性 → **先 commit 再 preview**；已污染的用 `git checkout -- <file>` 还原（校验方式：`git diff` 的新增行应 100% 是 `data-page-node-id`）。
- `artifacts/` 已 gitignore，适合放日志/模板/一次脚本；证据日志建议保留（报告里要引用）。
- **行尾必须 LF**：`.gitattributes` 为 `* text=auto eol=lf` 且 `core.safecrlf=true`，带 CRLF 的文件在 `git add` 阶段**直接 fatal**（`fatal: CRLF would be replaced by LF in <file>`）。自查 `grep -qU $'\r' <file>`，必要时 `sed -i 's/\r$//' <file>`。
  **已知源头（实测确认）**：`skill-creator` 的 `init_skill.py` 在 Windows 上生成的 4 个文件（`SKILL.md` / `example.py` / `api_reference.md` / `example_asset.txt`）**全部是 CRLF**（`write_text()` 默认把 `\n` 按 `os.linesep` 转换）；同一批次里由 Write 工具产出的文件是 LF。→ **用脚手架生成技能后，入库前必须先做行尾转换。**
  两份镜像副本要求逐字一致 → 转换须**两侧同做**。

## 7. 技能发现路径（各 harness 从哪里读它）

本技能有两份副本，必须逐字一致：

| 副本 | 路径 | 谁读它 |
|---|---|---|
| WorkBuddy 用户级 | `C:\Users\milo\.workbuddy\skills\pulsar-ui-runtime-verification\` | WorkBuddy（跨项目可用） |
| 仓库共享 | `<repo>\.agents\skills\pulsar-ui-runtime-verification\` | **OpenCode** 等读 `.agents/skills` 的 harness；入库随仓库分发 |

- **OpenCode 直接读 `.agents/skills/`**（官方文档 Agent Skills：「Project agent-compatible」路径，与 `.opencode/skills/`、`.claude/skills/` 并列；项目级从 cwd 逐级向上扫到 git worktree 根，沿途全部收集，同名以项目级覆盖全局级）。
  → **不需要**再往 `.opencode/skills/` 镜像第三份。该目录里现存的 `openspec-*` / `session-journal` 是历史遗留（早期镜像习惯），不必跟风复制；新增技能没有"三处同步"的义务。
- 若将来要供 Claude Code 使用，`~/.claude/skills/` 与 `<repo>/.claude/skills/` 是同族的 compatible 路径（**未实测**，需自行验证后再宣称支持）。
- 入口指针：`AGENTS.md` §4 任务路由表有一行指向本技能——新增技能后记得同类登记，否则 always-on 的 AGENTS.md 里没有发现它的线索。
- 校验：`python <skill-creator>/scripts/package_skill.py <skill-dir> <out-dir>` → 期望 `Skill is valid`；改完正文要重跑（并重建 zip）。
