## Context

设置窗口已是 `ui:NavigationView` + `Frame` 结构：导航项在 `SettingsWindow.BuildNavigationItems()`（L104-154）中遍历 `SettingsPageCatalog.Pages` 硬编码数组动态生成，页面按 id 缓存于 `_pages` 字典，导航经 `SettingsShellViewModel.NavigateAsync` + `SettingsNavigationGuard` 拦截。持久化走 `SettingsEditorSession` 单写入，脏状态（`HasUnsavedChanges`）是**窗口级全局**标志（`MarkDirty()` 汇总），不是按页独立。右键手势配置以 CardExpander 形式嵌在 General 页（约 30+ 控件）；外观仅 4 行也在 General 页。`OnLanguageChanged`（L464-475）会整体重建导航项。

调研结论与设计裁决（grill 审计已通过）见 `Docs/planning/2026-09-08-dynamic-settings-tabs-research.md`。动机见 proposal.md。

## Goals / Non-Goals

**Goals:**
- 临时页机制：注册 → 侧边栏动态增删 → 按类型单例 → 干净页导航离开自动回收。
- P0：机制 + 手势配置迁出 General 页为临时页 `gesture`。
- P1：外观迁出为独立**常驻**页，General 页瘦身。

**Non-Goals:**
- 不做钉住/晋升常驻、Tab 级脏红点、深链入口（API 预留）、插件设置/进程黑名单迁移（P2）、槽位编辑器迁移（P3）。
- 不改 `Profiles.json` 格式，不动 `SettingsEditorSession` 单写入路径。
- 不替换 `ui:NavigationView`，不引入新窗口。

## Decisions

### D1 — 注册表化：单一目录可变，而非双集合
`SettingsPageCatalog.Pages` 从静态数组改为可观察集合（或暴露变更事件），`SettingsPageRegistration` 增加 `bool IsTransient`；临时页与常驻页共用同一注册源与工厂。
*备选*：独立的 TransientPageCatalog。放弃原因：`settings-shell-navigation` spec 要求"单一注册源"，双集合会造成 id 查重、语言重建、恢复逻辑三处分叉。

### D2 — 工厂去 switch：按 id 注册的字典
`SettingsPageFactory` 从 `switch(PageType)` 改为 `Dictionary<string, Func<Page>>`，目录注册时一并注册构造委托。常驻 5 页随改造一并迁移，消除新增页的双点维护。

### D3 — 回收判定用全局脏标志，而非按页脏状态
临时页"干净"= `HasUnsavedChanges == false`（全局）。脏是窗口级共享草稿的属性，按页拆分脏状态需要为每个页面建立独立草稿合并机制，P0 收益不抵复杂度。副作用（在常驻页有编辑时临时页也视为脏、不回收）是保守方向——多保留一个 Tab，绝不丢数据。
*备选*：per-page dirty + 草稿分片。推迟到 P2 迁移插件设置（多实例并存）时再评估。

### D4 — 脏临时页豁免导航提示（spec 冲突裁决点）
既有 `settings-dirty-state-guard` spec 规定"带未保存修改切换页面时提示 save/discard/cancel"。本变更将该要求修改为：**仅常驻页**导航离开提示；从脏临时页离开不提示（页保留、Tab 留存、关窗时经既有 `CanCloseAsync` 统一提示）——即 VS Code 的"脏文件不阻塞切换编辑器、关窗才拦截"语义。此为审计通过的决策，delta 中显式 MODIFIED，非静默覆盖。

### D5 — 单例与多开问题的统一解
再次触发已打开的临时页类型 = 激活既有实例（`NavigateAsync` 到既有缓存页），天然覆盖"用户跳去别的 Tab 干活后又触发同一配置"的场景；不引入"每个触发源一个实例"的规则。实体编辑类（`<type>:<entityId>`）语义推迟到 P3。

### D6 — P0 不引入通用页级生命周期接口
页面实例随打开构建、随回收销毁，状态天然重置；手势页无异步初始化需求。`ISettingsPageLifecycle`（OnNavigatedTo/From）推迟到 P2 出现真实需求再引入，避免空转抽象。

