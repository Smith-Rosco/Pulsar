## Why

Pulsar 的 UI 在快速迭代中积累了多处可用性债务：设置导航层级混乱、Radial Menu 在高 DPI 下存在布局缺陷、核心配置对话框认知负荷过重、视觉语言缺乏系统性。这些问题直接影响新用户的上手效率和老用户的日常操作流畅度，需在下一个版本周期内系统性修复。

## What Changes

- **设置导航重组**：合并 "Plugins" 与 "External Plugins" 为单一入口，将 "Save Changes" 从导航项改为页面内操作按钮
- **Slots 页面 Context 切换器重定位**：将 Profile/Context 下拉框从 Header Actions 区域移至页面主体顶部，作为独立的作用域指示器
- **Radial Menu DPI 适配**：引入 DPI-aware 动态半径计算，根据 Slot 数量自适应菜单尺寸
- **Radial Menu HitTest 修复**：将圆形 HitTest 区域与 JellyOrb 视觉区域严格对齐，消除矩形误触
- **SlotConfiguration 分步向导**：将单页配置表单重构为 3 步向导（类型选择 → 参数配置 → 个性化）
- **IconPicker 交互层级优化**：明确内置图标与自定义上传的视觉优先级
- **间距与排版系统化**：建立统一的 4px 基准间距网格，消除硬编码 Magic Number
- **动效时长统一**：将分散在各 XAML 文件中的动画时长收束至 Theme 资源层统一管理

## Capabilities

### New Capabilities
- `settings-navigation`: 重组后的设置导航结构，合并插件入口，修正保存动作语义
- `slot-context-switcher`: Slots 页面顶部的作用域指示器组件
- `radial-menu-dpi-adaptive`: DPI 感知的 Radial Menu 动态布局系统
- `radial-menu-hittest-fix`: 圆形精确 HitTest 遮罩机制
- `slot-config-wizard`: 分步向导式 Slot 配置对话框
- `icon-picker-ux`: 优化后的 IconPicker 交互层级
- `design-tokens`: 统一的间距、动效时长 Theme 资源体系

### Modified Capabilities

## Impact

- `Views/SettingsWindow.xaml` — 导航结构调整
- `Views/Pages/SettingsSlotsPage.xaml` — Context 切换器重定位
- `Views/RadialMenuWindow.xaml` + `Views/Controls/JellyOrb.xaml` — DPI 适配与 HitTest 修复
- `Views/Dialogs/Contents/SlotConfigurationDialogContent.xaml` + 对应 ViewModel — 向导重构
- `Views/Dialogs/Contents/IconPickerContent.xaml` — 交互层级调整
- `Themes/Theme.Dark.xaml` / `Theme.Light.xaml` — 新增 Design Token 资源键
- `Styles/ButtonStyles.xaml` — 间距 Token 引用更新
