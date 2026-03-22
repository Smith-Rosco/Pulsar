## 1. 数据模型补充

- [ ] 1.1 在 `PluginSlot`（`ProfilesConfig.cs`）中添加 `ValidationSeverity` 枚举属性（None / Warning / Error），并在 `SetParameterMetadata` 中根据 ValidationSummary 内容自动推断严重程度
- [ ] 1.2 更新 `HealthBadgeColor` 和 `HealthBadgeText` 的计算逻辑，映射到三级 ValidationSeverity

## 2. Slot 列表卡片重构（SettingsSlotsPage.xaml）

- [ ] 2.1 精简卡片展开区：移除 Appearance Expander、移除完整 Required/Optional/Advanced 参数列表，保留 QuickEditParameters 和 ValidationSummary
- [ ] 2.2 展开区无 QuickEditParameters 时添加「Open Edit Details for full configuration」引导提示文字
- [ ] 2.3 将 ValidationSummary 区域改为根据 `ValidationSeverity` 显示不同图标（Warning24 / ErrorCircle24）和颜色
- [ ] 2.4 卡片主操作区：移除 Remove 按钮，仅保留全宽 `Edit Details` 按钮（`PulsarSecondaryButtonStyle`）
- [ ] 2.5 在卡片 Header 右侧添加三点图标按钮（`MoreHorizontal24`，`PulsarIconButtonStyle`）
- [ ] 2.6 创建包含「Remove Slot」选项的 ContextMenu，注入 `ui:ControlsDictionary` 确保样式正确（参考 `Docs/lessons/CONTEXTMENU_RESOURCE_INHERITANCE.md`）
- [ ] 2.7 将 Remove 操作迁移至 ContextMenu，连接到现有 `RemoveSlotCommand`，并在 Code-Behind 添加二次确认逻辑
- [ ] 2.8 验证 ExpandableCard Header 区域的 SummaryTokens chip 展示（最多 3 个），超出截断处理

## 3. Slot 详情弹窗重构（SlotConfigurationDialogContent.xaml）

- [ ] 3.1 重构弹窗整体布局为三段式：Header 区（Identity）/ ScrollViewer 内容区（Action + Parameters）/ Footer 区（Appearance + Delete）
- [ ] 3.2 实现 Identity Header：Label 文本框、Icon 预览+浏览按钮、Color 色块+输入框+拾色按钮，紧凑两列或三列 Grid 排列
- [ ] 3.3 将弹窗标题栏的 Delete 按钮移除，在 Footer 左侧添加「Delete Slot」按钮（`PulsarDangerButtonStyle`）
- [ ] 3.4 实现 Action 选择区块：`HasActionChoices && AvailableActions.Count <= 4` 时使用 RadioButton 列表，`> 4` 时使用 ComboBox，仅 1 个 Action 时只读标签显示
- [ ] 3.5 实现参数分级展示：Required 参数标签添加红色 `*` 标注，Optional 参数添加灰色「Optional」小字，Advanced 参数置于默认折叠的 Expander
- [ ] 3.6 将 ValidationSummary 区块移至内容区顶部（Action 区块下方、参数列表上方），根据 ValidationSeverity 显示对应图标和颜色
- [ ] 3.7 将 Appearance 相关次要设置移入 Footer 区（位于 Delete 按钮右侧或上方），标题区不再显示 Appearance 相关控件
- [ ] 3.8 Delete 按钮点击后调用 `IDialogService.ShowConfirmAsync` 二次确认，确认后执行删除并关闭弹窗

## 4. ViewModel 配套调整

- [ ] 4.1 在 `SlotConfigurationDialogViewModel` 中补充 `ActionSelectionMode` 属性（RadioList / ComboBox / ReadOnly），供 XAML DataTrigger 切换 Action 选择控件
- [ ] 4.2 在 `SlotConfigurationDialogViewModel` 中补充 `ConfirmDeleteCommand`，封装二次确认 + 调用 `_removeSlotAsync` 逻辑
- [ ] 4.3 在 `SettingsSlotsPage.xaml.cs`（或对应 ViewModel）中补充三点按钮的 ContextMenu 显示逻辑，确保 Code-Behind Tag 桥接正确（参考 `Docs/lessons/WPF_USERCONTROL_BINDING_BREAKS.md`）

## 5. 构建验证

- [ ] 5.1 运行 `dotnet build Pulsar/Pulsar/Pulsar.csproj` 确认无编译错误
- [ ] 5.2 手动验证列表页：卡片展开/折叠、三点菜单、Remove 二次确认、Edit Details 打开弹窗
- [ ] 5.3 手动验证弹窗：Identity 编辑、Action RadioButton/ComboBox 切换、参数分级、验证区、Delete 二次确认
