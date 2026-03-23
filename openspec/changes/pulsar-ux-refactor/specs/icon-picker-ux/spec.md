## ADDED Requirements

### Requirement: 内置图标优先展示
IconPicker 对话框 SHALL 默认展示内置图标网格，搜索框始终可见并作为主要交互入口，自定义图标上传作为次级选项放置在图标网格底部。

#### Scenario: 打开 IconPicker
- **WHEN** 用户打开图标选择对话框
- **THEN** 内置图标网格完整显示，搜索框位于顶部且获得焦点，"Upload Custom" 入口位于图标网格末尾

#### Scenario: 搜索图标
- **WHEN** 用户在搜索框输入关键词
- **THEN** 图标网格实时过滤，显示匹配的图标，"Upload Custom" 入口在搜索状态下隐藏

#### Scenario: 自定义上传入口
- **WHEN** 用户滚动到图标网格底部
- **THEN** 看到 "+ Upload Custom Icon" 次级按钮（使用 PulsarSecondaryButtonStyle），点击触发文件选择

#### Scenario: Browse Custom 按钮不与搜索框并排
- **WHEN** 用户查看 IconPicker 顶部区域
- **THEN** 顶部区域只有搜索框，无 "Browse Custom" 按钮与之竞争视觉注意力
