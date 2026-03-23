## ADDED Requirements

### Requirement: Plugins 导航合并
设置窗口 SHALL 将原有的独立 "Plugins" 和 "External Plugins" 两个导航项合并为单一 "Plugins" 导航入口，对应页面内部使用 TabControl 展示 "Built-in" 和 "External" 两个子视图。

#### Scenario: 用户点击 Plugins 导航项
- **WHEN** 用户在设置窗口侧边导航中点击 "Plugins"
- **THEN** 页面内容区显示含两个 Tab（"Built-in" / "External"）的 TabControl，默认选中 "Built-in"

#### Scenario: 用户切换到 External tab
- **WHEN** 用户点击 "External" Tab
- **THEN** 内容区切换显示外部插件列表，与原 SettingsExternalPluginsPage 内容一致

### Requirement: 保存动作语义修正
设置窗口 SHALL 移除 "Save Changes" NavigationViewItem，在含有可编辑状态的每个设置页面底部提供固定的操作栏，内含使用 PulsarPrimaryButtonStyle 的保存按钮。

#### Scenario: 用户在 General 页面修改设置后保存
- **WHEN** 用户修改任意设置项后点击页面底部的 "Save" 按钮
- **THEN** 设置被持久化，按钮短暂显示成功状态，不发生页面跳转

#### Scenario: 导航栏不再包含 Save Changes 项
- **WHEN** 用户查看设置窗口左侧导航列表
- **THEN** 导航列表中不存在 "Save Changes" 或任何非页面性质的导航项
