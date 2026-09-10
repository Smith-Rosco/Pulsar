# ADR-030: Root wheel radius single formula (kill the dual-path divergence)

**Status**: Accepted（2026-09-10 实施并经用户真机 sanity 确认：build 0/0、全量 1491/1491，N=10/12 中心环带命中 / 边缘槽命中 / 编辑器预览对照均通过）
**Date**: 2026-09-10
**Deciders**: Project owner (milo), pending grilling session
**Origin**: R1 遗留（`Docs/journal/NEXT.md` R1 条目「⚠ 已记录未修」；R1 引入 `WheelGeometry` 时收口了 (250,250) 硬编码，半径双路径留待本 ADR）

---

## Context

R1（ADR 见 journal 2026-09-09）把画布中心/默认槽径收口到 `WheelGeometry`（500 画布 / 250 中心 / 50 默认槽径），但**半径推导仍存在两条路径**，同一 N 产出不同半径：

### 两条路径

**Path A — `SlotLayoutEngine.CalculateOptimalLayout(slotCount)`**
半径用**默认槽径**（`WheelGeometry.DefaultSlotSize = 50`）：

```
radius_A = CalculateOptimalRadius(slotCount, slotSize: 50)
```

**Path B — `RadialMenuLayoutCoordinator.GetLayoutMetrics(...)`**
半径用**该槽位数的最优（缩放）槽径**：

```
slotSize_N = clamp(50 × (1 − (N−8)×0.04), 38, 60)
radius_B = CalculateOptimalRadius(slotCount, slotSize_N)
```

两条路径共用同一公式（`minRadius = (slotSize + 10) / (2·sin(π/N))`，clamp `[90, 180]`），仅 slotSize 入参不同。

### 数值分歧（精确计算）

| N | slotSize_N | r_A（Path A） | r_B（Path B） | dz_A（现运行时死区） | dz_B（应然死区） | 死区有效比率漂移 |
|---:|---:|---:|---:|---:|---:|---:|
| 6–8 | 50–54 | 90.00 | 90.00 | 54.00 | 54.00 | 0.6000（无分歧） |
| 9 | 48.00 | 90.00 | 90.00 | 54.45 | 54.45 | 0.6050（无分歧） |
| 10 | 46.00 | **97.08** | **90.61** | 59.22 | 55.27 | 0.6536 |
| 11 | 44.00 | 106.48 | 95.84 | 65.49 | 58.94 | 0.6833 |
| 12 | 42.00 | 115.91 | 100.46 | 71.86 | 62.28 | 0.7154 |

（dz = radius × deadZoneRatio；ratio：N≤8=0.60，N>8 每槽 +0.005，上限 0.65）

### 语义判断：Path B 是正确意图，Path A 是幽灵半径

缩放槽径（`CalculateOptimalSlotSize`）的存在意义就是 N>8 时收窄槽径防重叠；半径公式按「实际槽径 + 10px 间距」求最小不重叠半径。Path A 用 50px 默认槽径推导半径，但 N>8 时**实际渲染的槽径 < 50**——它给出的半径大于实际需要，是按一个不存在的槽径算出来的「幽灵半径」。

### 现实影响（三处，按严重度）

1. **运行时死区失真（MenuSession:2227 `GetDeadZoneRadius`）**：命中判定死区取自 Path A（`CalculateOptimalLayout(...).DeadZoneRadius`），而实际环半径 `_currentRadius` 来自 Path B。死区由 `radius × ratio` 派生，两个 radius 不同源 → N=10 时死区有效比率 0.6536（设计 0.61），N=12 漂到 0.7154。中心圆附近 55.27–59.22（N=10）的环形带被判为「中心」而非槽位，槽位有效命中区被吃掉一圈。`HitTestAt`（MenuSession:2092-2102）同时用 `_currentRadius`（Path B）与 `GetDeadZoneRadius()`（Path A）构造 HitTest 参数——**同一函数内两源混用**。
2. **编辑器预览失真（SlotWheelEditorViewModel:243 `ComputeLayout`）**：编辑器环半径用 Path A（97.08@N=10），而显示槽径用 `CalculateOptimalSlotSize`（Path B 语义）→ 编辑器自身半径/槽径就不同源，且与真实运行时环（90.61）不一致；编辑器内置 HitTest（:143）同样基于幽灵半径。
3. **认知负担**：R1 后 `SlotLayoutEngine.cs:23-26` 被迫写注释解释「radius 故意用默认槽径，缩放变体在 GetLayoutMetrics」——双源需要注释守卫，本身就说明契约模糊。

### 不受影响面

