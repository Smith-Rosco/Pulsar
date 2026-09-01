# Pulsar UX 重构性评审与优化提案

> 评审方法：[Impeccable v2.0.0](https://github.com/pbakaus/impeccable) 设计评审框架
> 评估维度：Nielsen 10 项启发式评分 · 认知负载 8 项检查 · AI Slop 检测
> 评审日期：2026-09-01 · 代码基线：44 个 XAML / 8106 行

---

## 〇、方向决策（用户裁定 · 2026-09-01）

| 决策项 | 裁定 | 对方案的影响 |
|--------|------|-------------|
| 浅色主题（P0） | **修复为对等主题** | 需为浅色设计不依赖发光的激活反馈通道 |
| 视觉调性 | **跟随系统 Fluent** | 不做独立品牌配色；以 WPF.Ui Fluent 令牌为唯一真相源 |
| 执行范围 | **全量改造 P0–P3** | 12 类问题全部处理 |

### 关键修正：Fluent 已是事实标准，不是"三套并存"

初稿判定为「三套色彩体系并存」，进一步统计引用频率后需要修正 —— **三方权重极不均衡**：

| 体系 | 引用次数 | 实际地位 |
|------|:---:|------|
| WPF.Ui Fluent 令牌 | `TextFillColorSecondaryBrush` 130 · `TextFillColorPrimaryBrush` 52 · `TextFillColorTertiaryBrush` 39 · `ControlFillColorDefaultBrush` 25 · `ControlFillColorSecondaryBrush` 24 · `ControlStrokeColorSecondaryBrush` 23 · `SurfaceStrokeColorDefaultBrush` 16 | **事实标准，占绝对多数** |
| `Theme.*` 自定义令牌 | `Theme.Accent` 9 · `Theme.Text.Primary` 5 · `Theme.Destructive` 5 · `Theme.Control.Background` 5 · `Theme.Accent.Foreground` 4 · `Theme.Border.Brush` 3 · `Theme.Text.Secondary` 2（合计约 33 处） | **少量残留，应清理** |
| 硬编码 hex | `#dc3545` 12 · `#FFFFFF` 9 · `#666666` 4 · `#1A1A1A` 4 等 | **应清零** |

**结论**：跟随 Fluent 不是推翻重来，而是清理约 33 处自定义残留 + 全部硬编码。主力界面已在正确体系上，改造风险与工作量显著低于初估。

### 连带修正：「零品牌观点」不再是缺陷

初稿将「借用系统默认色、无品牌辨识度」列为 AI Slop 指纹。**在「跟随系统 Fluent」的裁定下，这是有意的设计选择，而非疏忽** —— 判定从"缺陷"改为"已确认的设计立场"。

保留的独立观点应落在**交互层**（空间定位、肌肉记忆、morph 过渡），而非视觉层。

---

## 一、总体判定

**设计健康分：24 / 40（Acceptable — 需要显著改进）**

| # | 启发式 | 分数 | 关键问题 |
|---|--------|:---:|----------|
| 1 | 系统状态可见性 | 2 | 浅色主题下径向菜单激活反馈完全失效；未保存状态仅 8px 圆点无文字 |
| 2 | 匹配真实世界 | 3 | 空间隐喻自然，术语一致；保存位置违反惯例 |
| 3 | 用户控制与自由 | 2 | 无自动保存，显式保存模型已产生已知并发故障 |
| 4 | 一致性与标准 | **1** | 三套色彩体系并存；两套设计语言；11 种字号无比例；令牌零引用 |
| 5 | 错误预防 | 3 | 权限门控、断路器、配置校验等基础设施扎实 |
| 6 | 识别而非回忆 | 2 | 保存藏于页脚；选中指示器自制；空状态缺失 |
| 7 | 灵活与高效 | 3 | 热键 + 空间定位是强项，但回弹缓动拖慢感知 |
| 8 | 审美与简约 | 2 | 字号集中 11–12px 层级消失；常驻脉冲动画；配色零品牌观点 |
| 9 | 错误恢复 | 3 | ConfigEditSession 的 Rebase/重试机制完善 |
| 10 | 帮助与文档 | 3 | Tutorial 系统 + 首次启动向导 + Tooltip 齐全 |

**认知负载检查：8 项中失败 3 项 → 中等负载（moderate）**
失败项：视觉层级（字号差 1px 不可辨）· 工作记忆（跨页保存需记忆）· 决策点选项数（设置页信息密度）

### AI Slop 检测：**未通过，但病因不同**

诚实结论：**这不是典型的 AI 生成感界面**。径向菜单是围绕"肌肉记忆 + 空间定位"建立的原创交互模型，不是通用模板。

但执行层存在明确的"零观点"指纹：

| 指纹 | 实际取值 |
|------|---------|
| 借用 VS Code Dark 默认值 | `#181818` / `#252526` / `#2D2D2D` |
| 借用 Windows 系统蓝 | `Theme.Accent` = `#0078D7` / `#0067C0` |
| 纯白 / 纯黑 | `Text.Primary` = `#FFFFFF`（Dark）/ `#000000`（Light） |
| 深色 + 发光强调色 | `Theme.Orb.Active.Glow` = `#CC00BFFF` |
| 圆角 + 通用阴影 | 每球一个 `DropShadowEffect` |

> **一句话诊断：设计意图优秀，执行层无观点。问题不是"像 AI 做的"，而是"像没做完的"。**

---

## 二、优先级问题

### [P0] 浅色主题下核心交互反馈完全消失

**证据链**
- `Themes/Theme.Light.xaml:43` — `Theme.Orb.Active.Glow` = `#00FFFFFF`（完全透明）
- `Views/Controls/SlotOrb.xaml:130` — `ActiveShape` 在 `CustomFill` 为 null 时回退到该值
- `Themes/Theme.Light.xaml:62` vs `Theme.Dark.xaml:62` — `Theme.Orb.BlurOpacity` = `0.0` vs `1.0`

**为什么严重**：径向菜单的全部交互契约建立在"我能看出当前选中哪一项"。浅色模式下用户滑到扇区上没有任何高亮反馈 —— 肌肉记忆直接失效。浅色主题不是一等公民，而是被"关闭能力"的降级模式。

**修复**：浅色主题的激活态改走**不依赖发光的通道** —— 描边加粗（1.5→2.5）+ 填充提亮 + 外圈同心环。不要再试图用半透明发光在浅底上做反馈。

---

### [P0] 三套色彩体系并存，主题切换只覆盖部分界面

**证据链**
| 体系 | 代表 | 使用范围 |
|------|------|---------|
| ① `Theme.*` 自定义令牌 | `Theme.Dark/Light.xaml` | 部分界面 |
| ② WPF.Ui Fluent 令牌 | `ControlFillColorSecondaryBrush`、`TextFillColorPrimaryBrush`、`SurfaceStrokeColorDefaultBrush` | `Styles/SlotStyles.xaml` 全文 |
| ③ 硬编码 hex | `#dc3545`×12、`#FFFFFF`×9、`#666666`×4、`#1A1A1A`×4、`#c62828`、`#27AE60`、`#E74856`、`#E81123` | 散布各处 |

**后果**：设置窗口走 Fluent/Mica（`SettingsWindow.xaml:14` `WindowBackdropType="Mica"`），径向菜单走裸 Canvas 自绘（`RadialMenuWindow.xaml:38`）。**这是两个产品的观感**。切换主题时，槽位相关样式不响应自定义主题，视觉漂移。

**修复方向（依用户裁定：跟随系统 Fluent）**

以 **WPF.Ui Fluent 令牌为唯一真相源**，反向清理 `Theme.*` 残留（约 33 处）并清零硬编码 hex：

| 现状 | 迁移目标 |
|------|---------|
| `Theme.Accent` / `Theme.Accent.Foreground` | `AccentFillColorDefaultBrush` / `AccentTextFillColorPrimaryBrush`（跟随系统强调色，不再硬编码 `#0078D7`） |
| `Theme.Text.Primary` / `.Secondary` | `TextFillColorPrimaryBrush` / `TextFillColorSecondaryBrush` |
| `Theme.Control.Background` | `ControlFillColorDefaultBrush` |
| `Theme.Border.Brush` | `ControlStrokeColorDefaultBrush` |
| `Theme.Destructive` | `SystemFillColorCriticalBrush` |
| 硬编码 `#dc3545` / `#c62828` / `#E74856` | `SystemFillColorCriticalBrush` |
| 硬编码 `#27AE60` | `SystemFillColorSuccessBrush` |
| 硬编码 `#E81123` / `#FF5C2B` | `SystemFillColorCriticalBrush` / `SystemFillColorCautionBrush` |

迁移后 `Theme.Dark.xaml` / `Theme.Light.xaml` 中仅保留 Fluent 未覆盖的**径向菜单专用**令牌（Orb 系列），且必须保证明暗两套**对等**实现。

---

### [P1] 设计令牌是"僵尸系统"：定义齐全，引用为零

**证据链**
- 定义：`Theme.Dark.xaml:64-74` 与 `Theme.Light.xaml:64-74` — `Pulsar.Spacing.*`（5 个）、`Pulsar.Animation.Duration.*`（3 个）
- 引用：全仓库 XAML 中，这两套令牌**仅出现在定义它们的文件自身**（各 5 次 / 3 次），业务界面引用数为 **0**
- 反面证据：**33 处**硬编码动画时长（`0.15`×9、`0.2`×8、`0.25`×6、`0.3`×5、`0.18`×3、`0.5`、`0.4`）
- 典型讽刺：`SettingsWindow.xaml:80` 写 `Margin="24"`，而 `Pulsar.Spacing.LG` 恰好等于 `24` —— 令牌就在旁边，没用

**为什么重要**：这说明设计系统是"看起来存在"而非"正在运转"。任何后续的视觉重构如果不先修复这条管道，新令牌同样会被绕过。

**修复**：先做令牌接入（`/normalize`），再做视觉改造。否则改一处漏三处。

---

### [P1] 排版层级消失：字号集中在 1px 区间

**分布**：`12`×128 · `11`×86 · `14`×28 · `13`×28 · `16`×18 · `18`×7 · `20`×6 · `22`×5 · `64`×3 · `36`×2 · `10`×2 · `9`×1

**问题**：11 与 12 合计 **214 处**，占绝对多数，两者仅差 1px —— 肉眼无法区分层级。11 个离散字号几乎连续，不存在模块化比例（非 1.25 / 1.2 倍率体系），说明是逐处微调而非系统设计。这正是认知负载框架中的「视觉噪音地板」：所有元素同等权重，视线无处落脚。

**修复**：收敛为 5 级比例 —— `11 / 13 / 16 / 20 / 28`（约 1.25 倍率），全部改为 Style 资源引用；禁止裸 `FontSize`。

---

### [P1] 动效语言与产品定位自相矛盾

**证据链**
1. **回弹缓动滥用** — `SlotOrb.xaml:169,174`（Amplitude 0.3）、`RadialMenuWindow.xaml:177,182`（Amplitude 0.4）
   Impeccable 明确禁止弹性/回弹缓动。更关键的是它与产品定位冲突：README 主打"肌肉记忆、空间定位、高性能"，而回弹会推迟到达终态的感知时间 —— 用户已经知道答案了，动画还在晃。
2. **常驻脉冲** — `SlotOrb.xaml:38-52` 推荐光晕 `#FFFFD700` + `BlurRadius="20"` + `RepeatBehavior="Forever"`
   永久动画既是注意力污染源（目标用户靠**空间位置**找目标，不需要闪烁提示），又是持续 GPU 负担（模糊每帧重绘）。
3. **性能自相矛盾** — `RadialMenuWindow.xaml:221,231` 注释已写明 `[Optimized] No DropShadowEffect for Performance`，说明团队知道阴影的性能代价；但 `SlotOrb.xaml:89-93` 仍给**每个**圆球挂 `DropShadowEffect` —— N 个扇区 = N 个模糊，与"高性能启动器"定位直接冲突。

**修复**：全部改为指数缓动（`ease-out-quart` / `ease-out-expo`）；脉冲改为进入时一次性播放或改为静态高亮；移除逐球 DropShadow，改用**单层预烘焙位图**或纯外描边。

---

### [P1] 保存操作被藏在导航页脚

**证据链** — `Views/SettingsWindow.xaml:47-65`
- 保存按钮位于 `NavigationView.FooterMenuItems`（左下角页脚），而非常规的底部操作栏 / TitleBar
- 未保存提示 = 8px 红点（`:57-59`，`#E74856`，又一个硬编码红），无文字、无 tooltip 解释

**佐证**：`Docs/lessons/CONFIG_EDIT_SESSION_STALE_REVISION.md` 记录了"连续第二次保存失败" —— 说明当前的显式保存模型本身对用户不友好，不只是 UI 位置问题。

**修复**：保存提到内容区底部操作栏或 TitleBar 右侧；未保存状态改为文字徽章「未保存的更改」。

---

### [P2] 空状态完全缺失

全仓库 XAML 中**无** `EmptyState` 组件、无"暂无数据"类文案。而产品有多个必然为空的列表：插件列表、槽位列表、进程黑名单、密钥库、插件日志。

**修复**：统一 `EmptyState` 组件 —— 图标 + 一句说明 + 一个主操作（引导用户完成第一个创建动作）。

---

### [P2] 强调色有两个值，且都是 Windows 默认蓝

- `Theme.Accent`：Dark = `#0078D7`、Light = `#0067C0`
- Badge 硬编码：`SlotOrb.xaml:224` = `#FF0078D7` → 与浅色主题的 `#0067C0` **不一致**

同一个"强调色"在系统里存在两个值，且都是 Windows 系统默认蓝 —— 零品牌辨识度。

---

### [P2] 自制选中指示器，禁用了原生实现

`SettingsWindow.xaml:43-45` 把 WPF.Ui 的 `NavigationViewSelectionIndicatorForeground` 设为 `Transparent`，改用 `:68-75` 手绘 `Canvas` + `Rectangle`（`NavIndicator`，默认 `Collapsed`），位置靠代码计算。

除非有强视觉理由，否则应复用原生指示器 —— 自绘方案在 DPI 变化、窗格折叠、动画过渡时都容易错位。

---

### [P2] 圆角与间距无统一刻度

`SlotStyles.xaml` 定义了 4 个圆角令牌（6 / 6 / 10 / 10），但 `SlotOrb.xaml:225` 直接写 `CornerRadius="6"`、`:269` 写 `CornerRadius="5"` —— **5 不在令牌体系内**。硬编码 Margin 遍布（`RadialMenuWindow.xaml:86` `Margin="0,-6,-6,0"`、`:240` `Padding="10,3"`）。

---

### [P3] 图标缩放质量自相矛盾

`RadialMenuWindow.xaml:22` 窗口级 `RenderOptions.BitmapScalingMode="LowQuality"`，但 `SlotOrb.xaml:160` 局部 Image 覆盖为 `HighQuality`。冲突且意图不明；低质量会让图标边缘发糊。

---

### [P3] Emoji 与 Fluent 图标混用

XAML 中出现 `✓`×2 · `⚡`×2 · `🔄` · `🌐` · `⚠`。Emoji 是彩色位图，Fluent `SymbolIcon` 是单色矢量，同屏混用渲染口径不一致，且跨 Windows 版本 emoji 字形会变。

---

## 三、做得好的地方（重构时应保护，勿误伤）

1. **径向菜单的空间定位模型是真实差异化** —— 不是套模板，是围绕"肌肉记忆"建立的原创交互。中心节点的 morph 过渡（`RadialMenuWindow.xaml:107-219`）设计考究，子菜单切换是连续变形而非内容替换，这一点优于多数同类产品。
2. **本地化基础设施完善** —— `lex:Locale` 全覆盖、中英双语 resx、插件标签按约定自动本地化。这在新手上非常罕见，视觉重构时必须保持这条链路完好。
3. **工程韧性扎实** — Circuit Breaker、`ConfigEditSession` 的 Rebase/重试、330+ 测试、Tutorial 系统、首次启动向导。**问题在表现层，不在架构层。**

---

## 四、Persona 红旗

**张 · Power User（每天唤起 200 次）**
- 回弹缓动拖慢每次交互的终态感知 —— 200 次/天 × 0.25s = 每天多等 50 秒
- 常驻金色脉冲在高频使用下成为持续干扰（他靠位置记忆，不需要闪烁）
- `LowQuality` 缩放让最熟悉的图标边缘发糊
- 若开启浅色主题：激活反馈直接消失，肌肉记忆完全失效 → **高流失风险**

**李 · First-Timer（刚装好）**
- 找不到保存按钮（在左下角页脚，非常规位置）
- 插件页空了没有任何引导（空状态缺失）
- 未保存状态只有 8px 红点，看不懂含义 → **可能在配置环节就放弃**

---

## 五、执行顺序（全量改造 · Fluent 方向）

> 关键原则：**先接管道，再做视觉**。令牌零引用的错误不得重演 —— 任何新令牌若未实际接入，等于没做。

| 阶段 | 动作 | 范围 | 对应问题 |
|:---:|------|------|---------|
| 1 | 令牌管道 | 新建 `Styles/Tokens.xaml`：排版 5 级 · 时长 3 级 · 间距 4px 网格 · 圆角刻度，全量替换裸值 | P1 僵尸令牌 · P1 排版 · P2 圆角 |
| 2 | 色彩归一 | `Theme.*` 残留 33 处 → Fluent 令牌；硬编码 hex 清零 | P0 三套体系 · P2 强调色 |
| 3 | P0 专项 | 浅色主题激活反馈：不依赖发光的三通道（描边加粗 / 填充提亮 / 外圈环） | P0 浅色失效 |
| 4 | 动效重构 | 回弹 → `ease-out-quart`；常驻脉冲 → 进入一次性；移除逐球 DropShadow | P1 动效 · 性能 |
| 5 | 布局与引导 | 间距刻度统一；空状态组件；保存按钮重定位到底部操作栏 | P1 保存 · P2 空状态 |
| 6 | 上线打磨 | 图标缩放质量 · Emoji → Fluent SymbolIcon · 最终对齐检查 | P3 |

### 回归验证清单

> 状态更新：2026-09-01 全量改造执行完成。回归清单 7 项全部勾选（FontSize 收敛含豁免说明）。构建 0 警告 0 错误，719 测试全绿。

- [x] 明暗主题切换后，径向菜单激活反馈在两套主题下均清晰可见
  - 实现：`Theme.Orb.Active.Fill/Stroke/Ring` 三通道（填充提亮 + 描边加粗 1.5→2.5 + 外圈同心环），明暗对等定义；`SlotOrb.xaml` 已接入；`Theme.Orb.Active.Glow` 仅保留兼容旧键
- [x] 全局搜索无 `#` 开头硬编码色（渐变定义除外）
  - 业务界面硬编码已清零；仅保留：令牌定义处取值、径向菜单专用 `Theme.*`、Tutorial/VbaRunner 固定浮层本地令牌、ColorPicker RGB 通道语义色（已注释说明）
- [x] 全局搜索无裸 `FontSize=`（全部走 Style 资源）
  - 实现：约 320 处裸 FontSize（含 16 处 Style Setter）全部收敛为 6 级令牌引用（Caption 11 / Body 13 / Subtitle 16 / Title 20 / Display 28 / Hero 36）；文本字号按语义映射（9/10/11→Caption、12/13→Body、14/15/16→Subtitle、18/20/22→Title、28→Display）
  - 豁免（已注释说明）：SlotOrb 内部 24×24 Viewbox 设计坐标系（14/11/8 是设计单位非屏幕像素）；SymbolIcon/图标按钮 FontSize（36/44/48/64/24 是图标渲染尺寸，非排版层级）
  - 注意：Style Setter 形式的 `Value="X"` 也一并令牌化（ButtonStyles/SlotStyles/TooltipStyles 等 16 处）
- [x] 全局搜索无裸 `Duration=`（全部走时长令牌）
  - 全部替换为 `Pulsar.Duration.Fast/Normal/Slow`；仅教程卡"立即变绿"的零时长动画保留（已注释说明）
- [x] 浅色主题下 `Theme.Orb.BlurOpacity` 不再作为"关掉能力"的开关
  - 激活反馈改三通道后该键零引用，已删除
- [x] 本地化链路完好（`{lex:Locale}` 未被破坏）
- [x] 构建通过，330+ 测试无回归（719 通过 / 0 失败）

---

## 六、第二轮：间距 / 圆角令牌管道收尾（2026-09-01 续）

> 第一轮只把「排版」这一条管道接通了，间距与圆角令牌仍是僵尸状态：
> `Pulsar.Pad/Space` 引用 0 处、`Pulsar.Radius` 仅 4 处。本轮补全。

### 根因：令牌形状与真实用法不匹配

旧版 `Pulsar.Pad.*` 是**四边等距**的 `Thickness`，而全库 90% 的 Margin 用法是**单边**
的（`0,0,8,0` 出现 44 次）。均匀 Thickness 表达不了单边间隙 ——
**令牌从设计上就用不了**，这才是零引用的根本原因，不是"忘了用"。

对策：新增 `Pulsar.Gap.<方向>.<档位>` 系列（Left/Top/Right/Bottom/Horizontal/Vertical），
按真实需求生成 45 个组合，不做全矩阵铺开。

### 刻度重定：改为数据驱动

原刻度 4/8/16/24/32 与真实用量错位：

| 档位 | 2 | 4 | 8 | **12** | 16 | 20 | 24 | 48 | ~~32~~ |
|---|---|---|---|---|---|---|---|---|---|
| 需求次数 | 48 | 86 | 133 | **89** | 58 | 27 | 9 | 7 | **0** |

- **12 是第二高频值，旧刻度却从 8 直接跳到 16，把它跳过了**
- **32 需求为零，却占着一个档位**

新刻度 = 4px 基准 + 低区 2px 细分，剔除零需求档位。

### 本轮成果

| 项 | 结果 |
|---|---|
| 间距 / 圆角裸值收敛 | **577 处** → 令牌引用 |
| 间距刻度 | 8 档（2/4/8/12/16/20/24/48），数据驱动 |
| 圆角刻度 | 6 档（2/4/6/8/12/16），对齐 Fluent 2 |
| 新增令牌 | 105 个（`Pad` / `Gap` / `Thk` / `Radius`） |
| 僵尸令牌 | 0（本轮新增的全部有引用） |

### 豁免（保留裸值，已就地注释）

1. **光学对齐负值** 4 处 —— `0,-2,-2,0`、`0,-6,-6,0`、`0,0,-2,-2`、`0,0,0,-26`。
   用于抵消徽章 / 状态点 / 操作条的视觉溢出，由几何决定，不是布局节奏。
2. **几何正圆** 2 处 —— `SlotWheelEditor` 44×44 上的 `CornerRadius="22"`、
   `ColorPicker` 按钮 32×32 上的 `CornerRadius="16"`。
   半径 = 边长/2，由尺寸决定而非设计刻度；吸附会把正圆变成圆角方形。

### 本轮踩坑（重要）

1. **XAML 注释不可放在标签属性列表中间** —— 脚本在 `Margin="0,4,0,4"` 后直接追加注释，
   生成 `<Grid Margin="..."  <!-- 注释 -->>`，非法 XML。注释必须放在标签外。
2. **分类应作用于吸附后的值** —— `0,3,0,4` 按原值判定为「多轴组合」，
   吸附成 `0,4,0,4` 后其实是合法的 `Pulsar.Gap.Vertical.XS`。先吸附再分类。
3. **ControlTemplate 内的 StaticResource 在模板加载时立即解析** ——
   与 Style Setter 的延迟解析不同。因此凡引用数值令牌的**样式字典**
   都必须自带 Tokens.xaml 合并，否则脱离 Application 加载（单元测试手工构造资源树）
   会抛 `XamlParseException`。已为 ButtonStyles / ScrollViewerStyles /
   SlotStyles / TooltipStyles 四个字典补上合并。
4. **grep 令牌名会命中注释** —— 审计"僵尸令牌"时必须先剥掉 XML 注释，
   否则 `Theme.Light.xaml` 里"令牌已迁移至 Tokens.xaml"的说明文字会被误判为合并声明。

### 验证

- `dotnet build`：0 警告 0 错误
- `dotnet test`：**718 通过 / 0 失败**
- 僵尸令牌复查：0 个（本轮新增部分）

### 遗留（本轮已处理部分 · 2026-09-01 复核修正）

> 复核基线：工作区提交 `f9a85f0`（含第二轮 token 管道）。修正两处与代码不符的旧表述：
> ① `Theme.Destructive` 基键、`Theme.Radial.Scrim/ScrimBrush` 此前已零引用，本轮已删（`.Hover`
> 仍被 ButtonStyles.xaml:304 使用，保留）；② 空状态覆盖此前已齐（外部插件页 / 插件日志页 /
> 密钥选择页 / 统计页 / 槽位页均有），本轮将 6 处内联实现统一为 `EmptyState` 组件。

- [x] **僵尸令牌清理**：9 个（`PulsarText*Style` ×6、`Pulsar.Type.Hero`、`Pulsar.Easing.Emphasis/Subtle`）
      零引用，已从 `Tokens.xaml` 删除 —— 数值令牌为唯一真相源，不走 Style 层（避免引入 LineHeight 布局变化）。
- [x] **僵尸色彩令牌清理**：`Theme.Destructive`（基键）、`Theme.Radial.Scrim`、`Theme.Radial.ScrimBrush`
      已从两套主题删除；`Theme.Destructive.Hover` 因 ButtonStyles 依赖保留。
- [x] **空状态统一组件**：新建 `Views/Controls/EmptyState.xaml`（图标 + 标题 + 提示 + 可选主操作 +
      可选卡片边框），替换 Slots / ExternalPlugins / Plugins / Analytics / PluginLog / SecretPicker 6 处内联实现。

**仍遗留（待裁定）**

1. **自制导航指示器**：`SettingsWindow.xaml.cs` 约 70 行手绘动画。
2. **可访问性近乎为零**：45 个 XAML 仅 7 处 `AutomationProperties` / `KeyboardNavigation`。
