# 📝 Pulsar v2.0 产品需求文档 (PRD)

> **项目代号**: Pulsar Redux
> **核心价值**: 消除“切换”与“执行”之间的微秒级阻力。
> **版本目标**: 构建一个**逻辑严格分离**、**执行绝对稳定**、**交互符合直觉**的系统级效率轮盘。

## 1. 核心交互逻辑 (Core Interaction Logic)

### 1.1 极坐标判定算法 (Infinite Sector Selection)

这是交互体验的基石，彻底摒弃旧版基于控件碰撞（HitTest）的逻辑。

* **无限延伸扇区**: 屏幕被划分为 8 个 `45°` 的扇形区域。
* 无论鼠标距离中心多远（只要超出盲区），光标所在的**角度**直接决定选中的槽位。
* *解决痛点*: 用户在大屏幕上甩动鼠标过快移出轮盘界面时，依然能精准选中。


* **中心盲区 (Dead Zone)**:
* 定义一个半径为 `R_Inner` (如 50px) 的中心圆。
* **取消机制**: 当 `Distance < R_Inner` 时，视为**“未选中 / 取消”**。
* **触发行为**: 此时松开快捷键，窗口直接关闭，**不执行**任何命令。



### 1.2 双模态物理隔离 (Dual-Mode Separation)

系统在运行时严格区分两种状态，互不干扰。

| 特性 | **模式一: 窗口切换器 (Window Switcher)** | **模式二: 智能命令集 (Smart Commands)** |
| --- | --- | --- |
| **快捷键** | `Ctrl + Shift + Q` | `Ctrl + Q` |
| **数据源** | 全局唯一的静态配置 (Global Single) | 上下文感知动态配置 (Context Aware) |
| **核心行为** | 激活已运行窗口 (或启动) | 运行脚本/工具 (Exe + Args) |
| **视觉隐喻** | 绿色 / 导航风格 | 蓝色 / 终端风格 |

---

## 2. 功能详细说明 (Functional Requirements)

### 2.1 设置中心 (Settings Window) - 核心重构

设置界面不再是单一视图，而是根据“功能入口”进行**顶层分流**。

#### **A. 顶层导航 (Top Level Nav)**

界面最左侧或顶部包含两个醒目的入口按钮：

1. **[ 🔀 窗口切换配置 ]**
2. **[ 🚀 智能命令配置 ]**

#### **B. 窗口切换视图 (View for Mode 1)**

* **布局**: 无左侧列表，仅显示**一个**大尺寸可视化轮盘。
* **编辑交互**: 点击槽位 -> 弹出模态框。
* **核心字段**: `Label` (显示名), `MatchProcess` (目标进程名).
* **智能录入**: 提供下拉列表，列出当前**所有运行中的窗口**（带图标），用户点击即可填入准确的 `ProcessName`。
* **图标**: 优先提取目标 EXE 图标，允许覆盖为内置图标。



#### **C. 智能命令视图 (View for Mode 2)**

* **布局**: **左侧 Profile 列表** + **右侧可视化轮盘**。
* **Profile 管理**:
* 列表包含: `🌐 Global Default` (默认) 和用户新建的 `Chrome`, `VSCode` 等。
* **新建流程**: 点击“+”，使用“靶心抓取器”点击任意窗口，自动获取进程名作为 Profile 触发条件。


* **编辑交互**: 点击槽位 -> 弹出模态框。
* **核心字段 (显式拆分)**:
* `Executable`: 程序绝对路径 (粘贴或浏览)。
* `Arguments`: 运行参数 (纯文本粘贴)。


* **禁止解析**: 严禁在代码中对 Exe/Args 进行字符串 Split 操作，保持原样传递给 `Process.Start`。
* **图标**: 必须提供图标选择器（基于 Segoe MDL2 字体库 + 本地文件上传）。



### 2.2 运行时行为 (Runtime Behavior)

#### **上下文感知 (Context Awareness - 仅模式二)**

* 当按下 `Ctrl + Q` 时：
1. 系统获取当前前台窗口进程名 (如 `chrome.exe`)。
2. 在配置中查找是否存在 `Key="chrome"` 的 Profile。
3. **命中**: 加载 Chrome 专属轮盘。
4. **未命中**: 加载 `Global Default` 轮盘。



#### **中心圆反馈 (Center Feedback)**

* **模式一**: 始终显示 Pulsar Logo 或“Switch”图标。
* **模式二**:
* 若加载了 Chrome Profile -> 中心显示 Chrome 图标（视觉提示：当前是 Chrome 模式）。
* 若加载 Global -> 显示 Pulsar Logo。


* **交互状态**: 当鼠标位于盲区内时，中心圆高亮/微缩，提示“松开即取消”。

---

## 3. 数据结构重构 (Data Architecture)

为了支持上述逻辑，必须使用多态结构。

```csharp
// [Core/Models/AppConfig.cs]

public class AppConfig
{
    // Mode 1: 全局唯一配置
    public List<LauncherItem> WindowSwitcherSlots { get; set; } = new(8);

    // Mode 2: 多层级配置
    public CommandConfig CommandLayer { get; set; } = new();
}

public class CommandConfig
{
    public List<CommandItem> GlobalSlots { get; set; } = new(8);
    public List<AppProfile> Profiles { get; set; } = new();
}

public class AppProfile
{
    public string ProcessName { get; set; } // 触发条件
    public List<CommandItem> Slots { get; set; } = new(8);
}

// [Core/Models/Items/GridItemBase.cs]
// 使用 JSON 多态序列化
public abstract class GridItemBase 
{
    public string Label { get; set; }
    public string IconKey { get; set; } // Segoe 字符 或 文件路径
    public string IconType { get; set; } // Enum: Font, File, Auto
}

public class LauncherItem : GridItemBase
{
    public string ProcessName { get; set; } // 用于 FindWindow
}

public class CommandItem : GridItemBase
{
    public string ExePath { get; set; }     // 绝对路径
    public string Arguments { get; set; }   // 参数原样传递
}

```

---

## 4. 开发路线图 (Implementation Roadmap)

### Phase 5.1: 地基重筑 (Foundation)

1. **数据迁移**: 删除旧的 `GridItem`，建立上述多态类结构。
2. **配置服务**: 重写 `ConfigService`，确保能读写新的 JSON 结构。
3. **核心算法**: 实现 `MathHelper.GetSector(Point mouse, Point center)`，返回 0-7 索引或 -1 (盲区)。

### Phase 5.2: 服务层升级 (Services)

1. **CommandService**: 移除所有正则解析代码。改为 `Process.Start(new ProcessStartInfo(exe, args))`。
2. **WindowService**: 完善 `GetForegroundProcess()` 和 `ActivateWindow(processName)`。

### Phase 5.3: UI 重构 (Interface)

1. **设置窗口**: 拆分为 `SwitcherConfigView` 和 `CommandConfigView`。
2. **组件开发**:
* `JellyWheel`: 支持数据绑定的可视化轮盘控件。
* `SlotEditorDialog`: 基于 DataTemplate 动态切换内容的弹窗。
* `IconPicker`: Segoe 图标网格选择器。



### Phase 5.4: 联调与发布 (Release)

1. 绑定双全局快捷键 (`HookService`)。
2. 测试极坐标判定的手感（调整盲区半径）。
3. 测试复杂参数命令（如 `python script.py "arg with spaces"`）的执行稳定性。
