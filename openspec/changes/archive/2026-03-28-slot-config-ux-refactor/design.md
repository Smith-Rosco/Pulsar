## Context

Pulsar 设置页的 Slot 配置分为两个层级：`SettingsSlotsPage`（列表页）中内嵌的展开卡片，以及通过 `Edit Details` 触发的 `SlotConfigurationDialogContent` 弹窗。当前实现功能完整，但存在职责边界模糊、信息层级扁平、危险操作权重失衡等 UX 问题。

本次重构为纯 UI/UX 层面变更，不涉及插件系统、配置序列化、热键或 PKI 核心模块。现有 ViewModel 逻辑保留，仅在必要处补充展示层属性。

## Goals / Non-Goals

**Goals:**
- 明确列表卡片与详情弹窗的职责边界：卡片负责「理解状态」，弹窗负责「完成配置」
- 重构弹窗为分区块布局，参数分级展示，增强可行动验证反馈
- 修正危险操作（Remove/Delete）的视觉权重，避免误操作
- 优化 Action 选择和参数行的视觉层级

**Non-Goals:**
- 不改变配置数据模型的序列化行为
- 不引入新的外部 UI 库或依赖
- 不修改插件参数元数据的加载逻辑
- 不改变拖拽排序机制

## Decisions

### D1: 卡片内容精简策略

**决策**：卡片展开区仅保留 Label/Icon/Color 快速编辑（QuickEditParameters）+ ValidationSummary，完全移除卡片内的 Appearance Expander 和 Parameters 完整列表。

**理由**：卡片的首要职责是「一眼看懂这个 Slot 是什么、状态如何」。当前卡片展开后等同于一个小型配置页，破坏了列表的扫视节奏。Appearance 是低频任务，归入弹窗更合理。

**备选方案**：保留 Appearance Expander 但默认折叠 → 仍然增加卡片高度，不如直接移除。

### D2: 弹窗分区块结构

**决策**：弹窗采用顶部 Header（Identity：Label、Icon、Color）+ 内容区（Action 选择 + Parameters 分级）+ 底部 Footer（Appearance 次要设置 + 关闭按钮）的三段式布局，用 `ui:CardControl` 或带标题的 `Border` 区隔各区块。

**理由**：将视觉外观（Appearance）沉降到底部，让用户聚焦于「这个 Slot 执行什么、参数是否正确」这一核心任务。

**备选方案**：TabControl 分页 → 隐藏了配置全貌，不利于一次性完成配置。

### D3: Action 选择控件

**决策**：当 `HasActionChoices == true`（多于 1 个 Action）时，使用 `RadioButton` 列表替换 ComboBox；单 Action 时展示只读标签。

**理由**：Action 是 Slot 的核心决策，选项通常 ≤ 5 个，RadioButton 让所有选项可见、选择意图明确，避免用户不知道有其他选项。

**备选方案**：保留 ComboBox → 用户需要点开才能发现选项，决策成本高。

### D4: 验证反馈分级

**决策**：在 `PluginSlot` 模型上补充 `ValidationSeverity` 枚举（None / Warning / Error），卡片 Badge 和弹窗提示区根据严重程度使用不同颜色和图标；弹窗验证区展示具体字段名 + 修复建议。

**理由**：现有 `HealthBadgeColor` 仅做二元区分，无法传达「有小问题但可运行」vs「有致命缺失无法执行」的差别。

**备选方案**：保持现有二元状态 → 用户无法判断紧急程度，倾向于忽略所有警告。

### D5: 危险操作降权

**决策**：
- 列表卡片：Remove 按钮从主操作区移入 ContextMenu（右键 / 三点菜单），主操作区只保留 `Edit Details`
- 弹窗：Delete 从标题栏右侧移至底部 Footer，使用 `PulsarDangerButtonStyle` 但添加 `ConfirmationDialog` 二次确认

**理由**：删除是不可逆操作，当前位置与高频编辑操作并排，存在误触风险。

## Risks / Trade-offs

- [风险] 卡片移除 Appearance 区块后，部分用户可能找不到外观设置入口 → 缓解：弹窗 Appearance 区块添加明显的区块标题和展开状态默认为 Expanded
- [风险] RadioButton 替换 ComboBox 在选项较多时（>5）可能导致弹窗过高 → 缓解：超过 4 个选项时回退到 ComboBox，由 `HasActionChoices && AvailableActions.Count <= 4` 控制
- [风险] ContextMenu 右键对 Windows 桌面用户是已知模式，但可发现性低于明显按钮 → 缓解：保留三点图标按钮（`MoreHorizontal24`）作为 ContextMenu 的显式触发入口
- [风险] WPF ContextMenu 不继承 Window 资源（见 AGENTS.md 已知 pitfall）→ 缓解：手动注入 `ui:ControlsDictionary` 到 ContextMenu.Resources

## Migration Plan

1. 修改 `SettingsSlotsPage.xaml` 卡片模板（不破坏绑定，仅调整 XAML 结构）
2. 修改 `SlotConfigurationDialogContent.xaml` 弹窗布局
3. 在 `PluginSlot` 补充 `ValidationSeverity` 计算属性（只读，不序列化）
4. 在 `SlotConfigurationDialogViewModel` 补充辅助属性（`UseRadioForAction`）
5. 运行 `dotnet build` 验证无编译错误

回滚：所有修改限于 XAML 和 ViewModel 展示层，Git revert 即可。

## Open Questions

- `ConfirmDelete` 二次确认是复用现有 `IDialogService` 的 `ShowConfirmAsync`，还是内联一个 inline 确认展开区？→ 建议复用现有 DialogService 保持一致性。
- 弹窗是否需要「Apply / Cancel」按钮，还是维持当前的即时保存（live binding）模式？→ 当前 live binding 模式简单有效，保留；重构不改变此行为。
