## ADDED Requirements

### Requirement: 细化 PPID 差异为「仅产品名差异」和「PPID差异」两级
系统 SHALL 在 `CheckParamDiff` 中，当两行 PPID 原始值不相同时，分别用各自对应的产品名对两个 PPID 进行归一化，再比对归一化结果。若归一化后相同，则标记为「仅产品名差异」；否则标记为「PPID差异」。两种标记使用不同的视觉颜色：「仅产品名差异」使用 INFO 色（`COLOR_STATUS_INFO`，浅蓝），「PPID差异」使用 WARN 色（`COLOR_STATUS_WARN`，浅黄）。

#### Scenario: PPID 仅产品名不同，归一化后一致
- **WHEN** T1 PPID 为 `XY1234AB_ETCH_V02`，T2 PPID 为 `WZ1234AB_ETCH_V02`，两表产品名分别为 `XY1234AB` 和 `WZ1234AB`
- **THEN** `CheckParamDiff` 返回包含「仅产品名差异」的差异字符串

#### Scenario: PPID 有真实结构差异
- **WHEN** T1 PPID 为 `XY1234AB_ETCH_V02`，T2 PPID 为 `WZ1234AB_ETCH_V03`，两表产品名分别为 `XY1234AB` 和 `WZ1234AB`
- **THEN** `CheckParamDiff` 返回包含「PPID差异」的差异字符串（归一化后 `<PROD>_ETCH_V02` ≠ `<PROD>_ETCH_V03`）

#### Scenario: PPID 完全相同（无差异）
- **WHEN** T1 和 T2 的 PPID 原始值相同
- **THEN** `CheckParamDiff` 不包含任何 PPID 相关差异标记

#### Scenario: 产品名识别失败时回退至原始比对
- **WHEN** 至少一张表的产品名识别返回空字符串，且两 PPID 不同
- **THEN** `CheckParamDiff` 返回「PPID差异」（保持现有行为）

#### Scenario: 「仅产品名差异」使用INFO色
- **WHEN** 差异类型为「仅产品名差异」
- **THEN** 对应行的状态色应用 `COLOR_STATUS_INFO`（浅蓝），而非 `COLOR_STATUS_WARN`（浅黄）
