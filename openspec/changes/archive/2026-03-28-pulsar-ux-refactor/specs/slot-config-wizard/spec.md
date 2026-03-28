## ADDED Requirements

### Requirement: 三步向导流程
Slot 配置对话框 SHALL 将现有单页表单重构为包含三个步骤的线性向导：Step 0（插件类型选择）、Step 1（参数配置）、Step 2（个性化设置）。

#### Scenario: 打开 SlotConfiguration 对话框
- **WHEN** 用户打开 Slot 配置对话框
- **THEN** 对话框默认显示 Step 0（插件类型选择），Step 1 和 Step 2 内容不可见

#### Scenario: Step 0 选择插件类型
- **WHEN** 用户在 Step 0 点击某个插件类型卡片
- **THEN** 该卡片高亮显示选中状态，"Next" 按钮变为可点击状态

#### Scenario: 从 Step 0 前进到 Step 1
- **WHEN** 用户在 Step 0 已选择插件类型后点击 "Next"
- **THEN** 界面切换到 Step 1，显示所选插件的参数配置字段，Step 0 内容隐藏

#### Scenario: Step 1 参数配置
- **WHEN** 用户在 Step 1 查看参数字段
- **THEN** 仅显示当前所选插件类型的相关参数，复用现有 DialogSlotParameterFieldTemplate

#### Scenario: 从 Step 1 前进到 Step 2
- **WHEN** 用户在 Step 1 填写完必填参数后点击 "Next"
- **THEN** 界面切换到 Step 2，显示图标、颜色、标签等个性化选项

#### Scenario: 步骤回退
- **WHEN** 用户在 Step 1 或 Step 2 点击 "Back"
- **THEN** 界面返回上一步，已填写内容保持不变

#### Scenario: 最终确认
- **WHEN** 用户在 Step 2 点击 "Confirm"
- **THEN** 对话框关闭，Slot 配置以合并三步数据保存

### Requirement: 步骤进度指示器
对话框顶部 SHALL 显示当前步骤的文字描述（如 "Step 1 of 3 - Select Plugin"），让用户清楚知晓所处位置。

#### Scenario: 步骤标题随步骤更新
- **WHEN** 向导步骤发生切换
- **THEN** 顶部步骤描述文字同步更新为当前步骤的标题
