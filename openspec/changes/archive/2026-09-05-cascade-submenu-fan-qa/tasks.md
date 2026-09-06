# Tasks — Cascade SubMenu Fan QA 收尾

> **收口（2026-09-06 16:1x）**：ADR-024 系列（D1/D1a/D6/D7/D10/D11）已落地并 push（`3f68c13`）；E2E `fan-sector-constraint-2` / `ring-center-visible-4` PASS；全量 **1129/1129**、build 0 新警告。3.3 位置参数草案已被 ADR-024 D1/D1a/D6 取代（本清单关闭，草案留档）；2.x 人工 QA 仍待用户真机（E2E 已部分替代），QA 结果另行记录。本 change 归档。
>
> **执行状态（2026-09-05 21:4x，QA 暂停点 · 位置问题分析已推进）**
> - 已完成：1.1 / 1.2（QA 准备）、3.1（Fan 方向几何缺陷修复 + 6 个回归测试，全量 1066/1066 绿）。
> - 进行中：2.x 人工 QA——A 组修复后重验**部分通过**（Fan2/3 子项可见且方向正确），用户反馈「位置仍有问题」，B–H 未执行。
> - **新增（2026-09-05 21:4x，未动代码/未跑 E2E，用户在打游戏）**：ADR-011 决策 6 修订**草案**已产出——`submenu-radius-geometry-amendment-draft.md`。纯几何量化钉死了位置问题的客观基线：Fan3 相邻翼距仅 27.95 DIP（槽位 50 → 重叠 22）；子项与中心圆重叠 6 DIP；子环与根环槽位带径向重叠 14 DIP；并发现「避开中心圆需 r≥60 且避开根环槽位带需 r≤40」的可行域矛盾，Fan3 结构性需 r≥96.6（根环外侧）。推荐方案 A（Fan 移根环外侧 r≈120），Ring 视 QA 现象再定。
> - 待办：① 用户 QA 按下轮截图指引（草案 §5）拍 4 张现象照 → ② 草案定稿 → ADR-011 追加 Amendment 小节 → ③ 实施（Fan/Ring 半径参数化 + 新增"不重叠"回归断言）→ ④ A 组余项（A3/A4）与 B–H 全部用例 → ⑤ 3.2 视 2.4 结果 → ⑥ 4.1–4.3 收尾。
> - 环境注意：人工 QA 必须用**真实配置的正常实例**（`--ui-debug` 不装鼠标钩子，无鼠标交互）；fixture 载入/还原步骤见 `qa-checklist.md` 准备节。

## 1. QA 准备

- [x] 1.1 编写 `qa-checklist.md`：用例矩阵（下 §2）+ 通过标准 + Change History（沿用 renderer-plugin-registry 格式）
- [x] 1.2 准备 fixture：配置一个含 2 个子动作、一个含 3 个子动作、一个含 5 个子动作的测试 slot（5 项用于 Ring 回落 + 分页）；确认 Dark/Light 可切换环境
      - `Pulsar/Pulsar.E2E/Fixtures/cascade-submenu-fan-qa.json`（默认 `slotsPerPage: 8`）
      - `Pulsar/Pulsar.E2E/Fixtures/cascade-submenu-fan-qa-paged.json`（`slotsPerPage: 4`，供 2.4 分页用例：`SlotsPerPage` 为子环共享页大小，5 子项需降到 4 才分页）
      - 载入方式：备份 `%APPDATA%\Pulsar\Profiles.json` 后拷入 fixture，以**正常实例**（无 `--ui-debug`）启动——debug 实例无鼠标交互，人工 QA 不可用（详见 qa-checklist 准备节）；验完用备份还原
      - Dark/Light 切换入口：设置 → 通用 → 外观 → 主题（已确认可切换）

## 2. 人工 QA 执行（requires human）

- [ ] 2.1 Fan 渲染：2 子项（上下翼）/ 3 子项（三角）位置正确，父扇区方向展开，标签完整可读
- [ ] 2.2 Fan 命中：滑入子项即高亮、松开触发对应动作；死区与扇区外不误触（对照 `HitTestFan` 语义）
- [ ] 2.3 Ring 回落：4+ 子项自动回落 Ring 同心子环，内外环带命中正确
- [ ] 2.4 分页叠加：子项超页时翻页可用，页状态独立（ADR-011），翻页后命中按当前页作用域
- [ ] 2.5 双主题：Dark / Light 下子环渲染、高亮、文字对比度均正常
- [ ] 2.6 高 DPI：150% 缩放下 Fan/Ring 命中无偏移（验证「纯几何与像素无关」假设）
- [ ] 2.7 外甩取消：二级展开时顺势外甩 → 以根环为基准取消，无残留状态
- [ ] 2.8 回归路径：窗口切换子菜单（`WindowSwitchSubMenuStrategy`）行为不变

