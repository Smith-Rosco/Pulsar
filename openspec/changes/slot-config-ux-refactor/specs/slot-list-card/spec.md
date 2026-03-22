## ADDED Requirements

### Requirement: 卡片聚焦状态摘要
Slot 列表卡片折叠状态 SHALL 显示：序号角标、插件图标、Label、HealthBadge（Ready / Warning / Error）、SummaryTokens 摘要行。不得在折叠状态展示任何可编辑控件。

#### Scenario: 折叠卡片显示健康状态徽章
- **WHEN** Slot 的 `ValidationSeverity` 为 Warning 或 Error
- **THEN** 卡片 Header 右侧 SHALL 显示对应颜色（Warning=#D97706，Error=#DC2626）的徽章文字

#### Scenario: 折叠卡片显示摘要 tokens
- **WHEN** Slot 有 `SummaryTokens`
- **THEN** 卡片 Header 副标题区 SHALL 以 chip 形式展示最多 3 个 token，超出部分截断

### Requirement: 卡片展开区只保留核心快速编辑
卡片展开区 SHALL 仅包含：QuickEditParameters（若有）、ValidationSummary 告警区（若有）、主操作按钮（Edit Details）。SHALL NOT 包含完整参数表单、Appearance Expander 或 Remove 主按钮。

#### Scenario: 展开区无多余参数表单
- **WHEN** 用户展开 Slot 卡片
- **THEN** 展开区 SHALL 不显示 Required/Optional/Advanced 参数列表

#### Scenario: 展开区显示快速编辑字段
- **WHEN** Slot 有 `QuickEditParameters`
- **THEN** 展开区 SHALL 显示这些字段的内联编辑控件

#### Scenario: 展开区无快速编辑字段时显示引导
- **WHEN** Slot 没有 `QuickEditParameters`
- **THEN** 展开区 SHALL 显示「Open Edit Details for full configuration」提示文字

### Requirement: Edit Details 是展开区唯一主操作
展开区底部 SHALL 只有一个主操作按钮 `Edit Details`，使用 `PulsarSecondaryButtonStyle`，全宽显示。

#### Scenario: Edit Details 按钮存在且可点击
- **WHEN** 用户展开任意 Slot 卡片
- **THEN** SHALL 看到 `Edit Details` 按钮，点击后打开 SlotConfigurationDialog

### Requirement: 危险操作移入 Context Menu
Remove（删除 Slot）操作 SHALL 从卡片展开区主操作位移除，改为通过卡片右键 ContextMenu 或三点图标按钮（`MoreHorizontal24`）触发。

#### Scenario: 三点按钮显示在卡片 Header
- **WHEN** 用户悬停或聚焦 Slot 卡片 Header
- **THEN** SHALL 在 Header 右侧显示三点图标按钮

#### Scenario: ContextMenu 包含 Remove 选项
- **WHEN** 用户点击三点按钮或右键卡片
- **THEN** SHALL 显示包含「Remove Slot」选项的 ContextMenu，样式正确（需注入 ui:ControlsDictionary）

#### Scenario: Remove 触发二次确认
- **WHEN** 用户点击「Remove Slot」
- **THEN** SHALL 弹出确认对话框，用户确认后才执行删除

### Requirement: ValidationSummary 分级显示
卡片展开区的验证反馈 SHALL 根据 `ValidationSeverity` 显示不同图标和颜色：Warning 使用 `Warning24` 图标 + 黄色，Error 使用 `ErrorCircle24` 图标 + 红色。

#### Scenario: Warning 级别验证提示
- **WHEN** `ValidationSeverity` 为 Warning
- **THEN** 验证区 SHALL 显示黄色 Warning 图标 + ValidationSummary 文字

#### Scenario: Error 级别验证提示
- **WHEN** `ValidationSeverity` 为 Error
- **THEN** 验证区 SHALL 显示红色 ErrorCircle 图标 + ValidationSummary 文字

#### Scenario: 无验证问题时不显示验证区
- **WHEN** `ValidationSeverity` 为 None
- **THEN** 验证区 SHALL 隐藏（Collapsed）
