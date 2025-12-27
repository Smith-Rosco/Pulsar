# 🚀 Pulsar Project: 核心重构 PRD (v2.0)

> **版本**: 2.0 (The Polymorphic Refactor)
> **日期**: 2025-12-27
> **核心目标**: 彻底消除逻辑歧义，实现“切换”与“执行”的物理隔离，构建可视化配置体系。

---

## 1. 🎯 项目愿景与痛点分析

### 1.1 核心痛点 (The Pain)

1. **身份混淆**: 旧版 `GridItem` 强行揉合了“窗口切换”与“命令执行”，导致逻辑臃肿。
2. **解析黑盒**: 依赖不稳定的字符串智能解析（Smart Parse），导致带空格的路径无法正确执行。
3. **配置迷宫**: 用户无法直观区分自己到底是在配 `Ctrl+Q` 还是 `Ctrl+Shift+Q`。

### 1.2 解决方案 (The Solution)

* **多态架构**: 在代码层面将数据结构拆分为 `LauncherItem` (切换) 和 `CommandItem` (执行)。
* **显式录入**: 废除解析猜测，强制拆分 `Exe` 与 `Args` 字段。
* **平行宇宙**: 通过 UI 和数据结构的物理隔离，让“命令层”与“窗口层”互不干扰。

---

## 2. 🏗️ 数据结构定义 (Data Architecture)

这是本次重构的基石。我们将从单一类转向**多态继承**结构。

### 2.1 继承体系

```csharp
// 基类
public abstract class GridItemBase
{
    public int SlotIndex { get; set; }      // 0-7
    public string Label { get; set; }       // 显示名称
    public string Description { get; set; } // 悬停提示
    public string IconKey { get; set; }     // 图标源 (Segoe Glyph 或 文件路径)
    public bool UseCustomIcon { get; set; } // 是否使用自定义图标
}

// 派生类 A: 仅用于 Ctrl+Shift+Q (窗口层)
public class LauncherItem : GridItemBase
{
    public string MatchProcessName { get; set; } // 核心: 目标进程名 (如 "chrome")
    // 逻辑: 仅负责查找并激活窗口，若无窗口则 (可选) 尝试启动
}

// 派生类 B: 仅用于 Ctrl+Q (命令层)
public class CommandItem : GridItemBase
{
    public string ExecutablePath { get; set; }   // 核心: 绝对路径
    public string Arguments { get; set; }        // 核心: 参数字符串
    public string WorkingDirectory { get; set; } // 可选: 工作目录
    // 逻辑: 直接 Process.Start(Exe, Args)
}

```

### 2.2 配置对象 (Profile)

```csharp
public class AppProfile
{
    public string ProfileName { get; set; }       // 如 "Global", "VS Code"
    public string TriggerProcess { get; set; }    // 激活此 Profile 的前台进程名
    
    // 双层数据隔离，杜绝混淆
    public List<CommandItem> CommandLayer { get; set; }   // 对应 Ctrl+Q
    public List<LauncherItem> WindowLayer { get; set; }   // 对应 Ctrl+Shift+Q
}

```

---

## 3. 🖥️ 设置界面交互规范 (UI/UX Specifications)

### 3.1 整体布局 (Layout)

| 区域 | 控件类型 | 功能描述 |
| --- | --- | --- |
| **左侧边栏** | `ListBox` | **Profile 管理区**。<br>

<br>包含 Global 及各软件专用配置。底部有 [➕ 添加] 按钮。 |
| **顶部导航** | `SegmentedButton` | **层级切换区 (核心)**。<br>

<br>左钮：`[ ⌨️ Command Layer (Ctrl+Q) ]`<br>

<br>右钮：`[ 🔀 Window Layer (Ctrl+Shift+Q) ]` |
| **中部舞台** | `ItemsControl` | **可视化轮盘**。<br>

<br>根据顶部 Tab 绑定不同的数据源 (`CommandLayer` vs `WindowLayer`)。<br>

