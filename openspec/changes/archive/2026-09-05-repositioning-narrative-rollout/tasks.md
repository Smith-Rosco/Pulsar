# Tasks — Repositioning Narrative Rollout

## 1. 插件显示名叙事对齐（先做：README 截图需用到新名称）

- [x] 1.1 BookmarkletRunner `GetMetadata()`：DisplayName/Description 改为叙事对齐口径（网页脚本 / 老旧系统助手方向），Id 与配置键不动（DisplayName "Web Scripts" 与 resx 键绑定保持稳定；描述补「把老旧企业系统的重复点击变成一键动作」叙事；zh 侧原本已是叙事口径，保留）
- [x] 1.2 `Strings.resx` / `Strings.zh-CN.resx` 同步插件相关标签；复核无硬编码用户可见字符串（`ILocalizationService` 纪律）（EN/zh 三插件描述同步：WebScripts/ExcelMacros/AutoFill；PkiPlugin `fill` 动作描述同步安全口径）
- [x] 1.3 复核 WinSwitcher / VbaRunner / PkiPlugin / Command / SystemCommand 的描述是否符合三支柱叙事（只调措辞，不改语义）（VbaRunner 描述升「一键跑宏」自动化领衔；PkiPlugin 升「DPAPI 保护、可编程登录动作」凭据护城河（DPAPI 已核实 `CredentialsManager.cs` ProtectedData）；WinSwitcher/Command/SystemCommand 维持窗口打底/后台定位措辞不动）
- [x] 1.4 全仓扫描旧显示名引用（`Docs/`、`PLUGIN_DEVELOPMENT.md`、`AGENTS.md` 等）并同步（`Docs/Plugins/PkiPlugin.md` 标题 Secret Fill → AutoFill Plugin (Secret Fill) + 补 Display Name 行；`Docs/Plugins/VbaRunner.md` 概述补显示名与一键跑宏口径；`Docs/Plugins/BookmarkletRunner.md` 已有对齐无需改；archive 历史文档按 append-only 原则不改写）
- [x] 1.5 补/更新 `plugin-display-identity` 相关测试；`scripts/dev.ps1 build` + `test` 全绿（新增 `Pulsar.Tests/Plugins/BuiltInPluginDisplayIdentityTests.cs` 12 用例：显示名→resx 键约定绑定、zh 叙事关键词、Id 稳定、EN C#↔resx 同步；`PluginManagerViewModelLocalizationTests` 样例描述更新；**实际验证走 bash + env 前缀 dotnet（dev.ps1 在 WorkBuddy 下有 127 退出码问题）：全量 1143/1143 通过，0 错误**；构建存在 4 条存量 CS8604 警告（MenuSession.cs:891/894，stash 验证干净树同样存在，随 3f68c13 引入，与本变更无关））

## 2. 宣传截图（E2E 管线）

- [x] 2.1 新增宣传用 E2E 工作流（fixture 注入真实感配置：办公场景 slot + 级联 + 渲染器预设），Dark 主题 1920×1080（fixture `office-workbench-dark.json`：8 commandMode + 6 switchMode + Fan 级联 + MatchaForest 预设；workflow `promo-release-screens.json`：34 步含 set-invocation-point 居中 + 5 张截图；E2E 基建新增 move-cursor 步骤（SetCursorPos P/Invoke）与 DebugCommandServer `set-invocation-point` 命令；纯黑壁纸 + MinimizeAll 一体化脚本 `run-promo-wallpaper.ps1` 保证干净背景）
- [x] 2.2 产出首屏用图：主界面全景 / 轮盘呼出 / Excel 跑宏瞬间 / 窗口切换子菜单 ≥4 张（promo-release-9 PASS 30.7s，菜单像素质心 (1260,734) 确认居中；4 张 1920×1080 PNG 已入 `Docs/media/release/`：01-main-interface / 02-radial-summoned / 03-excel-macro-moment / 04-window-switch-menu）
- [x] 2.3 确定发布资产目录约定（如 `Docs/media/release/`）并入 git；确认与「UI 验证截图 never commit」纪律的边界并写入该目录 README（`.gitignore` 从 `Docs/media/` 改为 `Docs/media/*` + `!Docs/media/release/` 例外；README 明确三方边界：release/ 入 git / Docs/media/ 其他子目录忽略 / E2E artifacts/ 不入 git；含命名约定、再生步骤、review checklist）

## 3. Demo 视频

- [x] 3.1 三支脚本定稿（Excel 跑宏 / 老旧网页脚本注入 / 登录填表自动化；各 30–60s，分镜 + 台词）
- [x] ~~3.2 录制 + 剪辑（录屏即可），输出 mp4（与 E2E recording.mp4 管线兼容）~~ **已取消（2026-09-08 用户裁决）**：现有动图已足够，不再录制视频；3.1 的三支脚本文档保留备查。
- [x] ~~3.3 视频与脚本入库（`Docs/media/release/videos/`）~~ **随 3.2 一并取消**（同上裁决）。

