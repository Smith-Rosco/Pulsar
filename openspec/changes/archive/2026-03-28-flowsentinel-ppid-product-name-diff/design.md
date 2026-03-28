## Context

FlowSentinel (`Scripts/VBA/FlowSentinel.txt`) 是一个单文件 VBA 宏，使用 Needleman-Wunsch 全局对齐算法比对两张工艺流程表。当前 `CheckParamDiff` 函数对 PPID 字段做纯字符串等值比对，不区分差异类型。

产品名格式为 `AA1234BB.01` 或 `AA1234BB-C.01`（客户代码2位 + query name 4位数字 + 后缀字母2位 + 可选variant + 版本号）。产品名通常出现在工作表前10行某单元格中，但具体行号不固定。在 PPID 字符串中，产品名可能以三种形式出现：完整名、客户+query、或仅 query name（4位纯数字）。

VBA 环境无原生正则支持，需通过 `CreateObject("VBScript.RegExp")` 使用 COM 正则引擎。

## Goals / Non-Goals

**Goals:**
- 自动从两张工作表各自识别产品名
- 将 PPID 中的产品名「影子」归一化为占位符后再做比对
- 将 PPID 差异细化为「仅产品名差异」和「PPID差异」两级
- 「仅产品名差异」使用 INFO 色（浅蓝），降低视觉警告级别
- 产品名识别失败时，行为完全回退至现有逻辑

**Non-Goals:**
- 不修改 Needleman-Wunsch 算法或评分矩阵
- 不修改 PPID 以外的字段比对逻辑（Mask、DCOP 等）
- 不处理产品名在 PPID 中出现多次的歧义消解（接受一定误判率）
- 不引入任何新的外部依赖或文件

## Decisions

### 决策1：产品名识别用正则还是字符串解析？

**选择：VBScript.RegExp 正则**

产品名格式有可选段（`-C`）和严格的字符类型约束（特定位置必须是字母/数字），字符串解析需要多层 `Mid`/`Left`/`IsNumeric` 组合，可读性差且边界条件难以覆盖。正则模式 `[A-Z0-9]{2}\d{4}[A-Z]{2}(-[A-Z])?\.\d{2}` 一次表达完整约束，更可靠。

VBScript.RegExp 是 Excel VBA 标准 COM 组件，无需额外安装。

**备选**：逐字符解析 → 放弃，代码复杂且难以维护。

### 决策2：NormalizePpid 的归一化策略

**选择：优先级替换，从长到短，第一次命中即停止**

PPID 中产品名的三种形式长度不同，必须从最长（完整名8-10字符）到最短（query 4字符）依次尝试，避免短模式误命中长模式的子串。替换为固定占位符 `<PROD>`，使两边归一化后可直接做字符串等值比对。

替换顺序：
1. 完整名（含variant）：`AA1234BB-C`
2. 完整名（不含variant）：`AA1234BB`
3. 客户+query：`AA1234`（前2字母+4数字）
4. 仅 query name：`1234`（4位纯数字）

**备选**：同时替换所有形式 → 放弃，可能造成过度替换和占位符嵌套。

### 决策3：`CheckParamDiff` 签名扩展方式

**选择：在 `Main` 中提前提取产品名，作为字符串参数传入 `CheckParamDiff`**

产品名提取是一次性操作（每张表只需提取一次），不应在每行比对时重复执行。在 `Main` 初始化阶段调用 `ExtractProductName`，将结果字符串传入比对函数，性能最优且职责清晰。

`CheckParamDiff` 新签名：
```
Function CheckParamDiff(ws1, r1, d1, ws2, r2, d2, prodName1 As String, prodName2 As String) As String
```

**备选**：每次调用时实时提取 → 放弃，重复扫描工作表性能差。

### 决策4：差异级别与颜色映射

| 差异类型 | 标签 | 颜色常量 | 含义 |
|---------|------|---------|------|
| PPID 完全一致 | （无） | - | 无差异 |
| 归一化后一致 | 仅产品名差异 | `COLOR_STATUS_INFO`（浅蓝） | 流程结构相同，仅产品标识不同 |
| 归一化后仍不同 | PPID差异 | `COLOR_STATUS_WARN`（浅黄） | 真实流程偏差 |

## Risks / Trade-offs

- **[风险] query name 误匹配**：4位纯数字在复杂 PPID 中可能误命中其他数字片段（如版本号 `V1234`）→ 缓解：优先尝试更长的完整名形式；用户已确认可接受一定误判率
- **[风险] 产品名无法识别**：前10行无符合格式的单元格（如模板表、空表）→ 缓解：`ExtractProductName` 返回空字符串，`NormalizePpid` 直接返回原始 PPID，行为退化为现有逻辑
- **[风险] COM 对象创建失败**：极少数受限环境下 `VBScript.RegExp` 不可用 → 缓解：`On Error Resume Next` 包裹，失败时回退字符串比对
- **[Trade-off] 签名变更**：`CheckParamDiff` 增加两个参数，所有调用方需同步更新 → 影响面小（仅 `Main` 中一处调用）

## Migration Plan

本变更为单文件 VBA 脚本修改，无部署流程：
1. 在 `FlowSentinel.txt` 中新增两个函数（`ExtractProductName`、`NormalizePpid`）
2. 修改 `Main` 函数：初始化后调用 `ExtractProductName` 提取两表产品名
3. 修改 `CheckParamDiff` 函数签名及内部 PPID 比对逻辑
4. 更新 `Main` 中对 `CheckParamDiff` 的调用，传入产品名参数
5. 手动在 Excel VBA 编辑器中替换宏内容，运行对比验证输出结果

回滚：保留原始 `FlowSentinel.txt` 备份，直接恢复即可。

## Open Questions

- 「仅产品名差异」是否需要在 Dashboard 统计区单独计数？（当前设计复用现有 `ParamDiff` 计数器，未区分子类型）
- 当两表产品名均识别成功但互不相同时，归一化仍能正确工作（各自抹各自）；但是否需要在报告中展示识别到的产品名，以便用户确认？