- **运行时环视觉：零变化**（Path B 胜出方案下，`GetLayoutMetrics` 语义不变）。
- **ADR-024 Fan/Ring 子菜单几何**：独立几何（R+gap=160、±30°、同心），不依赖根环半径路径。
- N≤9 的所有行为：两路径数值恒等。

---

## Decision

**Path B 语义为唯一权威：半径永远由「该槽位数的最优槽径」推导。**

1. `SlotLayoutEngine.CalculateOptimalLayout(slotCount)` 改为：

   ```csharp
   var slotSize = CalculateOptimalSlotSize(slotCount);
   var radius = CalculateOptimalRadius(slotCount, slotSize);
   ```

   （`CalculateOptimalRadius(slotCount, slotSize, baseRadius)` 公共签名不变——显式 slotSize 入参保留给需要覆盖的调用方；现无其它调用方。）

2. `GetLayoutMetrics` 不改代码：其「重算 slotSize 后调 CalculateOptimalRadius」与收敛后的 `CalculateOptimalLayout` 数值恒等。后续可（可选清理）改为直接读 `CalculateOptimalLayout(...)` 的字段，消除重算；本 ADR 不强制。

3. `GetDeadZoneRadius()` 代码不变（仍读 `CalculateOptimalLayout(...).DeadZoneRadius`），收敛后自动与 `_currentRadius` 同源。

4. `ISlotLayoutEngine` 接口注释同步：说明 radius 权威语义（optimal-slotSize 推导），替代 `SlotLayoutEngine.cs` 里的双路径守卫注释。

### 效果

| 消费方 | 收敛前 | 收敛后 |
|---|---|---|
| 运行时环（GetLayoutMetrics） | 90.61@N=10 | **不变** |
| 运行时死区（GetDeadZoneRadius） | 59.22@N=10（比率漂移 0.6536） | 55.27@N=10（比率还原 0.61） |
| 编辑器环半径（ComputeLayout） | 97.08@N=10 | 90.61@N=10（与运行时一致） |
| 编辑器显示槽径 | 46@N=10 | 不变（本就缩放） |

### 测试迁移

- `SlotLayoutEnginePoseSnapshotTests`：更新 N=10/12 的钉死值（97.08→90.61、115.91→100.46 及对应 deadZone）；N=8 钉死值不变；文件头注释中「固定 vs 缩放」双路径说明删除。
- `SlotLayoutEngineTests.CalculateOptimalLayout_ShouldReturnExpectedDeadZone_ForEightSlots`：N=8 数值不变，应原样通过。
- `RadialMenuLayoutCoordinatorTests`：GetLayoutMetrics 数值不变，应原样通过。
- 新增收敛断言：`CalculateOptimalLayout(N).Radius == GetLayoutMetrics(N, ...).Radius`（N∈[6,12] 全覆盖）——把「单源」钉进测试，防回潮。

---

## Alternatives Considered

**A. Path A 胜出（GetLayoutMetrics 改用默认槽径）**：运行时环半径变大（90.61→97.08@N=10），但实际槽径仍缩放——环变大了、槽没变大，间距虚胖，违背缩放槽径的防重叠初衷；且直接改变真机视觉，需要重新真机验收全部 N。**拒绝**。

**B. 参数化双路径并存（配置项选择）**：把分歧变成配置，等于把 bug 合法化，两源注释守卫继续存在。**拒绝**。

**C. 维持现状 + 注释守卫**：R1 已经证明这条路走不通（注释守卫仍需要本 ADR 来解释）。**拒绝**。

## Consequences

- 正向：死区与环半径同源（命中判定回到设计比率）；编辑器预览=运行时真值；单一公式单一真相，`SlotLayoutEngine` 注释守卫删除。
- 代价：N≥10 死区缩小 3.95–9.58px（10–12 档），中心命中带变窄——这是**回到设计意图**而非行为退化，但仍属可感知的交互变化，需真机 sanity（N=10 与 N=12 配置各一轮：中心环带命中、边缘槽命中、编辑器预览对照）。
- 测试：钉死值迁移 2 处 + 新增收敛断言；全量测试数 +1 左右。

## References

- R1 journal 条目（2026-09-09 14:15）与 NEXT.md「半径双路径分歧」记录
- `Pulsar/Pulsar/Services/SlotLayoutEngine.cs`（公式本体）· `ViewModels/RadialMenuLayoutCoordinator.cs`（Path B）· `ViewModels/MenuSession.cs:2092-2102,2227-2230`（死区混用点）· `ViewModels/Settings/SlotWheelEditorViewModel.cs:243-256`（编辑器）
- `Docs/lessons/` 无相关条目；ADR-024（子菜单几何，不受影响）