<br>点击槽位触发弹窗。 |
| **中心圆** | `ContentControl` | **状态指示器**。<br>

<br>显示当前 Profile 图标或层级图标。 |

### 3.2 交互剧本：模态编辑 (Modal Editing)

点击槽位后，弹出模态对话框，内容根据当前层级动态渲染。

#### 场景 A: 编辑 Command Layer (Ctrl+Q)

* **模板**: `CommandEditorTemplate`
* **字段**:
1. **Label**: 文本输入。
2. **Icon**: 预览图 + [选择器] (打开 Segoe 图标网格)。
3. **Executable**: 文本输入 + [📂 文件浏览]。
4. **Arguments**: 文本输入 (多行)。**不做任何解析，原样传递。**



#### 场景 B: 编辑 Window Layer (Ctrl+Shift+Q)

* **模板**: `LauncherEditorTemplate`
* **字段**:
1. **Label**: 文本输入。
2. **Match Process**: `ComboBox` (可输入，可选择)。
* *智能行为*: 点击下拉时，列出当前系统所有**有窗口的进程** (Icon + ProcessName)，供用户直接点选。





---

## 4. ⚙️ 运行时逻辑 (Runtime Logic)

### 4.1 激活流程

1. 用户按下 `Ctrl+Q` 或 `Ctrl+Shift+Q`。
2. **上下文感知**: 获取当前前台窗口进程 (例如 `Code.exe`)。
3. **Profile 匹配**:
* 查找 `TriggerProcess == "Code.exe"` 的 Profile。
* 若无，回退到 `Global` Profile。


4. **层级加载**:
* 若按的是 `Ctrl+Q` -> 加载该 Profile 的 `CommandLayer` 数据。
* 若按的是 `Ctrl+Shift+Q` -> 加载该 Profile 的 `WindowLayer` 数据。


5. **渲染**: 轮盘弹出。

### 4.2 智能取消与中心区

* **判定区**: 轮盘中心 `InnerRadius` 范围内。
* **视觉**: 显示当前 Profile 图标（如 VS Code 图标）。
* **行为**: 鼠标位于中心区松开快捷键 -> **不执行任何操作**，窗口直接根据 FadeOut 动画关闭。

### 4.3 图标渲染策略

1. **优先**: 检查 `GridItem.UseCustomIcon`。若为真，渲染 `GridItem.IconKey` (Segoe Glyph 或 Path)。
2. **后备 (仅 Launcher)**: 若无自定义图标，尝试提取 `MatchProcess` 对应的 exe 图标。
3. **默认**: 显示通用占位符。

---

## 5. 📅 开发路线图 (Implementation Roadmap)

建议按以下顺序执行，不要跳跃：

* **Step 1: 基础设施 (Foundation)**
* 创建 `GridItemBase`, `CommandItem`, `LauncherItem` 类。
* 重构 `AppConfig` 以支持双层 List 结构。
* 引入 `Segoe MDL2` 资源字典。


* **Step 2: 核心服务 (Core Services)**
* 重写 `CommandService`: 移除所有 Parse 逻辑，改为强类型执行。
* 更新 `RadialMenuViewModel`: 增加 `IsWindowLayer` 状态，根据快捷键加载不同数据列表。


* **Step 3: 设置界面 (Settings UI)**
* 搭建“左侧列表 + 顶部 Tab + 可视化轮盘”的 XAML 骨架。
* 实现 `DataTemplateSelector`，用于在弹窗中根据 Item 类型切换编辑模板。
* 实现“抓取当前运行进程”的逻辑 (用于 Launcher 编辑器)。


* **Step 4: 缝合与测试 (Integration)**
* 连接运行时与配置数据。
* **重点测试**: 复杂的带参命令 (如 Python 脚本) 是否能通过拆分字段完美运行。
* **重点测试**: 快捷键切换层级是否流畅，数据是否串味。