# 动态标签页（Dynamic Settings Tabs）调研报告

> 日期：2026-09-08 ｜ 状态：调研 + 设计拷问（grill）输入稿
> 范围：设置窗口侧边栏动态增减标签页的业界做法、Pulsar 现状差距、方案建议
> 本文档不修改任何源码。

---

## 1. 主流软件的做法

### 1.1 VS Code — Preview Tab（预览标签）⭐ 最直接的参照

- 单击文件 → 以**斜体预览标签**打开，**复用同一个预览槽位**：再点别的文件，预览标签被替换，Tab 数量不增长。
- 开始编辑 或 双击 → 预览标签**晋升为常驻标签**（斜体消失，不再被替换）。
- 可全局关闭（`workbench.editor.enablePreview`）。
- 关键洞察：**"临时性"是标签的属性，不是窗口的属性**；用"每类只允许一个临时实例 + 脏状态自动晋升"来保证 Tab 数量有界。

### 1.2 Raycast — 每个可配置对象一个侧边栏条目 ⭐ 与 Pulsar 场景最像

- 单一设置窗口，左侧 Tab 列表；**每个 extension / command 的配置就是一个独立 Tab**（"Configure Extension" 直接跳入）。
- 设置支持**子视图（sub-views）**：复杂配置（如 AI Settings）拆成子页；处于子视图时点击侧边栏父项返回父级。
- 支持从 Action Panel **深链直达**某条配置（上下文入口），不必先开设置再找。
- 新增 `Ctrl+F` 设置内搜索，搜索结果直达具体配置。

### 1.3 PowerToys Command Palette

- 单窗口、分区设置页；`Backspace 返回` 做**钻入式（drill-in）导航**；上下文入口深链。模态只用于确认类交互。

### 1.4 JetBrains / Office / Unity / Blender — Inspector 模式

- 复杂配置走**上下文属性面板**（选中什么，右侧显示什么），而非模态框；模态只保留给"快速确认 / 破坏性操作"。
- 教训：**配置面板跟随"正在编辑的对象"**，而不是让用户去设置窗口里找。

### 1.5 SaaS 设置架构共识（techinterview / designpixil 等）

- **设置注册表（settings registry）**：声明式定义（id / type / category / label / visibility），渲染器遍历注册表——加一个配置 = 加一条声明，不是改五个文件。
- **深链是硬需求**：配置项可被直接定位（含高亮）。
- **避免用手风琴（accordion）承载导航**："视觉上承诺了简单，随增长必然崩坏"——这正是 General 页 CardExpander 的现状。
- 侧边栏适合 8+ 分区；**hide vs disable**：用户完全无法理解的配置隐藏，相关但受限的禁用 + 提示。
- 保存模式：低风险自动保存 / 高风险显式保存（Pulsar 已是后者：底部保存按钮 + InfoBadge）。

---

## 2. Pulsar 项目现状（探索事实）

| # | 事实 | 位置 |
|---|---|---|
| F1 | 设置窗口 = `ui:NavigationView` + `Frame`；导航项**已经是在 code-behind 遍历页面目录动态生成**的 | `Views/SettingsWindow.xaml` L43-95、`SettingsWindow.xaml.cs` L104-154 |
| F2 | 页面目录硬编码 5 页（Slots/Plugins/General/Analytics/About），工厂 `switch` 硬编码 | `Services/SettingsPageCatalog.cs` L43-50、`Services/SettingsPageFactory.cs` L32-43 |
| F3 | 右键手势配置塞在 General 页一个 CardExpander 里：约 30+ 控件、9 个 SettingsRow、最深 7 层嵌套、184 行 XAML 片段 | `Views/Pages/SettingsGeneralPage.xaml` L177-360 |
| F4 | "外观"只有 4 行（主题/渲染器/预设/语言），混在 General 页 | 同上 L53-123 |
| F5 | 重配置走模态：18 个 DataTemplate，SlotEditorContent 57KB、PluginSettingsDialog、ProcessBlacklist 等 | `Themes/DialogTemplates.xaml` L12-78、`Views/Dialogs/Contents/` |
| F6 | 持久化单写入点 `SettingsEditorSession`（经 `ConfigEditSession`），`MarkDirty()` → 窗口级保存按钮 + InfoBadge | `ViewModels/Settings/SettingsEditorSession.cs` L18-20 |
| F7 | **没有页级导航生命周期钩子**（无 INavigationAware）；页面缓存在 `_pages` 字典不销毁；导航离开有 `SettingsNavigationGuard` 拦截 | `SettingsWindow.xaml.cs` L38/L506-541、`Services/SettingsNavigationGuard.cs` L18-39 |
| F8 | 已有动态渲染先例：`PluginSettingTemplateSelector` 按类型渲染 7 种设置控件；插件页大量 `ExpandableCard` + ObservableCollection | `Views/TemplateSelectors/PluginSettingTemplateSelector.cs` |

