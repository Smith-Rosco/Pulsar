# Pulsar v1.11.0 — 办公自动化工作台正式版：自动更新 + 安装包 + 用户手册

**发布日期**：2026-09-06
**定位语**：Pulsar —— 重度办公效率工作台：一键跑宏、安全填表登录、快速切换窗口。

## ✨ 亮点

- **应用内自动更新** — 三级容灾检查（GitHub API → Atom 订阅 → 302 重定向探测），官方源故障时自动切换镜像，SHA256 完整性校验；设置 → 关于 页可手动检查、下载、安装。
- **安装包 + 便携版双形态** — Inno Setup 安装器（Program Files、开始菜单、可选开机自启、卸载保留用户数据）+ 自包含单文件便携版（75.9MB，无需 .NET 运行时）。
- **中英双语用户手册** — 安装首启 / 跑宏 / 登旧系统 / 切窗口 / FAQ 五章，含 SmartScreen 处理、管理员权限、卸载数据保留说明；About 页与首启引导均有入口。
- **级联子菜单** — Fan（≤3 项翼展）与 Ring（完整子环）双形态，按槽位类型智能注入默认子动作；二级动作编辑器。
- **单实例互斥** — 命名 Mutex 防止多开，第二实例自动激活已有窗口。

## 📦 下载

| 资产 | 说明 | 适用于 |
|---|---|---|
| `Pulsar-v1.11.0-Setup.exe` | 安装版（含自动更新、开机自启选项） | 普通用户推荐 |
| `Pulsar-v1.11.0-Standalone-win-x64.zip` | 便携版（自包含单文件，免安装） | 受限环境 / U 盘携带 |
| `SHA256SUMS.txt` | 全部资产校验和 | 校验完整性 |
| 用户手册（中/英） | `Docs/manual/` | 新手上手 |

> **注意**：首次运行安装版可能触发 SmartScreen 提示（未签名代码），点击「更多信息」→「仍要运行」即可。

## 🖥️ 系统要求

- Windows 10 1903+ / Windows 11（x64）
- 安装版与便携版均为**自包含**，无需单独安装 .NET 8 运行时
- 可选：Excel 或 WPS Office（宏自动化）、Chromium 内核浏览器（网页脚本）

## 📝 本版变更

### 新增
- **应用内自动更新**：`UpdateCheckService`（三级容灾）+ `UpdateDownloadService`（官方/镜像故障转移、SHA256/大小/未验证三档校验）+ `UpdateOrchestrator`（状态编排、托盘通知、安装移交）；About 页新增软件更新区域（当前版本、三态徽标、手动检查、下载进度、安装按钮）。ADR-025。
- **安装与分发**：`dev.ps1 publish` 子命令（Standalone zip → Setup.exe → SHA256SUMS）；`scripts/installer/pulsar.iss`（autopf 安装、可选自启、卸载保留 `%AppData%\Pulsar`）。ADR-026（Inno vs WiX 选型 + 禁裁剪）。
- **用户手册**：`Docs/manual/` 中英双语五章 + `Docs/ops/RELEASE_CHECKLIST.md`（发布门槛与「暂停等确认」纪律）。
- **单实例互斥**：`Local\Pulsar-SingleInstance` 命名 Mutex + 已有窗口激活（SetForegroundWindow/ShowWindow）；UI Debug 模式跳过以允许 E2E 多实例。
- **级联子菜单**：`PluginSlot.SubActions` / `CascadeSubMenuDescriptor`；Fan（≤3 项、翼角约束）与 Ring 双布局；二级动作编辑器 `SubSlotEditorRow`；智能默认子动作注入。
- **多形态径向渲染器**：`IRadialRenderer` 契约 + Default / ClassicRing / Glassmorphism 三套内置渲染器；3 套主题预设（MatchaForest / GlacialIce / MorandiMuted）。
- **自定义图标库**：SVG 路径数据支持 + `CustomIconStore` 持久化 + 图标选择器导入。
- **手势外甩取消**：光标超出轮盘半径 ×1.5 即虚化取消（仅右键手势）。
- **手势进程隔离**：白名单/黑名单双模态，旁路桌面与任务栏。
- **配置备份与恢复**：含密码保护的秘密项导出/导入。

