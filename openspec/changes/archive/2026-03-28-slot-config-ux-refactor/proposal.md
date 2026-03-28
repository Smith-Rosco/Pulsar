## Why

Slot 配置 UX 目前停留在「工程可用」阶段：列表卡片与详情弹窗职责高度重叠，参数分组信息架构不清晰，危险操作权重失衡，验证反馈缺少可行动性。用户在「理解现状 → 快速调整 → 深度配置」三个层级之间的认知路径模糊，导致配置效率低、出错风险高。

## What Changes

- **重构 Slot 列表卡片**：卡片专注于「状态摘要 + 最关键快速编辑」，移除卡片内的 Appearance Expander 和重复的完整参数表单，降低单卡高度和认知噪声
- **重构 Edit Details 弹窗**：从「搬进弹窗的长表单」升级为「分步引导式配置编辑器」，明确 Identity / Action / Parameters / Appearance 四个区块的职责边界
- **改善验证反馈**：ValidationSummary 增加严重程度标识和具体可行动提示，而不仅是文字描述
- **修正危险操作权重**：Remove 按钮降级，不再与 Edit Details 并排同等宽度；弹窗内 Delete 移出标题区
- **强化 Action 选择体验**：当 Slot 有多个可选 Action 时，用 Radio/Segment 控件替代隐式 ComboBox，让选择意图更直观
- **参数行视觉层级优化**：Required / Optional / Advanced 三级参数在视觉密度和标注方式上做差异化，减少机械表单感

## Capabilities

### New Capabilities

- `slot-list-card`: 重构后的 Slot 摘要卡片 —— 专注状态可见性与最小化快速编辑，支持拖拽排序，Context Menu 承载次要操作
- `slot-config-dialog`: 重构后的 Slot 详情配置弹窗 —— 分区块布局（Identity / Action / Parameters / Appearance），参数分级显示，验证反馈可行动化，危险操作降权

### Modified Capabilities

<!-- 暂无现有 spec 需要 delta -->

## Impact

- `Pulsar/Pulsar/Views/Pages/SettingsSlotsPage.xaml` — 卡片模板重构
- `Pulsar/Pulsar/Views/Pages/SettingsSlotsPage.xaml.cs` — 可能微调事件处理
- `Pulsar/Pulsar/Views/Dialogs/Contents/SlotConfigurationDialogContent.xaml` — 弹窗布局全面重构
- `Pulsar/Pulsar/Views/Dialogs/Contents/SlotConfigurationDialogContent.xaml.cs` — 配合新布局调整
- `Pulsar/Pulsar/ViewModels/Dialogs/SlotConfigurationDialogViewModel.cs` — 可能补充验证分级属性
- `Pulsar/Pulsar/Models/ProfilesConfig.cs` (PluginSlot) — 可能补充 ValidationSeverity 字段
- 不涉及插件系统、热键、PKI 等核心模块
