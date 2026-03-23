## 1. Design Tokens（基础，其他任务依赖）

- [ ] 1.1 在 `Themes/Theme.Dark.xaml` 中添加 `Pulsar.Spacing.*` 间距 Token（XS/SM/MD/LG/XL）
- [ ] 1.2 在 `Themes/Theme.Light.xaml` 中添加相同的 `Pulsar.Spacing.*` 间距 Token
- [ ] 1.3 在 `Themes/Theme.Dark.xaml` 中添加 `Pulsar.Animation.Duration.*` 动效时长 Token（Fast/Normal/Slow）
- [ ] 1.4 在 `Themes/Theme.Light.xaml` 中添加相同的 `Pulsar.Animation.Duration.*` 动效时长 Token
- [ ] 1.5 验证 Token 可在 XAML 中通过 `{StaticResource}` 正常解析（构建无报错）

## 2. Radial Menu HitTest 修复

- [ ] 2.1 在 `Views/Controls/JellyOrb.xaml.cs` 中重写 `HitTestCore(PointHitTestParameters)`，实现圆形命中检测逻辑
- [ ] 2.2 重写 `HitTestCore(GeometryHitTestParameters)` 处理几何命中
- [ ] 2.3 验证 JellyOrb 圆形边缘外的角落区域点击不触发响应
- [ ] 2.4 验证圆形内部点击正常触发 hover 和 command

## 3. Radial Menu DPI 适配

- [ ] 3.1 在 `ViewModels/RadialMenuViewModel.cs` 中添加 `LayoutRadius` 属性和 `UpdateLayout(double dpiScale)` 方法
- [ ] 3.2 在 `Views/RadialMenuWindow.xaml.cs` 的 `Loaded` 事件中读取 DPI 缩放系数并传入 ViewModel
- [ ] 3.3 在 `RadialMenuViewModel` 的 Slot 坐标计算中使用 `LayoutRadius` 替代硬编码半径值
- [ ] 3.4 将 `RadialMenuWindow` 的窗口尺寸绑定为 `BaseSize × DpiScale` 的动态值
- [ ] 3.5 在高 DPI（150%）环境下验证菜单布局正确（或通过 DPI 模拟验证坐标计算）

## 4. 设置导航重组

- [ ] 4.1 在 `Views/SettingsWindow.xaml` 中移除 "External Plugins" 独立导航项
- [ ] 4.2 移除 "Save Changes" NavigationViewItem
- [ ] 4.3 在 `Views/Pages/SettingsPluginsPage.xaml` 中添加 TabControl，含 "Built-in" 和 "External" 两个 Tab
- [ ] 4.4 将 `SettingsExternalPluginsPage` 的内容迁移/引用到 "External" Tab 中
- [ ] 4.5 在 `Views/Pages/SettingsGeneralPage.xaml` 底部添加固定操作栏，含 `PulsarPrimaryButtonStyle` 保存按钮
- [ ] 4.6 更新 `ViewModels/SettingsWindowViewModel.cs`，移除 Save 导航相关逻辑
- [ ] 4.7 验证导航列表无多余项，Plugins 页面 Tab 切换正常

## 5. Slots 页面 Context 切换器重定位

- [ ] 5.1 在 `Views/Pages/SettingsSlotsPage.xaml` 的 Header ActionsContent 中移除 Context 下拉选择器
- [ ] 5.2 在页面主体 Slot 列表上方添加独立的 Context 切换器行（Label + ComboBox 组合）
- [ ] 5.3 确保 Context 切换器的数据绑定与原有 ComboBox 一致
- [ ] 5.4 使用 `{StaticResource Pulsar.Spacing.MD}` 设置切换器与列表之间的间距
- [ ] 5.5 验证切换 Context 后 Slot 列表正确刷新

## 6. SlotConfiguration 三步向导

- [ ] 6.1 在 `ViewModels/Dialogs/SlotConfigurationDialogViewModel.cs` 中添加 `CurrentStep`（int）、`CanGoNext`、`CanGoBack` 属性及 `NextStepCommand`、`BackStepCommand`
- [ ] 6.2 在 ViewModel 中实现三步状态机逻辑（Step 0: 类型选择，Step 1: 参数配置，Step 2: 个性化）
- [ ] 6.3 重构 `Views/Dialogs/Contents/SlotConfigurationDialogContent.xaml`，用 `ContentControl` + `DataTrigger` 控制三个步骤面板的显隐
- [ ] 6.4 实现 Step 0 插件类型选择面板（大卡片列表，替换原下拉选择）
- [ ] 6.5 实现 Step 1 参数配置面板（复用现有 `DialogSlotParameterFieldTemplate`）
- [ ] 6.6 实现 Step 2 个性化面板（图标、颜色、标签，从原表单提取）
- [ ] 6.7 在对话框顶部添加步骤进度指示文字（"Step X of 3 - 描述"）
- [ ] 6.8 添加 Next/Back/Confirm 按钮，使用正确的 Pulsar 按钮样式
- [ ] 6.9 验证三步流程完整走通，数据正确合并保存
- [ ] 6.10 验证 Back 后已填数据保持不变

## 7. IconPicker 交互层级优化

- [ ] 7.1 在 `Views/Dialogs/Contents/IconPickerContent.xaml` 中移除顶部 "Browse Custom" 按钮
- [ ] 7.2 在图标网格末尾（ListBox/ItemsControl 底部）添加 "+ Upload Custom Icon" 次级按钮（`PulsarSecondaryButtonStyle`）
- [ ] 7.3 绑定自定义上传按钮到现有的 BrowseCustomCommand（或等效命令）
- [ ] 7.4 确保搜索激活时自定义上传入口隐藏（绑定 `IsSearching` 状态）
- [ ] 7.5 验证搜索框默认获得焦点，内置图标正常显示，自定义上传功能完整

## 8. 构建验证

- [ ] 8.1 运行 `dotnet build Pulsar/Pulsar/Pulsar.csproj` 确认零编译错误
- [ ] 8.2 检查所有新增/修改的 XAML 文件无 XamlParseException 风险（Resources 结构合规）
- [ ] 8.3 确认所有按钮使用 Pulsar 样式，无 `Appearance="Primary"` 用法
