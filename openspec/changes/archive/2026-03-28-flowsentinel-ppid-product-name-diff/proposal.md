## Why

FlowSentinel 目前对 PPID 做纯字符串比对，任何细微差异都归为「PPID差异」。实际上，当两张流程表对比的是不同产品（或同产品不同客户代码版本）时，PPID 中仅有产品名部分不同，而流程结构本质上是一致的。这类差异被误报为严重 PPID 差异，导致分析人员无法快速识别真正的流程偏差。

## What Changes

- 新增 `ExtractProductName` 函数：扫描工作表前10行，用正则识别符合格式的产品名（`AA1234BB.01` 或 `AA1234BB-C.01`）
- 新增 `NormalizePpid` 函数：将 PPID 中的产品名「影子」（完整名 / 客户+query / 仅query）替换为占位符 `<PROD>`
- 修改 `CheckParamDiff` 函数：在 PPID 不同时，进一步判断归一化后的 PPID 是否一致，若一致则报「仅产品名差异」，否则保持「PPID差异」
- 「仅产品名差异」使用降级警告色（INFO 级别，浅蓝），区别于「PPID差异」的警告色（WARN 级别，浅黄）

## Capabilities

### New Capabilities

- `product-name-extraction`: 从工作表前10行自动识别产品名，支持带/不带 variant（`-C`）的完整格式
- `ppid-product-name-normalization`: 将 PPID 字符串中出现的产品名（三种形式）替换为统一占位符，用于去除产品名干扰后的结构比对
- `ppid-diff-classification`: 在现有 PPID 比对基础上，细化为「仅产品名差异」vs「PPID差异」两个等级，并对应不同视觉警告级别

### Modified Capabilities

- （无现有 spec 需要修改，FlowSentinel 为独立 VBA 脚本，不在 openspec/specs/ 管理范围内）

## Impact

- **目标文件**: `Scripts/VBA/FlowSentinel.txt`（VBA 宏脚本，单文件）
- **修改函数**: `CheckParamDiff`（调用方需在 `Main` 中提前提取产品名并传入）
- **新增函数**: `ExtractProductName(ws As Worksheet) As String`、`NormalizePpid(ppid As String, prodName As String) As String`
- **调用方**: `Main` 函数在初始化阶段调用 `ExtractProductName`，结果传递给 `CheckParamDiff`
- **依赖**: VBScript.RegExp（COM 对象，Excel VBA 原生支持，无需额外依赖）
- **向后兼容**: 当产品名无法识别时，行为回退至现有纯字符串比对，不影响现有功能
