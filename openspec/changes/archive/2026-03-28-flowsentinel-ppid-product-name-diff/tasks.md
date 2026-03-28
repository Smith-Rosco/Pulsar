## 1. 准备与分析

- [x] 1.1 通读 `FlowSentinel.txt` 全文，确认 `Main`、`CheckParamDiff`、`BuildResultRow` 等函数的调用链和参数传递路径
- [x] 1.2 确认 `CheckParamDiff` 当前所有调用点，记录需要同步修改签名的位置
- [x] 1.3 确认状态色渲染逻辑所在位置（哪段代码根据 diff 字符串决定行颜色），为后续颜色差异化做准备

## 2. 实现 ExtractProductName 函数

- [x] 2.1 在脚本末尾新增 `Function ExtractProductName(ws As Worksheet) As String`
- [x] 2.2 使用 `CreateObject("VBScript.RegExp")` 构建正则，模式为 `[A-Z0-9]{2}\d{4}[A-Z]{2}(-[A-Z])?\.\d{2}`，启用 `IgnoreCase = False`
- [x] 2.3 循环扫描工作表前10行所有列（A到最后使用列），对每个单元格字符串做全词匹配（`^...$` 锚定或用 `Test`）
- [x] 2.4 找到第一个匹配后，用捕获组或字符串截取去掉 `.\d{2}` 版本号后缀，返回核心产品名
- [x] 2.5 无匹配时返回空字符串 `""`

## 3. 实现 NormalizePpid 函数

- [x] 3.1 在脚本末尾新增 `Function NormalizePpid(ppid As String, prodName As String) As String`
- [x] 3.2 若 `prodName` 为空字符串，直接返回原始 `ppid`
- [x] 3.3 从 `prodName` 解析出各级形式：完整名含variant（如 `XY1234AB-C`）、完整名不含variant（`XY1234AB`）、客户+query（前6字符 `XY1234`）、仅 query name（字符3-6的4位数字 `1234`）
- [x] 3.4 按优先级从长到短，用 `InStr` 定位，找到第一个命中即用 `Replace` 替换为 `"<PROD>"` 并返回结果
- [x] 3.5 全部未命中时返回原始 `ppid`

## 4. 修改 CheckParamDiff 函数签名与逻辑

- [x] 4.1 在 `CheckParamDiff` 函数签名末尾新增两个参数：`prodName1 As String, prodName2 As String`
- [x] 4.2 在现有 PPID 字符串比对（`If CStr(...) <> CStr(...)`）的分支内，调用 `NormalizePpid` 分别归一化两个 PPID
- [x] 4.3 若归一化后相同，将 `diff` 设为 `"仅产品名差异"`；否则保持 `"PPID差异"`

## 5. 修改 Main 函数：提取产品名并传递

- [x] 5.1 在 `Main` 函数初始化阶段（`head1`/`head2` 确定之后），调用 `ExtractProductName(ws1)` 和 `ExtractProductName(ws2)` 分别提取两张表的产品名，存入局部变量 `prodName1`、`prodName2`
- [x] 5.2 找到所有调用 `CheckParamDiff` 的位置，在调用参数末尾追加 `prodName1, prodName2`

## 6. 差异颜色差异化

- [x] 6.1 找到根据 diff 字符串决定行状态色的逻辑段
- [x] 6.2 在该逻辑中新增条件：当 diff 包含 `"仅产品名差异"` 时使用 `COLOR_STATUS_INFO`（浅蓝），不影响现有 `"PPID差异"` 使用 `COLOR_STATUS_WARN`（浅黄）的逻辑

## 7. 验证与收尾

- [ ] 7.1 在 Excel 中打开测试用例：两张表产品名不同但流程结构一致，验证「仅产品名差异」正确显示为浅蓝
- [ ] 7.2 验证两张表 PPID 有真实结构差异时，仍显示「PPID差异」浅黄
- [ ] 7.3 验证产品名识别失败（前10行无符合格式值）时，行为与修改前完全一致
- [ ] 7.4 验证 Mask、DCOP 等其他字段比对逻辑不受影响
