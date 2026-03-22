## ADDED Requirements

### Requirement: 弹窗三段式布局
`SlotConfigurationDialogContent` SHALL 采用三段式垂直布局：
1. **Header 区**（Identity）：Label、Icon、Color 三个字段横向紧凑排列
2. **内容区**（Action + Parameters）：Action 选择 + Required/Optional/Advanced 参数分级
3. **Footer 区**（Appearance + 危险操作）：外观设置折叠区 + Delete 按钮（左对齐，危险样式）

#### Scenario: 弹窗展示三段区块
- **WHEN** 用户打开 SlotConfigurationDialog
- **THEN** SHALL 看到 Identity Header、Action+Parameters 内容区、Appearance Footer 三个视觉区块，用分隔线或卡片边框区隔

### Requirement: Identity Header 紧凑展示
Header 区 SHALL 在单行或两行内展示 Label（文本框）、Icon（图标预览 + 浏览按钮）、Color（色块预览 + 输入框 + 拾色按钮），不得占用过多垂直空间。

#### Scenario: Identity Header 字段可编辑
- **WHEN** 用户点击 Label 文本框
- **THEN** SHALL 可直接编辑，修改实时同步到 `Slot.Label`

#### Scenario: Icon 预览与浏览
- **WHEN** 用户点击 Icon 浏览按钮
- **THEN** SHALL 触发图标选择流程，选中后预览图标更新

### Requirement: Action 选择使用 RadioButton（≤4 选项）
当 `HasActionChoices == true` 且 `AvailableActions.Count <= 4` 时，Action 选择 SHALL 使用 RadioButton 列表展示所有选项；当选项数 > 4 时，回退使用 ComboBox。

#### Scenario: 少量 Action 使用 RadioButton
- **WHEN** Slot 有 2-4 个可选 Action
- **THEN** SHALL 以垂直 RadioButton 列表展示，当前选中项高亮

#### Scenario: 多 Action 使用 ComboBox
- **WHEN** Slot 有 5 个或以上可选 Action
- **THEN** SHALL 使用 ComboBox 展示 Action 列表

#### Scenario: 单 Action 只读展示
- **WHEN** Slot 只有 1 个 Action
- **THEN** SHALL 以只读标签展示 Action 名称，不显示选择控件

### Requirement: 参数分级展示
内容区 SHALL 将参数分为三级展示：
- **Required**：始终展示，字段标签使用红色星号 `*` 标注
- **Optional**：默认展示，标签标注「Optional」灰色小字
- **Advanced**：默认折叠于 Expander，展开后显示

#### Scenario: Required 参数始终可见
- **WHEN** Slot 有 Required 参数
- **THEN** 内容区 SHALL 始终显示这些参数字段，不可折叠

#### Scenario: Required 参数有星号标注
- **WHEN** 显示 Required 参数行
- **THEN** 参数标签左侧或右侧 SHALL 有红色 `*` 标记

#### Scenario: Advanced 参数默认折叠
- **WHEN** Slot 有 Advanced 参数且用户刚打开弹窗
- **THEN** Advanced Expander SHALL 默认为折叠状态

### Requirement: 验证反馈区在内容区顶部显示
当 `HasValidationSummary == true` 时，内容区 SHALL 在参数列表上方显示验证反馈区块，包含：严重程度图标、ValidationSummary 文字、具体问题字段提示（若有）。

#### Scenario: 验证反馈区显示在参数上方
- **WHEN** Slot 有 ValidationSummary 内容
- **THEN** 验证反馈区 SHALL 在 Action 区块下方、参数列表上方显示

#### Scenario: 无验证问题时不显示验证区
- **WHEN** `HasValidationSummary == false`
- **THEN** 验证反馈区 SHALL 隐藏（Collapsed）

### Requirement: Delete 操作移至 Footer 并二次确认
Delete Slot 按钮 SHALL 从弹窗标题栏移除，改为在 Footer 左侧显示，使用 `PulsarDangerButtonStyle`，点击后通过 `IDialogService.ShowConfirmAsync` 二次确认。

#### Scenario: Delete 按钮在 Footer 左侧
- **WHEN** 用户打开 SlotConfigurationDialog
- **THEN** SHALL 在弹窗底部左侧看到「Delete Slot」按钮，样式为危险红色

#### Scenario: Delete 触发二次确认
- **WHEN** 用户点击「Delete Slot」
- **THEN** SHALL 弹出确认对话框，用户取消后弹窗保持打开，确认后关闭弹窗并删除 Slot

#### Scenario: 标题栏无 Delete 按钮
- **WHEN** 用户打开 SlotConfigurationDialog
- **THEN** 弹窗标题栏 SHALL 不包含任何删除操作按钮

### Requirement: Appearance 区块默认展开置于 Footer
Appearance 相关设置（Icon 高级选项、Color 输入）已在 Header 区提供，Footer 的 Appearance Expander 若仍需保留 SHALL 默认展开且置于 Footer 底部，不得占用内容区主要视觉位置。

#### Scenario: Appearance 区块在 Footer 底部
- **WHEN** 用户打开 SlotConfigurationDialog
- **THEN** Appearance 相关的次要选项（若有）SHALL 位于弹窗 Footer 区，在 Delete 按钮上方或同行右侧
