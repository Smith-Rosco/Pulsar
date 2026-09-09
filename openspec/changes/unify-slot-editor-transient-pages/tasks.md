# Tasks: Unify Slot Editor on Transient Pages

## 1. P0 — 双选 bug 独立小修（可先行交付）

- [x] 1.1 `SubSlotEditorRow` 增加 VM 持有的 `SelectedActionOption`（SelectedItem 绑定），`Rebuild()` 末尾显式同步选中项；`_isRebuilding` 期间不回写 `Action`。验证：单测——选插件后选动作一次即出现参数字段且选中项不丢（新增用例）
- [x] 1.2 `SlotConfigurationDialogContent.xaml` / `AddSlotContent.xaml` 子动作动作下拉改绑 `SelectedActionOption`。验证：真机——插件→动作一次选中即展开参数；`dev.ps1 all` 全绿

## 2. P1 — transient 机制扩展 + 编辑模式迁移

- [ ] 2.1 `ITransientPageService` 新增 `OpenTransientPageAsync(templateId, entityId)`，旧签名委托（entityId=null）；组合 id `"<templateId>:<entityId>"`。验证：单测——组合 id 注册/激活/回收、旧调用方行为不变
- [ ] 2.2 登记 `slot-editor` 模板定义（组合根 + `SettingsPageFactory.RegisterCreator`），新建 `SettingsSlotEditorPage`（主题注入遵循 ApplyTheme-after-InitializeComponent）。验证：单测——模板注册与页面构造
- [ ] 2.3 页 VM 组合 `SlotEditorViewModel`（委托注入同现对话框），编辑模式直连 live `PluginSlot`；页面无独立保存按钮。验证：单测——编辑字段经脏链置 `HasUnsavedChanges`
- [ ] 2.4 `SettingsViewModel.OpenSlotConfiguration` 改调 transient service（保留旧模态路径为 P3 前的回退开关）。验证：真机——编辑 Global slot 1 打开 `slot-editor:Global:1` tab；二次触发激活既有 tab
- [ ] 2.5 tab 标题实体化（slot 标签 + 上下文，走 resx）。验证：真机——标题随 slot 标签显示
- [ ] 2.6 生命周期联动：删 slot / 删 profile → messenger 消息 → `UnregisterTransient` 组合 id（前缀清理 profile）。验证：单测 + 真机——删除 slot 1 时其编辑 tab 同步消失
- [ ] 2.7 E2E 随批更新（编辑入口 AutomationId 与导航路径）；`dev.ps1 all` 全绿为 done 门槛。验证：E2E 全绿

## 3. P2 — 新建向导入 tab + 子动作手风琴

- [ ] 3.1 页 VM 两步向导状态机（类型选择/配置，复用 `IsConfigurationActive` 与 `GoBackToPicker` 语义）。验证：单测——步骤切换重置 draft、不置脏
- [ ] 3.2 `SettingsViewModel.AddSlotDialog` 改开 `slot-editor:<contextKey>:draft`（每上下文单例草稿）。验证：真机——重复触发"新建"激活既有 draft tab
- [ ] 3.3 显式提交动作（`CommitCreatedSlot`）+ 提交后 tab 重注册为 `slot-editor:<ctx>:<slotNo>` 转编辑模式。验证：真机——新建 slot 后 tab 续存且可继续编辑；Profiles.json 落盘正确
- [ ] 3.4 子动作行升级手风琴（同时展开一行，复用 `SlotParameterEditorField` 与 picker 桥），移除紧凑双下拉模板。验证：单测——展开互斥；真机——参数/picker 全可用
- [ ] 3.5 布局样式选择、排序/删除保留在列表层；空态文案迁 resx。验证：真机——Fan/Ring 生效、增删排序正常
- [ ] 3.6 E2E 随批更新（新建向导路径、子动作编辑）；`dev.ps1 all` 全绿为 done 门槛。验证：E2E 全绿

## 4. P3 — 模态退役与收口

- [ ] 4.1 删除 `AddSlotContent` / `SlotConfigurationDialogContent` 模态流程与死代码（前置：3.x 全部 done 且 E2E 全绿）。验证：`dev.ps1 all` 全绿；全仓 grep 无残留引用
- [ ] 4.2 resx 清理孤儿键；`SettingsViewModel` 移除对话框专用命令。验证：构建 0/0
- [ ] 4.3 ADR-029 增补 P3 落地决议（或独立 ADR）；journal + NEXT.md 收口。验证：文档评审通过
- [ ] 4.4 openspec archive（`/opsx-archive`，delivery both）。验证：`openspec validate` 通过，specs 归档落位
