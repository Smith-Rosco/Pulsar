## ADDED Requirements

### Requirement: 从工作表前10行自动识别产品名
系统 SHALL 扫描工作表前10行的所有单元格，找到第一个符合产品名格式的值并返回其核心部分（不含版本号后缀）。产品名格式为：2位大写字母或数字（客户代码）+ 4位数字（query name）+ 2位大写字母（后缀）+ 可选的连字符和1位大写字母（variant），后跟点号和2位版本数字。识别失败时返回空字符串。

#### Scenario: 识别标准格式产品名（无variant）
- **WHEN** 工作表前10行中存在值 `XY1234AB.01`
- **THEN** `ExtractProductName` 返回 `XY1234AB`

#### Scenario: 识别带variant的产品名
- **WHEN** 工作表前10行中存在值 `XY1234AB-C.01`
- **THEN** `ExtractProductName` 返回 `XY1234AB-C`

#### Scenario: 客户代码含数字
- **WHEN** 工作表前10行中存在值 `1A1234BC.02`
- **THEN** `ExtractProductName` 返回 `1A1234BC`

#### Scenario: 前10行无符合格式的值
- **WHEN** 工作表前10行所有单元格均不符合产品名格式
- **THEN** `ExtractProductName` 返回空字符串

#### Scenario: 产品名出现在非A4单元格
- **WHEN** 产品名值位于第7行B列
- **THEN** `ExtractProductName` 仍能正确识别并返回产品名核心部分
