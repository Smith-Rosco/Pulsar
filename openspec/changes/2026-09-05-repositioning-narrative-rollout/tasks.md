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
- [ ] 3.2 录制 + 剪辑（录屏即可），输出 mp4（与 E2E recording.mp4 管线兼容）
- [ ] 3.3 视频与脚本入库（`Docs/media/release/videos/`）

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

- [ ] 6.1 **VbaModuleInjector 未剥离 Attribute VB_Name 行**（实测确认）：VBE 导出的 .bas 首行 `Attribute VB_Name = "..."` 经 CodeModule.AddFromString 注入后，模块无法完整编译，Application.Run 报「宏不可用」。规避：演示 .bas 已去 Attribute 行（可编译）；治本：VbaModuleInjector.InjectModule 在 AddFromString 前剥离 Attribute 行（按行过滤，保留其余内容）。
- [ ] 6.2 **槽位参数不展开环境变量**：全库无 ExpandEnvironmentVariables 处理槽位参数（仅 SettingsViewModel UI 层有）。用户配置 %USERPROFILE%/%APPDATA% 路径会静默失效。规避：demo-profile.ps1 注入时展开为绝对路径；治本：插件执行前统一展开（参数解析层/ExecutablePathResolver）。
- [ ] 6.3 **Pulsar.Simulator 参数解析缺陷**：CommandLineParser 短名前缀匹配把 -args 解析为 -a rgs（与 -action 冲突报 multiple times）、-plugin 解析为 -p lugin（报 Plugin not found）；且 Plugins 目录缺失时扩展插件（bookmarklet/vbarunner）不可加载测试。规避：用 -g/--args 传参、仅测内置插件；治本：文档注明参数写法或改选项名避免前缀歧义。
- [ ] 6.4 **vbarunner 对无 VBA 引擎的 WPS 无优雅降级**：WPS 未装 VBA 组件时 VBProject 返回空壳对象（Name 空、VBComponents.Count=0、Add 抛 null 引用），用户看到「宏没反应」而非可读错误。治本：检测空壳并返回「WPS 未安装 VBA 组件（vbeapi.dll 仅接口存根），请安装 VBA for WPS」可读错误。
- [ ] 6.5 **WPS 信任开关与 VBA 组件两级前置**：HKCU\Software\Kingsoft\Office\6.0\et\Application Settings\KDEVBProjectTrust（默认 0，信任 VBA 工程对象模型访问）与 VBA 组件安装缺一不可；VbaRunner.md 应补充 WPS 前置检测与引导。
- [ ] 6.6 **演示凭据注入已自动化**（本次完成，记录约定）：demo-profile.ps1 switch 自动生成 DPAPI 凭据（ProtectedData CurrentUser，与 SecretRepository 解密一致）写入 secrets.json 并回填 Slot3 secretId；restore 同时恢复 Profiles.json 与 secrets.json。后续 PkiPlugin 若支持导入/CLI 添加凭据，可替代该注入。- [ ] 6.7 **SlotOrb.RefreshIcon 未对图标 key 做 NormalizeIconKey**（实测确认，文本图标根因）：径向菜单把配置 icon 字符串原样传入 SlotOrb → GetGlyph 只认 hex 码位，**名称形式（如 ReportDocument）被原样当文本渲染**，码位形式（E9F9）才渲染字形。设置页 ResolveIconDisplay 对名称同样 fallback 原样。规避（已落地）：宣传 fixture 全部改用码位（commandMode）与 exe 路径（switchMode，走 ExtractExeIcon 显示程序真图标）；治本：RefreshIcon/GetGlyph 对非路径 key 先 NormalizeIconKey（名称→码位→字形），并对 ResolveIconDisplay 补名称反查。
- [ ] 6.8 **本机环境变更**（2026-09-07 记录，供后续引用）：Microsoft Office 16.0 已安装（EXCEL.EXE 在 Office16），WPS 已卸载（App Paths 清除、仅 Kingsoft 残留 backup）。Excel COM + VBProject + AccessVBOM=1 实测可用，vbarunner 宏注入链（Add→AddFromString→Run→Remove）端到端验证通过（.bas 无 Attribute 行）。VBA 阻塞解除，视频一按真实宏执行录制；分镜 S4 已由「WPS 表格」改为「新开 Excel 窗口」。