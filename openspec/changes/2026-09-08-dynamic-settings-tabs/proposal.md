## Why

General 页的右键手势配置（约 30+ 控件、7 层嵌套的 CardExpander）已超出卡片的表达力，重配置走模态框会打断工作流，外观配置则过于简单。设置窗口侧边栏尚有容量：引入"动态标签页"（临时页）机制，让重配置按需进入侧边栏、用完即回收，既保住工作区大小，又不引入新窗口。

## What Changes

- 设置页目录（`SettingsPageCatalog`）从静态数组改为可变注册表，`SettingsPageRegistration` 增加 `IsTransient` 标记；`SettingsPageFactory` 从 `switch` 改为按 id 注册的工厂字典。
- 新增 `ITransientPageService`（Register/Open/Close）：打开临时页时在侧边栏语义分组末尾追加导航项（斜体标题 + hover 关闭钮）；再次触发同一类型 → 激活既有实例而非新建（按类型单例，Tab 数量有界）。
- 临时页回收策略：导航离开时若全局无未保存修改（`HasUnsavedChanges == false`）→ 自动回收（从侧边栏与页面缓存移除并销毁）；有未保存修改 → 页保留不回收。
- 脏临时页不阻塞跳转（VS Code 语义）：离开时不提示，页保持打开；关闭设置窗口时由既有 `CanCloseAsync` 统一提示保存/放弃。**常驻页之间的导航提示行为保持不变**。
- P0 迁移：右键手势配置从 General 页 CardExpander 迁出为独立临时页 `gesture`（单页多分区）；原卡片保留总开关 + 一行状态摘要 + "配置详情"按钮。
- P1 迁移：外观（主题/渲染器/主题预设网格）从 General 页迁出为独立**常驻**页；语言选择留在 General。
- 本期不做：钉住/晋升常驻、Tab 级脏指示红点、深链入口（`OpenTransientPage` API 天然预留）、插件设置/进程黑名单迁移（P2）、槽位编辑器迁移（P3）。

## Capabilities

### New Capabilities

- `settings-transient-pages`: 设置窗口临时页（动态标签页）生命周期——注册、按类型单例激活、干净页导航离开自动回收、脏页保留、视觉区分（斜体 + 关闭钮）、关闭交互（脏页关闭需确认）。

### Modified Capabilities

- `settings-shell-navigation`: 页面注册从"静态一次注册"扩展为"运行时可变注册"——侧边栏导航项随临时页注册/回收动态增删，且语言切换重建导航项时必须保留已打开的临时页项。
- `settings-dirty-state-guard`: 修改"导航离开未保存修改提示"要求——从**脏临时页**导航离开不再提示（页保留，关窗统一提示）；从**常驻页**导航离开的提示行为不变。

## Impact

- **代码**：`Pulsar/Pulsar/Services/SettingsPageCatalog.cs`、`SettingsPageFactory.cs`、`Services/Interfaces/`（新增 `ITransientPageService`）、`Views/SettingsWindow.xaml(.cs)`（导航项动态增删 + 关闭钮 + 斜体样式 + `OnLanguageChanged` 重建保留临时项）、`ViewModels/Settings/SettingsShellViewModel.cs`（导航离开回收判定）、`Views/Pages/SettingsGeneralPage.xaml`（瘦身）、新增 `Views/Pages/SettingsGesturePage.xaml`、`SettingsAppearancePage.xaml` 及 VM。
- **文档（规则文件，提交前需用户确认）**：`Docs/architecture/DIALOG_SYSTEM.md`、`AGENTS.md` 设置导航范式更新；新增 `Docs/decisions/022-settings-transient-pages.md`。
- **不变**：`Profiles.json` 格式（无新持久化字段）；`SettingsEditorSession` 单写入路径；一次性选择器/确认类模态全部保留。
