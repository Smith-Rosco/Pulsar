## ADDED Requirements

### Requirement: 将 PPID 中的产品名影子替换为统一占位符
系统 SHALL 接受一个 PPID 字符串和一个产品名字符串，按优先级从高到低尝试在 PPID 中定位并替换产品名的三种出现形式：完整名含variant、完整名不含variant、客户+query name（前6字符）、仅 query name（4位纯数字）。找到第一种匹配即替换为占位符 `<PROD>` 并返回，不继续尝试更短的形式。若产品名为空字符串，直接返回原始 PPID。

#### Scenario: PPID 含完整产品名（无variant）
- **WHEN** `ppid = "XY1234AB_ETCH_V02"`, `prodName = "XY1234AB"`
- **THEN** `NormalizePpid` 返回 `"<PROD>_ETCH_V02"`

#### Scenario: PPID 含完整产品名（含variant）
- **WHEN** `ppid = "XY1234AB-C_ETCH_V02"`, `prodName = "XY1234AB-C"`
- **THEN** `NormalizePpid` 返回 `"<PROD>_ETCH_V02"`

#### Scenario: PPID 仅含客户+query形式
- **WHEN** `ppid = "XY1234_ETCH_V02"`, `prodName = "XY1234AB"`
- **THEN** `NormalizePpid` 返回 `"<PROD>_ETCH_V02"`

#### Scenario: PPID 仅含query name形式
- **WHEN** `ppid = "1234_ETCH_V02"`, `prodName = "XY1234AB"`
- **THEN** `NormalizePpid` 返回 `"<PROD>_ETCH_V02"`

#### Scenario: 产品名为空字符串时直接返回原始PPID
- **WHEN** `ppid = "XY1234AB_ETCH_V02"`, `prodName = ""`
- **THEN** `NormalizePpid` 返回 `"XY1234AB_ETCH_V02"`（原值不变）

#### Scenario: PPID 中不含任何产品名形式
- **WHEN** `ppid = "ETCH_V02_SLOT01"`, `prodName = "XY1234AB"`
- **THEN** `NormalizePpid` 返回 `"ETCH_V02_SLOT01"`（原值不变）
