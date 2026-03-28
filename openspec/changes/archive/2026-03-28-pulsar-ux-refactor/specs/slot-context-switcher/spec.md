## ADDED Requirements

### Requirement: Context 切换器独立定位
Slots 设置页面 SHALL 将 Profile/Context 下拉选择器从 Header Actions 区域移出，作为独立的作用域指示器放置在页面主体内容区的顶部，在 Slot 列表之上，具有清晰的标签说明当前编辑的是哪个 Context。

#### Scenario: 页面加载时显示当前 Context
- **WHEN** 用户导航到 Slots 设置页
- **THEN** 页面主体顶部显示标有 "Current Context:" 标签的下拉选择器，展示当前激活的 Profile/Context 名称

#### Scenario: 切换 Context 后列表刷新
- **WHEN** 用户通过下拉选择器切换到另一个 Context
- **THEN** 下方 Slot 列表立即刷新，显示所选 Context 的 Slot 配置

#### Scenario: Header Actions 区域不再包含 Context 选择器
- **WHEN** 用户查看 Slots 页面的 Header 区域
- **THEN** Header Actions 区域仅包含添加、编辑、删除等操作按钮，不包含下拉选择器