### D7 — 手势临时页信息架构：单页多分区
`SettingsGesturePage`：总开关 → 召唤模式/阈值 → Switcher/Action 修饰键 → 手势隔离过滤（Allowlist/Blocklist/全屏拦截），分区顺序沿用现卡片，冲突警告 DataTrigger 迁入。不拆多 Tab。
手势卡片瘦身后保留：总开关 ToggleSwitch + 一行状态摘要（如"右键拖拽召唤 · 阈值 40px"）+ "配置详情"按钮（触发 `OpenTransientPage("gesture")`）。

### D8 — 外观常驻页内容与归属切分
`SettingsAppearancePage`（常驻，System 组）：主题、渲染器风格、主题预设（升级为网格/带预览色块的选择，仍不做实时渲染预览）。语言选择：审计时定留 General，2026-09-08 用户冒烟后改判**移入外观页主题卡**（覆盖 Q15，用户决策优先）。General 页保留：轮盘布局、全局热键、缓存管理、日志 + 外观摘要行。

### D9 — 视觉区分与导航项模板
临时项标题斜体（`FontStyle=Italic`，沿用主题前景色）+ hover 关闭钮（`NavigationViewItem` 自定义 Header ContentTemplate 内嵌 Button）。现有 `NavIndicator`（Canvas 自绘指示条）需在集合变更时重算位置——在导航项集合变更回调中触发一次布局更新。

### D10 — 语言重建保留临时项
`OnLanguageChanged` 重建改为从目录（含临时注册）取数据源重建，重建前记录当前选中 id、重建后恢复。临时项与常驻项共用标题 key 约定：`SettingsPage.{Id}.Title`（resx 双语各加一条）。

### D11 — 临时页 id 与服务接缝
`ITransientPageService`：`OpenTransientPage(string typeId)` / `CloseTransientPage(string typeId)`；由组合根注册，实现持有目录与 ShellViewModel。本期注册的 id：`gesture`。P2 预留：`plugin-settings:<pluginId>`、`process-blacklist`。服务内部不做 UI 假设（不持有 Window 引用之外的状态），便于测试。

## Risks / Trade-offs

- [NavigationView 自定义 Header 模板与 `OnLanguageChanged` 重建、NavIndicator 自绘逻辑三方耦合] → 临时项增删/重建统一走一个 `RebuildNavigationItems()` 私有方法，语言重建与临时项增删共用；针对重建保留行为写 UI 单元测试。
- [全局脏标志语义可能让用户困惑（在 General 页的编辑让 gesture Tab 不回收）] → 保守方向（多保留不丢数据）；关窗提示兜底；D3 记录了演进路径。
- [手势配置迁移造成的回归（阈值/隔离过滤/冲突警告行为）] → 迁移是纯绑定搬迁，不改数据模型（`ProfileSettings` 不动）；按既有 XAML 逐控件对照迁移，现有绑定路径不变。
- [Frame 页面缓存泄漏] → 回收时从 `_pages` 移除实例；窗口 `OnClosed` 清理逻辑保持不变。

## Migration Plan

1. P0：D1/D2/D5/D9/D10/D11 机制 + D7 手势页迁移（一个 change，分 task 提交，机制先行、迁移随后）。
2. P1：D8 外观常驻页 + General 瘦身。
3. 文档：`CHANGELOG.md`、`Docs/decisions/022-settings-transient-pages.md`、`Docs/architecture/DIALOG_SYSTEM.md` 与 `AGENTS.md` 导航范式更新（规则文件，commit 前按约定暂停等用户确认）。
4. 回滚：单 commit revert 即可，无数据迁移、无配置格式变更。

## Open Questions

无阻塞性开放问题。P2（插件设置/黑名单迁移）与 P3（槽位编辑器）的多实例 id 语义、按页脏状态拆分，留待对应 change 立项时裁决。