### 改进
- **产品重定位**：README 中英首屏由「生产力启动器」改为「重度办公效率工作台」，三支柱（一键宏 / 老旧网页脚本 / 安全填表登录）前置；4 张真实截图替换占位；内置插件显示身份叙事对齐（VbaRunner→一键跑宏、PkiPlugin→DPAPI 加密保护、BookmarkletRunner→老旧系统助手）。
- **设置页 UI 精简**：保存按钮改为图标+文字；4 处长文本描述精简，详情移入 Tooltip。
- **插件运行时架构深化**：宽门面拆分为三个窄 seam（`IPluginRegistry` / `IPluginExecutor` / `IPluginRuntimeOps`，ADR-012）；熔断策略去 UI/遥测依赖为纯状态机（ADR-013）；可回收 ALC 卸载不变量收口；插件清单解析单一事实来源；插件卡片能力声明进 metadata。
- **右键手势编排迁入 MenuSession**（ADR-020）：RadialMenuViewModel 收缩为薄转发，ADR-008 决策 2 收尾。
- **0 警告构建基线**：全部 32 条基线编译器警告清零；MenuSession Debug 跟踪日志移除。
- **`scripts/dev.ps1`**：build/test/commit/all 子命令，自动修补沙箱 shell 剥离的 Windows 环境变量。

### 修复
- **级联子菜单执行解析**：`ExecuteSelectionAsync` 由只查根 Slots 改为 `ResolveActiveSlotSource()`，子轮盘态选中子槽位不再误执行根槽。
- **级联子菜单状态残留**：`IsVisible=false` 分支统一清理子菜单态，下次唤起不再出现旧子槽位。
- **级联子菜单 Fan 方向角**：`ComputeChildPositions` Fan 分支翼角漏加父方向角，子项不随父槽位展开——已修复并新增 6 个回归测试。
- **Ring 命中统一为极坐标扇区触发**：删除窄环带下界，与父轮盘及 Fan 同构。
- **动态标题 cascade 期间抑制**：消除子环与固定标题带的视觉重叠。
- **插件运行时安装后不激活**：安装成功后先刷新发现、再授权、再立即激活。
- **运行中卸载/覆盖安装失败**：完整停用链（含 ALC 卸载 + GC 回收）后删文件，自动清理残骸目录。
- **重启后外部插件贡献消失**：启动协调器在延迟发现后立即激活所有已启用外部插件。
- **HotkeyService 陈旧配置覆盖**：`SettingsViewModel.Save()` 改用 `RebuildCache()` 刷新 `_config`。
- **Fluent accent token 未解析**：`ThemeService.ApplyAccent` 桥接 `UiApplication.Current.Resources` → `Application.Current.Resources`；按钮文字用 `TextOnAccentFillColorPrimaryBrush`。

## 🔐 安全与隐私说明

- 凭据使用 Windows DPAPI 加密存储，绑定当前用户，不写入日志。
- 自动更新仅从 GitHub 官方源及内置镜像表下载，校验 SHA256 后才执行安装。
- 遥测：OpenTelemetry 本地收集，不向第三方服务器发送数据。

## ⬆️ 升级与反馈

- 从 v1.10.0 升级：直接运行安装版覆盖安装，配置（`%AppData%\Pulsar\Profiles.json`）自动保留；便携版直接替换 exe。
- 问题反馈：[GitHub Issues](https://github.com/Smith-Rosco/Pulsar/issues)

## ⚠️ 已知限制

- 安装包为**未签名**代码，首次运行触发 SmartScreen 提示（「更多信息」→「仍要运行」）。
- 本机无 Inno Setup 编译器（ISCC），Setup.exe 编译路径在发布机上验证。
- 应用内自动更新的真实网络 QA（断网路径、测试 tag 全链路）需在发布后真机验证。
- Demo 视频（3 支脚本已定稿）尚未录制，将在后续版本补充到 Release 页。

---

**校验清单（发布前勾选，发布后删除本段）**
- [ ] 版本号 1.11.0 与 tag、安装器内版本、csproj 一致
- [ ] 双形态产物 + SHA256SUMS 全部上传
- [ ] 用户手册链接可达（中英）
- [ ] README 首屏截图与本版 UI 一致
- [ ] 叙事自查：无「启动器」自居；老旧系统叙事未窄化