## 3. 缺陷修复（条件性，按 QA 结果执行）

- [x] 3.1 命中/渲染类缺陷：修复 + 回归测试（加入 `SubMenuLayoutEngineTests` / `CascadeSubMenuEntryTests` 家族），行为回归 `cascade-submenu-layout` spec
      - 缺陷 A1（2026-09-05 人工 QA）：`SubMenuLayoutEngine.ComputeChildPositions` 的 Fan 分支把相对翼角当绝对角用（漏加 `DirectionRadians`），子项全部落到画布右侧 ±30°/0°，与父方向无关；且与 `HitTestFan`（正确减去 DirectionRadians 后比较）不一致——布局与命中在非朝东父槽位上全面分家。Fan2/3 的子项叠进右侧根环槽位（观感「消失」），Ring5 因 Ring 分支正确而位置对，但与用户「形状都是圆形」的观感吻合。
      - 修复：Fan 分支翼角加 `DirectionRadians`（`Services/SubMenuLayoutEngine.cs`）。
      - 回归测试：`SubMenuLayoutEngineTests` 新增 `ComputeChildPositions_Fan_ShouldRespectParentDirection`（direction=−90° 时三翼落 −120°/−90°/−60°）与 `Fan_LayoutAndHitTest_ShouldAgreeAtEveryChildCenter`（四个父方向 × 1–3 子项，布局输出位置命中必须回到自身）；新增 `CascadeSubMenuLayoutRuntimeTests`（真引擎 + 真 MenuSession：Fan2 子项落小环、Ring5 均布小环、filler 留根环）。
      - 全量 1066/1066 通过（原基线 1060 + 6）。
- [ ] 3.2 若涉及分页语义调整：补 ADR-011 Amendment 后再改代码
- [x] ~~3.3 位置参数修订（ADR-011 决策 6，草案见 `submenu-radius-geometry-amendment-draft.md`）~~ **已关闭（2026-09-06）**：草案方案 A（Fan 移根环外侧 r≈120）被 ADR-024 D1（Fan 同心外弧 `R+gap=160`，重叠结构性不可能）+ D1a（扇区约束）+ D6（gap 压缩）取代并经 E2E 验证；草案留档不适用。原计划「新增子项互不重叠/不撞中心圆回归断言」已由 ADR-024 各轮布局/命中测试覆盖。
- [x] 3.4 **Fan 命中扇区约束修复（验证通过）**：用户实测「子 slot 沿父方向渲染（如右下），但鼠标在中心槽附近即触发」→ 根因 `SubMenuLayoutEngine.HitTestFan` 无角度扇区约束，`[deadZone, fanExtent]` 全圆周按最近翼角命中；修复为翼只在自己的几何扇区内命中（2 翼 ±30° / 3 翼 ±15° / 单翼 ±30°），扇区外返回 -1。已改 `Services/SubMenuLayoutEngine.cs` + 新增 3 个回归测试（`OutsideWingSector` / `NearCenter_OffWingDirection`（用户场景）/ `InsideWingSector`）。**验证：全量 1095/1095 通过（原 1092+3），构建 0 警告 0 错误**。spec 语义补全：`cascade-submenu-layout` 「Fan hit test picks nearest wing」未限定角度范围，本修复按 QA B1/B4（指哪亮哪/扇区外不误触）补齐

## 4. 文档收口 & 验证

- [x] ~~4.1 `Docs/roadmap/IMPLEMENTATION_VERIFICATION.md`：未落地清单 #1 划线并注明 QA 结果~~（2026-09-06 收口：已划线——方向三 🟢 全部落地、E2E `fan-sector-constraint-2`/`ring-center-visible-4` PASS、全量 1129/1129；仅剩人工 QA 2.x）
- [x] ~~4.2 journal 记录 QA 结论与任何修复~~（2026-09-06 收口：journal 14:15/14:45/15:00/15:38/16:06 会话块已记录全部 QA 结论与修复，本次收口另记）
- [x] ~~4.3 `scripts/dev.ps1 build` → 0 警告 0 错误；`scripts/dev.ps1 test` → 全量通过~~（2026-09-06：build 无新增警告，**全量 1129/1129** 通过）
