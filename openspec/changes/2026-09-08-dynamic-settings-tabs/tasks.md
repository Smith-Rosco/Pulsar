## 1. 注册表化基座（P0 机制）

- [x] 1.1 `SettingsPageRegistration` 增加 `IsTransient`；`SettingsPageCatalog.Pages` 改为可观察集合并暴露注册/注销方法（常驻 5 页注册不变）。验证：`dotnet build Pulsar.sln` 0 错误；现有目录单元测试（如有）通过。
- [x] 1.2 `SettingsPageFactory` 从 `switch` 改为按 id 注册的 `Dictionary<string, Func<Page>>`，常驻 5 页随目录注册。验证：构建 0 错误 + 全量测试无回归（基线 1045/1045）。
- [x] 1.3 新增 `ITransientPageService`（`OpenTransientPage(string typeId)` / `CloseTransientPage(string typeId)`）接口与实现，组合根（`App.xaml.cs`）注册。验证：接口单测——对未注册 id 调用安全拒绝且当前页不变（对齐 settings-shell-navigation spec 场景）。

## 2. 侧边栏动态导航项（P0 机制）

- [ ] 2.1 `SettingsWindow.BuildNavigationItems()` 重构为从目录（含临时注册）统一重建；`RebuildNavigationItems()` 供语言切换与临时项增删共用，重建前记录选中 id、重建后恢复。验证：单测覆盖"语言切换保留已打开临时项"（settings-shell-navigation spec MODIFIED + ADDED 场景）。
- [ ] 2.2 临时项渲染：标题斜体 + hover 关闭钮（自定义 Header ContentTemplate），追加到语义分组末尾；`NavIndicator` 在集合变更后重算位置。验证：手动冒烟——打开/回收临时页，指示条位置正确；临时项与常驻项视觉可区分。
- [x] 2.3 `SettingsShellViewModel.NavigateAsync` 挂接回收判定：离开临时页且 `HasUnsavedChanges == false` → 注销注册 + 从 `_pages` 移除实例；脏 → 保留。验证：单测覆盖干净回收 / 脏保留 / 回收后再打开重建实例（settings-transient-pages spec 三个场景）。
- [x] 2.4 关闭钮交互：干净页直接回收；脏页弹确认（保存/放弃/取消，经 `SettingsDialogFlows.RunConfirmationAsync`）。验证：单测覆盖关闭确认三分支。

## 3. 脏守卫豁免（P0 机制）

- [x] 3.1 `SettingsNavigationGuard` 按来源页类型分流：来源为临时页 → 不提示直接放行（页保留）；来源为常驻页 → 现有 save/discard/cancel 提示不变。验证：单测覆盖两条路径（settings-dirty-state-guard spec 两个导航场景）；窗口关闭 `CanCloseAsync` 行为回归测试不破。

## 4. P0 迁移：手势临时页

- [x] 4.1 新建 `SettingsGesturePage.xaml(.cs)` + VM：按 design D7 分区迁移 `SettingsGeneralPage.xaml` L177-360 全部控件与绑定（`ProfileSettings` 数据模型不动），含冲突警告 DataTrigger 与条件可见性。验证：构建 0 错误；逐控件对照清单核对绑定路径一致。
- [x] 4.2 目录注册临时页 `gesture`（GroupId 归属现有分组、本地化 key `SettingsPage.Gesture.Title`，Strings.resx + Strings.zh-CN.resx 双语各加）。
- [ ] 4.3 手势卡片瘦身：保留总开关 + 一行状态摘要 + "配置详情"按钮（触发 `OpenTransientPage("gesture")`）；删除已迁出的 9 个 SettingsRow。验证：手动冒烟——卡片开关仍生效、按钮打开临时页、重复触发激活既有实例。
- [ ] 4.4 本地化：迁移控件的所有用户可见字符串走 `ILocalizationService` / `{lex:Locale}`，新增 key 双语补齐。验证：中英切换冒烟无裸字符串。

## 5. P1：外观常驻页

- [ ] 5.1 新建 `SettingsAppearancePage.xaml(.cs)`（常驻，System 组）：主题、渲染器风格、主题预设网格（带预览色块，无实时渲染预览）；语言留 General。验证：构建 0 错误 + 主题切换即时生效冒烟。
- [ ] 5.2 General 页外观 CardExpander 移除并加摘要行；目录注册外观页（本地化 key 双语补齐）。验证：中英切换冒烟；General 页无残留死绑定（grep `Appearance` 相关绑定）。

## 6. 测试与回归

- [x] 6.1 新增临时页生命周期测试组（单例/回收/重建/关闭确认/语言重建保留），覆盖 4 个 spec 文件全部场景。验证：`dotnet test` 全量通过且不低于当前基线数量。
- [ ] 6.2 全量回归 + 内存冒烟：反复打开/回收临时页 20 次后 `_pages` 无泄漏（窗口关闭 `TrimMemory` 行为不回归）。验证：`dotnet test` + 手动冒烟。

## 7. 文档沉淀（规则文件，commit 前按约定暂停等用户确认）

- [x] 7.1 新增 `Docs/decisions/022-settings-transient-pages.md`（记录 D1–D11 裁决与 spec 冲突处理）。
- [x] 7.2 更新 `Docs/architecture/DIALOG_SYSTEM.md` 与 `AGENTS.md`：设置导航新范式（重配置 = 临时页，选择器/确认留模态）。
- [ ] 7.3 `CHANGELOG.md` 据实更新（不编造测试数字）；`openspec validate` 通过、`/opsx-archive` 归档后本地 commit，push 由用户执行。

> **进度备注（2026-09-08）**：代码与单测全部完成（构建 0/0，全量 1303/1303，基线 1283 + 新增 20）。
> 未勾选项均剩 **GUI 手动冒烟**（临时页打开/回收/指示条位置/中英切换/内存 20 次开回收）与
> **归档提交**（7.3：`/opsx-archive` + 本地 commit——规则文件 DIALOG_SYSTEM.md / AGENTS.md 变更按约定待用户确认后提交）。

> **冒烟修复（2026-09-08 第 1 轮反馈，已修，1303/1303 回归通过）**：①关闭钮遮挡 → `HorizontalContentAlignment=Stretch` + 双列模板贴 Tab 最右；②自动回收后指示器不跟随 → 重建后 `UpdateLayout` + `InitializeNavIndicator` bounds 无效时 Loaded 重试 + 动画 early-return 兜底重定位；③语言设置移入外观页（覆盖审计 Q15，用户决策）；④外观页无法保存 → 预设 ListBox 绑定路径误用 `RelativeSource=Page`（应为 `DataContext.*`），已统一修正。