**差距结论**：
- 动态标签页的**基础设施大半已存在**（动态导航项生成、Frame 承载、guard 拦截、单写入会话）。缺的是：① 注册表从"静态数组"变"可变集合"；② 工厂支持运行时注册的页类型；③ 页级生命周期钩子；④ 临时页的视觉语义与回收策略。
- F3 证实了"卡片超载"判断：手势配置的复杂度（条件可见性、嵌套子面板、冲突警告）已明显超出 CardExpander 的表达力。
- 模态清单（F5）里真正"重配置"的是少数（SlotEditor、PluginSettings、ProcessBlacklist、InputProfile），多数模态是合理的一次性确认/选择器。

---

## 3. 方案建议（供 grill 拷问的初稿）

### 3.1 核心模型：借 VS Code Preview Tab 语义

- 侧边栏条目分两类：**常驻页**（现有 5 页）与**临时页（transient）**。
- 临时页规则：
  1. **按类型单例**：同一类临时页同时只存在一个实例；再次触发 = 激活已有实例（而非新建）→ 直接回答"重复触发导致 Tab 膨胀"。
  2. **视觉区分**：标题斜体（或后缀圆点/弱色），标示"临时"。
  3. **晋升**：用户点 Tab 上的 📌（或在页内做出修改）→ 转常驻；**脏状态强制晋升**（有未保存修改的临时页不允许被静默回收，走 `SettingsNavigationGuard`）。
  4. **回收**：干净（无未保存修改）的临时页在 ① 用户关闭 Tab，或 ② 离开设置窗口时自动回收。
- 实体编辑类临时页（如未来把槽位编辑迁入）例外：按**实体 id** 单例（`slot-editor:<slotId>`），同 VS Code 的文件语义。

### 3.2 架构落点（最小改动路径）

| 改动 | 内容 | 涉及 |
|---|---|---|
| A | `SettingsPageRegistration` 增加 `IsTransient` + `OwnerId`（触发来源）；目录从硬编码数组改为可观察集合 | `SettingsPageCatalog` / `SettingsPageRegistration` |
| B | `SettingsPageFactory` 从 switch 改为注册表字典（`Func<Page>` 按注册） | `SettingsPageFactory` |
| C | 新增 `ISettingsPageLifecycle`（`OnNavigatedToAsync` / `OnNavigatingFromAsync`），挂接现有 `SettingsNavigationGuard` | 新接口 + `SettingsWindow` 导航路径 |
| D | 导航项增删：`ObservableCollection` 驱动 `BuildNavigationItems()`（现已是代码生成，改造小）；临时页插到分组尾部、带关闭按钮 | `SettingsWindow.xaml.cs` L104-154 |
| E | 触发点：CardExpander header 加"编辑详情"按钮 → `OpenTransientPage("gesture")`；插件设置从模态迁临时页同理 | `SettingsGeneralPage` 等 |

### 3.3 迁移路线（P0–P3）

| 优先级 | 内容 | 理由 |
|---|---|---|
| **P0** | 机制落地 + 右键手势配置迁出 General 页 → 临时页 | 复杂度超载最严重（F3），收益最大 |
| **P1** | 外观升级为独立常驻页（主题预设网格 + 渲染器预览），General 瘦身 | 外观配置"过于简单"的诉求；常驻而非临时（高频访问） |
| **P2** | PluginSettings / ProcessBlacklist 从模态迁临时页（插件页卡片触发） | 模态打断工作流最明显的两处 |
| **P3** | 槽位编辑器（SlotEditor 57KB）评估迁 `slot-editor:<id>` 临时页 | 收益大但风险高（模态内状态最重），单独立项 |
| 保留 | 一次性确认/选择器模态（IconPicker、SecretPicker、确认框等） | Inspector 不是模态的替代品，两类各司其职 |

### 3.4 需要在 grill 中敲定的开放问题

1. 临时页 vs 常驻页 vs 模态的**分工边界**（哪些坚决不迁）。
2. 回收时机：导航离开即回收，还是设置窗口关闭时才回收。
3. 脏状态与现有窗口级"保存"按钮的关系（临时页是否引入页级保存）。
4. "晋升常驻"是否需要（还是临时页永远临时、只允许单例复用）。
5. 外观页是常驻还是临时。
6. 深链入口（轮盘/插件直接跳某配置页）是否纳入本期。

---

## 4. 参考资料

- VS Code Editor Tabs（preview mode）：code.visualstudio.com/docs/editor/tabs
- Raycast Settings 手册（per-extension tabs、sub-views、deep-link）：manual.raycast.com/settings
- PowerToys Command Palette 设置：learn.microsoft.com/windows/powertoys/command-palette/settings
- "Build a Settings Page Architecture That Scales"（settings registry / deep-linking / save patterns）
- "SaaS Settings Page Design That Doesn't Confuse Users"（accordion 反模式、sidebar 阈值）
- uxpatterns.dev — Sidebar / Tabs 模式对照
