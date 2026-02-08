# Pulsar 架构设计文档 (PADD) v2.0

**状态**: Approved | **核心**: Plugin System & Static Mapping | **最后更新**: 2026-01-26

## 1. 项目愿景 (Project Vision)

Pulsar 是一个基于 C# WPF 的高性能桌面效率工具。

* **核心形态**: 快捷键唤起的**径向菜单 (Radial Menu)**。
* **核心哲学**: **肌肉记忆 (Muscle Memory)** 至上。摒弃传统 Alt-Tab 的线性遍历，利用空间位置实现“盲操作”。
* **技术转型**: 从单纯的“启动器 (Launcher)”演进为“应用容器 (Container)”。通过统一的插件系统接管业务逻辑，实现毫秒级响应和统一的反馈机制。

---

## 2. 系统核心架构 (System Architecture)

### 2.1 运行模式 (Operational Modes)

Pulsar 通过不同的快捷键触发两种独立模式，数据结构需严格隔离：

1. **指令模式 (Command Mode)** - `Ctrl + Q`
* **逻辑**: 基于当前激活窗口（上下文），加载静态配置的动作。
* **用途**: 执行 VBA 脚本、填充密码 (PKI)、数据转换等。
* **布局**: 严格对应 `Profiles.json` 配置，不随使用频率改变位置。


2. **切换模式 (Switch Mode)** - `Ctrl + Shift + Q`
* **逻辑**: 快速激活其他应用程序。
* **布局**:
* **外圈 (Outer Ring)**: 静态配置。例如：左侧固定是 Chrome，右侧固定是 VS Code。
* **中心 (Center)**: 动态显示 **MRU (Most Recently Used)** 窗口（即“上一个窗口”），用于瞬间切回。





### 2.2 插件系统 (The Plugin System)

Pulsar 核心层不再包含具体业务，只负责：**捕获上下文** -> **分发任务** -> **渲染反馈**。

支持三种插件形态：

1. **Native Plugins (C# DLL)**: 运行在 Pulsar 进程内，访问完整的 WPF 对象。
2. **FFI Plugins (Rust/C++ DLL)**: 通过 P/Invoke 调用，追求极致计算性能和系统底层操作。
3. **Adapters (Legacy EXE)**: 用于兼容旧的独立工具 (如 VBA Runner)，通过插件层封装进程调用。

---

## 3. 技术规范与接口设计 (Technical Specs)

### 3.1 统一上下文对象 (Unified Context)

Pulsar 在唤起瞬间冻结系统状态，并封装为不可变对象传递给插件。这消除了插件自行搜寻窗口的开销和竞态风险。

```csharp
public struct PulsarContext
{
    // 目标窗口信息（用户唤起 Pulsar 时所在的窗口）
    public IntPtr TargetWindowHandle; 
    public string TargetProcessName;  // e.g., "EXCEL"
    public int TargetProcessId;
    
    // 用户输入/交互数据
    public string SelectedText;       // 预读取的选中文本（可选）
    public string ClipboardText;      // 剪贴板内容
    
    // 共享存储（用于插件间通信，暂定）
    public Dictionary<string, object> SessionData; 
}

```

### 3.2 插件接口契约 (Interface Contract)

#### C# 接口定义

```csharp
public interface IPulsarPlugin
{
    string Id { get; }          // e.g., "com.pulsar.pki"
    void Initialize();          // 冷启动初始化
    
    // 执行入口
    // action: 配置文件中指定的动作名
    // args: 静态参数
    // context: 运行时环境
    PluginResult Execute(string action, Dictionary<string, string> args, PulsarContext context);
}

public struct PluginResult
{
    public bool Success;
    public string Message;      // 用于日志或 Toast 显示
    public VisualCue Cue;       // e.g., ShowCheckMark, ShakeWindow, ErrorRed
}

```

#### Rust FFI 导出规范 (extern "C")

```rust
#[repr(C)]
pub struct FFIContext {
    pub target_hwnd: usize,
    pub selected_text: *const c_char,
    // ... 其他字段需与 C# 内存布局对齐
}

#[no_mangle]
pub extern "C" fn pulsar_plugin_execute(
    action: *const c_char, 
    context: FFIContext
) -> FFIResult {
    // Rust 高性能逻辑
    // 直接利用 target_hwnd 操作窗口
}

```

---

## 4. 配置驱动系统 (Configuration Driven)

配置是静态的，以保证肌肉记忆。

### `Profiles.json` 结构定义

```json
{
  "Settings": {
    "CenterSlotBehavior": "MRU_Window" // 切换模式下的中心动作
  },
  "Profiles": {
    // === 针对 Excel 的配置 ===
    "EXCEL": {
      "CommandMode": {
        // 只有在 Excel 下，1 点钟方向才是运行这个特定的 VBA
        "Slot_1": { "plugin": "VBARunner", "action": "run", "args": { "script": "format.vbs" } },
        "Slot_3": { "plugin": "RustPKI",   "action": "fill", "args": { "type": "login" } }
      },
      "SwitchMode": {
        // 允许针对特定软件覆盖切换逻辑，若为空则使用 Global
      }
    },
    
    // === 全局默认配置 ===
    "Global": {
      "SwitchMode": {
        "Slot_1": { "plugin": "WinSwitcher", "action": "activate", "args": { "app": "chrome" } },
        "Slot_2": { "plugin": "WinSwitcher", "action": "activate", "args": { "app": "code" } }
      }
    }
  }
}

```

---

## 5. 开发路线图 (Roadmap)

### 第一阶段：核心重构 (Core Refactoring)

1. **定义 `IPulsarPlugin` 接口**：创建 `Pulsar.Core` 类库。
2. **实现插件加载器 (PluginLoader)**：使用 Reflection 加载 C# DLL，使用 `DllImport` 加载 Rust DLL。
3. **重构主循环**：Pulsar 唤起 -> 识别 Process -> 读取 Profile -> 渲染 UI。

### 第二阶段：插件迁移 (Migration)

1. **PKI 模块**：封装为 Native Plugin (C#)，直接操作句柄填充密码。
2. **VBA Runner**：创建 `ExternalAdapterPlugin`，接管现有的 EXE 调用，但增加 `PulsarContext` 传递（作为命令行参数传递句柄）和日志捕获。
3. **窗口切换器**：实现“中心动态 MRU，周围静态配置”的逻辑。

### 第三阶段：Rust 引入 (Performance)

1. 尝试用 Rust 重写窗口查找与管理逻辑，编译为 DLL 供 Switch Mode 调用。
