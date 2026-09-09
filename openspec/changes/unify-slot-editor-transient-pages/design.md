# Design: Unify Slot Editor on Transient Pages

## Context

现状三套 slot 配置逻辑（见 proposal.md Why）：

- 编辑：`SettingsViewModel.OpenSlotConfiguration` → `_dialogService.ShowCustomAsync` + `SlotConfigurationDialogContent`，Edit 模式 `SlotEditorViewModel` 直接持有 live `PluginSlot`（`Slot = existingSlot`，SlotEditorViewModel.cs:312）。
- 新建：`AddSlotDialog` → `AddSlotContent` 向导，draft 经 `CommitCreatedSlot` 提交。
- 子动作：`SlotConfigurationDialogContent` 行内 `SubSlotEditorRow`（双下拉 + 参数摘要），`Rebuild()` 在选中提交中重建 `ItemsSource` 导致双选吞选择（SubSlotEditorRow.cs:173-228）。

Transient page 机制（ADR-029 / `settings-transient-pages`）已就绪：`SettingsPageCatalog.RegisterTransient`（分组末插入、事件通知）、`SettingsTransientPageService`（Open/Close/NotifyNavigatedAwayFrom，按 typeId 精确匹配 `_definitions`）、`SettingsPageFactory.RegisterCreator`（运行时注册构造委托）、脏语义（全局脏、脏 tab 留开、关窗统一 guard）。

脏链已修复（2026-09-09，commit `79f1485`）：`PluginSlot.SubActions`/`CascadeLayoutStyle` 走 `SetProperty` 通知，任何 live slot 属性变更 → `SlotEditorWorkspace.OnSlotPropertyChanged → MarkDirty()`。

约束（AGENTS.md 不变量）：本地化走 `ILocalizationService`/resx；`ApplyTheme()` 在 `InitializeComponent()` 之后；按钮用 `Pulsar*ButtonStyle`；配置写入经 `SettingsEditorSession`/`ConfigEditSession` 单写入者；一次性确认/picker 保持 modal。

## Goals / Non-Goals

**Goals**

- 一套编辑器 surface（VM + 控件）服务编辑/新建/子动作三种场景
- 实体级 transient tab（`slot-editor:<contextKey>:<slotNo>` / `:draft`）
- P0 独立修复双选 bug，可先行交付
- 模态退役有 E2E 门槛的安全路径

**Non-Goals**

- per-page dirty 拆分（ADR-029 已延期）
- `SlotEditorViewModel` 编辑核心的大重构/提取（组合优先，见 D3）
- 根轮盘左键执行语义等 radial 侧改动
- 插件设置页 / 黑名单迁移（ADR-029 P2 范围，另行 change）

## Decisions

### D1. 实体 id = 组合注册 id，模板注册 + 打开时克隆

`RegisterDefinition` 登记模板 `slot-editor`（id 即模板 id，含 `SettingsPageRegistration` 元数据：标题模板、图标、`typeof(SettingsSlotEditorPage)`、GroupId=Workbench）。`OpenTransientPageAsync(templateId, entityId)` 拼装 `"<templateId>:<entityId>"`，按组合 id 克隆注册（标题格式化为实体名）后导航。`UnregisterTransient`/回收/单例判定全部按组合 id 原样工作，catalog 零改动。

- 备选：catalog 增加模板概念、id 拆两段存 —— 拒绝：改动面大，且 Unregister/事件消费方都要感知分段。

### D2. `ITransientPageService` 双参打开 API

新增 `OpenTransientPageAsync(string templateId, string entityId)`；现有 `OpenTransientPageAsync(typeId)` 保留并委托（`entityId = null`，行为不变，手势页等既有调用方零改动）。`IsOpen/CloseTransientPageAsync` 增加组合 id 重载或在调用侧直接传组合 id —— 实现取后者（组合 id 就是合法 typeId，无需重载）。

### D3. 页面 VM 组合 `SlotEditorViewModel`，不提取核心

`SettingsSlotEditorPage` 的 VM 内持有一个 `SlotEditorViewModel` 实例（沿用其委托注入 seam：`createSlotDraft`/`setSlotAction`/`pickParameterValueAsync`/`pickIconAsync`/`pickColorAsync` 由 `SettingsViewModel` 注入，与现对话框完全同源），页面 XAML 绑定到该子 VM；向导步骤（类型选择/配置）由页 VM 的可见状态驱动（`IsTypePickerActive`/`IsConfigurationActive`，后者 `SlotEditorViewModel` 已有）。`IWizardDialogViewModel`/`IDialogViewModel` 成员在页内不使用（RequestClose 空实现）。

