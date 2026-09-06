# Tasks — Repositioning Narrative Rollout

## 1. 插件显示名叙事对齐（先做：README 截图需用到新名称）

- [x] 1.1 BookmarkletRunner `GetMetadata()`：DisplayName/Description 改为叙事对齐口径（网页脚本 / 老旧系统助手方向），Id 与配置键不动（DisplayName "Web Scripts" 与 resx 键绑定保持稳定；描述补「把老旧企业系统的重复点击变成一键动作」叙事；zh 侧原本已是叙事口径，保留）
- [x] 1.2 `Strings.resx` / `Strings.zh-CN.resx` 同步插件相关标签；复核无硬编码用户可见字符串（`ILocalizationService` 纪律）（EN/zh 三插件描述同步：WebScripts/ExcelMacros/AutoFill；PkiPlugin `fill` 动作描述同步安全口径）
- [x] 1.3 复核 WinSwitcher / VbaRunner / PkiPlugin / Command / SystemCommand 的描述是否符合三支柱叙事（只调措辞，不改语义）（VbaRunner 描述升「一键跑宏」自动化领衔；PkiPlugin 升「DPAPI 保护、可编程登录动作」凭据护城河（DPAPI 已核实 `CredentialsManager.cs` ProtectedData）；WinSwitcher/Command/SystemCommand 维持窗口打底/后台定位措辞不动）
- [x] 1.4 全仓扫描旧显示名引用（`Docs/`、`PLUGIN_DEVELOPMENT.md`、`AGENTS.md` 等）并同步（`Docs/Plugins/PkiPlugin.md` 标题 Secret Fill → AutoFill Plugin (Secret Fill) + 补 Display Name 行；`Docs/Plugins/VbaRunner.md` 概述补显示名与一键跑宏口径；`Docs/Plugins/BookmarkletRunner.md` 已有对齐无需改；archive 历史文档按 append-only 原则不改写）
- [x] 1.5 补/更新 `plugin-display-identity` 相关测试；`scripts/dev.ps1 build` + `test` 全绿（新增 `Pulsar.Tests/Plugins/BuiltInPluginDisplayIdentityTests.cs` 12 用例：显示名→resx 键约定绑定、zh 叙事关键词、Id 稳定、EN C#↔resx 同步；`PluginManagerViewModelLocalizationTests` 样例描述更新；**实际验证走 bash + env 前缀 dotnet（dev.ps1 在 WorkBuddy 下有 127 退出码问题）：全量 1143/1143 通过，0 错误**；构建存在 4 条存量 CS8604 警告（MenuSession.cs:891/894，stash 验证干净树同样存在，随 3f68c13 引入，与本变更无关））

## 2. 宣传截图（E2E 管线）

- [ ] 2.1 新增宣传用 E2E 工作流（fixture 注入真实感配置：办公场景 slot + 级联 + 渲染器预设），Dark 主题 1920×1080
- [ ] 2.2 产出首屏用图：主界面全景 / 轮盘呼出 / Excel 跑宏瞬间 / 窗口切换子菜单 ≥4 张
- [ ] 2.3 确定发布资产目录约定（如 `Docs/media/release/`）并入 git；确认与「UI 验证截图 never commit」纪律的边界并写入该目录 README

## 3. Demo 视频

- [ ] 3.1 三支脚本定稿（Excel 跑宏 / 老旧网页脚本注入 / 登录填表自动化；各 30–60s，分镜 + 台词）
- [ ] 3.2 录制 + 剪辑（录屏即可），输出 mp4（与 E2E recording.mp4 管线兼容）
- [ ] 3.3 视频与脚本入库（`Docs/media/release/videos/`）

## 4. README 重写

- [ ] 4.1 `README.md`（zh）首屏：定位语 + 三支柱场景 + 真实截图 + 与竞品差异化一句话（vs Quicker/Flow 话术取自重定位方案 §1.2）
- [ ] 4.2 `README_EN.md` 同步重写，中英叙事一致
- [ ] 4.3 自查：全文不再以「启动器」自居；「老旧系统」叙事不窄化（§9 反噬风险条款）；AI 愿景一句话 + roadmap 标注

## 5. 发布说明模板 & 验证

- [x] 5.1 建立 `RELEASE_NOTES` 模板（版本、亮点、下载、系统要求占位）供 Change 5 使用（仓库根 `RELEASE_NOTES.md`，含校验清单与叙事自查项）
- [x] 5.2 `scripts/dev.ps1 build` 0 警告 0 错误；`scripts/dev.ps1 test` 全量通过（WorkBuddy 下 dev.ps1 退出码 127 问题复现，改走 bash + env 前缀 dotnet 等效验证：build 0 警告 0 错误、全量 1143/1143 通过；存量 CS8604 ×4 已修复，见 journal 16:47）
- [ ] 5.3 journal 记录 + （如显示名语义有变）ADR 记录