## 4. README 重写

- [x] 4.1 `README.md`（zh）首屏：定位语 + 三支柱场景 + 真实截图 + 与竞品差异化一句话（vs Quicker/Flow 话术取自重定位方案 §1.2）
- [x] 4.2 `README_EN.md` 同步重写，中英叙事一致
- [x] 4.3 自查：全文不再以「启动器」自居；「老旧系统」叙事不窄化（§9 反噬风险条款）；AI 愿景一句话 + roadmap 标注

## 5. 发布说明模板 & 验证

- [x] 5.1 建立 `RELEASE_NOTES` 模板（版本、亮点、下载、系统要求占位）供 Change 5 使用（仓库根 `RELEASE_NOTES.md`，含校验清单与叙事自查项）
- [x] 5.2 `scripts/dev.ps1 build` 0 警告 0 错误；`scripts/dev.ps1 test` 全量通过（WorkBuddy 下 dev.ps1 退出码 127 问题复现，改走 bash + env 前缀 dotnet 等效验证：build 0 警告 0 错误、全量 1143/1143 通过；存量 CS8604 ×4 已修复，见 journal 16:47）
- [x] 5.3 journal 记录 + （如显示名语义有变）ADR 记录

## 6. 程序设计级发现（录制前实测暴露；后续修复方向，不在本次变更范围）

> 来源：2026-09-07 为 3.2 录制做配置预跑验证时实测发现。逐条已核实现象与根因。

- [x] 6.1 **VbaModuleInjector 未剥离 Attribute VB_Name 行**（实测确认）：…治本：VbaModuleInjector.InjectModule 在 AddFromString 前剥离 Attribute 行（按行过滤，保留其余内容）。
  - **治本已落地**：`VbaModuleInjector.InjectModule` 在 `AddFromString` 前用正则 `(?m)^\s*Attribute\s+VB_\w+\s*=.*$` 剥离属性行（保留其余内容，含空白行结构）。
- [x] 6.2 **槽位参数不展开环境变量**：…治本：插件执行前统一展开（参数解析层/ExecutablePathResolver）。
  - **治本已落地（2026-09-08）**：`ExecutablePathResolver.Resolve` 对 launchPath 先 `Environment.ExpandEnvironmentVariables`；`PluginRuntimeKernel.ExecuteAsync` 门面对 args 值统一展开（`ExpandEnvironmentVariablesInArgs`，无变化则返回原字典，保留引用相等）。未定义变量保持原样。
- [x] 6.3 **Pulsar.Simulator 参数解析缺陷**：…治本：文档注明参数写法或改选项名避免前缀歧义。
  - **治本已落地**：args 选项短名由 `-a` 改为 `-g`（`[Option('g', "args")]`），现短名集合 `-p/-a/-g/-c/-l` 两两不互为前缀，前缀匹配歧义消除。
- [x] 6.4 **vbarunner 对无 VBA 引擎的 WPS 无优雅降级**：…治本：检测空壳并返回可读错误。
  - **治本已落地（2026-09-08）**：`VbaModuleInjector.EnsureVbaProjectIsUsable` 在注入前探测空壳（`VBProject` 为 null / `VBComponents` 为 null / `Count == 0`）→ 抛出含「安装 VBA for WPS + 开启信任」指引的 `InvalidOperationException`；探测异常一律保守判定为可用，不误伤正常路径。
- [x] 6.5 **WPS 信任开关与 VBA 组件两级前置**：…VbaRunner.md 应补充 WPS 前置检测与引导。
  - **已补充（2026-09-08）**：`Docs/plugins/VbaRunner.md` 新增「WPS 前置检测（两级前置，缺一不可）」章节（VBA 组件安装 + `KDEVBProjectTrust=1`，Excel 侧 `AccessVBOM=1`），并在「故障排除」加入空壳错误的定位入口。
- [x] 6.6 **演示凭据注入已自动化**（本次完成，记录约定）：…后续 PkiPlugin 若支持导入/CLI 添加凭据，可替代该注入。
- [x] 6.7 **SlotOrb.RefreshIcon 未对图标 key 做 NormalizeIconKey**（实测确认，文本图标根因）：…治本：RefreshIcon/GetGlyph 对非路径 key 先 NormalizeIconKey（名称→码位→字形），并对 ResolveIconDisplay 补名称反查。
  - **治本已落地（2026-09-08）**：`SlotOrb.RefreshIcon` 对非路径 key 先 `IconHelper.NormalizeIconKey`（名称 → 码位）再取字形，归一化无结果时回退原 key（PUA 字符/emoji 不受影响）；`IconHelper.ResolveIconDisplay` 新增 `_byName` 反查，名称形式显示 `Code · Name` 而不是原始字符串。全量测试 1303/1303 通过。
- [x] 6.8 **本机环境变更**（2026-09-07 记录，供后续引用）：…分镜 S4 已由「WPS 表格」改为「新开 Excel 窗口」。（记录项，无需代码动作；VBA 阻塞已解除）