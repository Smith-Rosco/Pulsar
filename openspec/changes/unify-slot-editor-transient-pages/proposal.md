# Unify Slot Editor on Transient Pages

## Why

Slot 配置目前存在三套编辑逻辑、两个入口：主 slot 编辑（模态对话框）、新建 slot（模态向导）、子动作（行内紧凑双下拉）。行内双下拉存在真实缺陷——`SubSlotEditorRow.Rebuild()` 在选中提交过程中重建 `ItemsSource`，导致选动作必须选两次；更根本的是模态打断工作流（ADR-029 已裁定 heavy configuration 不走 modal）且三套 UI 无法共享同一份交互与验证逻辑。ADR-029 P3 已明确预留「slot editor 迁 transient page + entity-scoped id」，本次即落地该延期项。

## What Changes

- 新增 `SettingsSlotEditorPage` transient 页（页面 id 模板 `slot-editor`），统一承载主 slot 编辑与新建：
  - **新建**：类型选择作为 tab 内第 1 步（用户决策 2026-09-09），配置为第 2 步；草稿经显式「添加到槽位」按钮提交（`CommitCreatedSlot`），提交后 tab 转为编辑模式不关闭。
  - **编辑**：直接编辑 live `PluginSlot`（与现对话框同语义），无页内保存按钮，统一走设置窗口底部保存与既有脏链（`SlotEditorWorkspace` PropertyChanged → `MarkDirty`）。
  - **实体 id**：`slot-editor:<contextKey>:<slotNo>`（编辑）/ `slot-editor:<contextKey>:draft`（新建草稿，每上下文单例）；同一实体二次触发激活既有 tab。
- 子动作编辑升级：行内紧凑双下拉改为可展开手风琴行（同时只展开一行），复用主编辑器同款 `SlotParameterEditorField` 控件与级联逻辑（插件 → 动作 → 参数），消除双选 bug 类；排序/删除/布局样式选择保留。
- `ITransientPageService` 扩展：新增模板+实体双参打开 API（`OpenTransientPageAsync(templateId, entityId)`），组合 id 在打开时拼装并按实体注册/注销；旧签名保留并委托。
- tab 生命周期边角：删除 slot / 删除 profile 联动关闭对应编辑 tab；脏页语义不变（全局脏、脏 tab 留开、窗口关闭统一 guard，per-page dirty 仍按 ADR-029 延期）。
- P0 独立小修（先行交付，与迁移解耦）：子动作动作下拉改 VM 持有 `SelectedActionOption`（SelectedItem 绑定 + 显式同步），杜绝 `ItemsSource` 重建期间 TwoWay null 回写吞选择。
- 模态退役（末阶段）：`AddSlotContent` / `SlotConfigurationDialogContent` 模态流程删除，一次性确认/picker 仍走 modal（ADR-029 原则）；E2E 全绿为退役门槛。

## Capabilities

### New Capabilities

- `slot-editor-transient-page`: slot 编辑器作为 transient 设置页的行为契约——实体 id 命名与单例激活、tab 内两步新建向导（类型选择 → 配置）、提交转编辑、live 编辑 + 统一保存、子动作手风琴表单、实体删除联动关 tab。

### Modified Capabilities

- `settings-transient-pages`: 「按类型单例」扩展为「按实体单例」——模板 id + 实体 id 组合成注册 id，打开数量上界从"注册的临时页类型数"放宽为"打开的实体编辑 tab 数"；新增实体删除联动注销场景。
- `cascade-submenu-editor`: 子动作编辑从"slot configuration dialog 行内紧凑行"改为"slot 编辑器（transient 页）内可展开手风琴行"，插件/动作选择采用 VM 持有选中项（SelectedItem）模式，杜绝 ItemsSource 重建吞选择。

## Impact

- **代码**：`Services/ITransientPageService.cs` + `SettingsTransientPageService.cs`（API 扩展）、`Views/Pages/SettingsSlotEditorPage`（新）、`ViewModels/Settings/`（页 VM，组合 `SlotEditorViewModel`）、`ViewModels/Dialogs/SubSlotEditorRow.cs`（P0 修复 + 表单化）、`ViewModels/SettingsViewModel.cs`（入口改道）、`Services/SettingsPageCatalog.cs` / `SettingsPageFactory.cs`（模板注册）、`Resources/Strings*.resx`（新字符串）。
- **E2E/UIA**：现有 E2E 依赖模态对话框 AutomationId 路径，P1/P2 每批同步更新；P3 模态删除以 E2E 全绿为门槛。
- **兼容**：`Profiles.json` 格式不变；单写入者 `SettingsEditorSession` / `ConfigEditSession` 链路不变；一次性确认/picker 保持 modal。
- **关联文档**：ADR-029 增补 P3 落地决议（archive 时写 ADR 或增补节）。