- 备选：提取 `SlotEditingCore` 服务 —— 拒绝（现阶段）：提取是一次高风险重构，且模态退役前双路径并存期需要 `SlotEditorViewModel` 原样存活；P3 退役后再评估拍平。

### D4. 新建流：draft 生命周期在 tab 内

draft 由 `CreateSlotDraft` 创建，仅存在于页 VM；显式提交动作调 `CommitCreatedSlot`（既有：编号、元数据、`MarkDirty`、`SlotAddedMessage`）。提交成功后按 `GetNextSlotNumber` 结果把 tab 重注册为 `slot-editor:<contextKey>:<slotNo>` 并切换编辑模式。取消（关 tab/导航离开）丢弃 draft，不置脏。步骤 1 返回重选类型 = 重置 draft（既有 `GoBackToPicker` 语义）。

### D5. P0 双选修法：VM 持有选中项

`SubSlotEditorRow` 新增 `SelectedActionOption`（VM 属性，`SelectedItem` 绑定），`Rebuild()` 末尾按当前 `Action` 显式同步选中项；`OnActionChanged` 改由 `SelectedActionOption` setter 驱动，且 `_isRebuilding` 期间不回写 `Action`。主 slot 动作选择（`SelectedValue→Slot.Action`）不动——其 ItemsSource 不在选中过程中重建，无此 bug。

### D6. 子动作表单化：手风琴复用 `SlotParameterEditorField`

行展开区直接复用 `RequiredParameters`/`OptionalParameters`/`AdvancedParameters` 三个 `ObservableCollection<SlotParameterEditorField>`（`SubSlotEditorRow` 已生成）与 picker 桥（`PickSubActionParameterValueAsync`）。同时只展开一行由页 VM 管理展开索引。移除行内紧凑双下拉模板。

### D7. 生命周期联动

`SlotEditorWorkspace.RemoveSlot` / 删 profile 路径发送既有 messenger 消息（或新增 `SlotRemovedMessage`），transient service 订阅并 `UnregisterTransient` 对应组合 id（`slot-editor:<ctx>:<slotNo>`；profile 删除按 `slot-editor:<profileKey>:*` 前缀清理）。draft tab 不受影响。

### D8. 入口改道与 E2E 契约

`SettingsViewModel.OpenSlotConfiguration` / `AddSlotDialog` 改调 transient service；模态内容与 AutomationId 在 P1/P2 期间保留（退役前不删）。E2E 依赖 UIA 点击执行路径与对话框 AutomationId——每批改动同步更新 E2E 用例，`scripts/dev.ps1 all` 全绿为该批 done 门槛；P3 删模态以 E2E 全绿为前置。

## Risks / Trade-offs

- [组合 VM 使页 XAML 绑定路径变长（`vm.SlotEditor.Slot.*`）] → 页 VM 暴露转发属性，绑定路径保持一层；不为绑定期望重写子 VM
- [实体 tab 数量上界放宽为打开实体数] → slot 每上下文 ≤8、上下文少量，实际有界；`NotifyNavigatedAwayFrom` 干净即回收进一步收敛；不做硬 cap（ADR-029 精神：有界即可）
- [draft tab 脏语义边缘：draft 未提交但用户改了别的页置全局脏] → draft 本身不参与脏链（不在 `CurrentSlots`），关闭/回收即丢弃，无数据丢失面
- [E2E 随批更新遗漏导致门槛假绿] → P1/P2 每批 PR 内跑 `dev.ps1 all`；P3 拆成"改道完成"与"模态删除"两个独立批次
- [P0 修复与 D6 表单化的重复劳动] → P0 修在 `SubSlotEditorRow`（D6 继续复用该类），不修 XAML 绑定层，无浪费

## Migration Plan

P0（独立小修）→ P1（模板注册 + 双参 API + 编辑迁移 + 生命周期联动 + E2E 同步）→ P2（新建向导入 tab + 子动作手风琴 + E2E 同步）→ P3（模态退役 + ADR-029 P3 增补 + openspec archive）。每阶段独立可交付、独立回滚（P1/P2 入口改道各为一个 commit，回滚即恢复旧入口调用）。

## Open Questions

无（grill auto-with-guardrails 2026-09-09 已收敛：类型选择进 tab 第一步、子动作留 tab 内手风琴、Q2 接口扩展与 Q8 E2E 契约在审计表标记待复核）。
