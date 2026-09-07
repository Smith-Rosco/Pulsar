# Pulsar 教程系统开发交接文档

**项目**: Pulsar Interactive Tutorial System  
**最后更新**: 2026-03-15  
**当前状态**: Phase 2 核心基础设施已完成，剩余集成工作待开发  
**文档版本**: v1.1

---

## 📋 目录

1. [项目概述](#项目概述)
2. [已完成工作](#已完成工作)
3. [文件清单](#文件清单)
4. [架构说明](#架构说明)
5. [待完成工作](#待完成工作)
6. [开发指南](#开发指南)
7. [测试建议](#测试建议)
8. [已知问题](#已知问题)
9. [参考文档](#参考文档)

---

## 1. 项目概述

### 1.1 项目目标

开发一个交互式教程系统，帮助新用户在 2 分钟内掌握 Pulsar 的核心功能：
- 切换模式 (Ctrl+Shift+Q)
- 命令模式 (Ctrl+Q)
- 槽位配置

### 1.2 设计原则

- ✅ **非侵入式** - 半透明遮罩 + 聚光灯效果，不阻塞用户操作
- ✅ **零依赖** - 使用 Windows 内置的 Notepad 作为演示应用
- ✅ **可跳过/恢复** - 用户随时可以跳过或稍后继续
- ✅ **状态持久化** - 教程进度保存到配置文件

### 1.3 实施计划

| Phase | 状态 | 工作量 | 说明 |
|-------|------|--------|------|
| Phase 1: Core Infrastructure | ✅ 已完成 | Week 1-2 | 数据模型、UI 组件、基础服务 |
| Phase 2: State Machine & Triggers | 🟡 80% 完成 | Week 3-4 | 状态机、触发器核心已完成，剩余集成工作 |
| Phase 3: Enhanced Process Picker | 📅 计划中 | Week 5 | 改进进程选择器 UX |
| Phase 4: Integration & Polish | 📅 计划中 | Week 6 | 集成测试、UI 优化 |

---

## 2. 已完成工作

### 2.1 Phase 1 完成清单

#### ✅ Milestone 1.1: 数据模型 & 服务接口
- [x] 创建 `TutorialStep` 模型 - 教程步骤定义
- [x] 创建 `TutorialTarget` 模型 - 目标元素定义
- [x] 创建 `TutorialTrigger` 模型 - 触发器定义
- [x] 实现 `ITutorialService` 接口 - 服务层抽象
- [x] 扩展 `ProfileSettings` - 添加 `HasCompletedTutorial` 和 `LastTutorialStep`

#### ✅ Milestone 1.2: 遮罩窗口
- [x] 创建 `TutorialOverlayWindow` - 全屏透明窗口
- [x] 实现聚光灯效果 - 使用 `CombinedGeometry` 镂空
- [x] 实现点击穿透 - 使用 Win32 API (`WS_EX_TRANSPARENT`)
- [x] 支持动态设置聚光灯区域

#### ✅ Milestone 1.3: 指令卡片
- [x] 创建 `TutorialStepCard` UserControl
- [x] 实现卡片入场动画 (Fade + Scale)
- [x] 实现箭头定位逻辑 (Top/Bottom/Left/Right)
- [x] 支持居中显示和边界检测

#### ✅ Milestone 1.4: 基础服务
- [x] 实现 `TutorialService` 基本功能
- [x] 支持启动/跳过/完成教程
- [x] 状态持久化到配置文件

### 2.2 Phase 2 完成清单 (2026-03-15 更新)

#### ✅ Milestone 2.1: 触发器系统
- [x] 创建 `ITriggerHandler` 接口 - 触发器处理器抽象
- [x] 实现 `WindowOpenedTriggerHandler` - 监听窗口打开事件
- [x] 实现 `PageNavigatedTriggerHandler` - 监听页面导航
- [x] 实现 `SlotAddedTriggerHandler` - 监听 Slot 添加事件
- [x] 实现 `RadialMenuShownTriggerHandler` - 监听轮盘菜单显示

#### ✅ Milestone 2.2: UI 标记系统
- [x] 创建 `TutorialMarker` - WPF Attached Property 用于标记 UI 元素
- [x] 创建 `TutorialTargetRegistry` - 运行时元素注册表和定位系统

#### ✅ Milestone 2.3: 状态机核心
- [x] 创建 `TutorialOrchestrator` - 教程编排器
- [x] 实现步骤导航逻辑 (StartAsync, NextStepAsync)
- [x] 实现触发器集成和管理
- [x] 实现遮罩窗口和步骤卡片管理
- [x] 定义前 4 个教程步骤 (欢迎、打开设置、设置导览、导航到槽位)

#### ⏳ Milestone 2.4: 剩余集成工作 (待完成)
- [ ] 完善 TutorialOrchestrator - 添加剩余 5 个教程步骤 (Step 5-9)
- [ ] 完善目标元素定位逻辑 (托盘图标、窗口等)
- [ ] 集成 TutorialOrchestrator 到 TutorialService
- [ ] 在 XAML 中添加 TutorialMarker 标记
- [ ] App.xaml.cs 集成和首次启动检测

### 2.3 构建状态

```bash
✅ 编译成功 - 0 警告 0 错误
✅ 所有新文件已添加到项目
✅ XAML 代码生成正常
✅ Phase 2 核心基础设施完成
```

---

## 3. 文件清单

### 3.1 新增文件

#### 数据模型 (`Pulsar/Models/Tutorial/`)
```
TutorialStep.cs          - 教程步骤模型 (Id, Title, Description, Type, Target, Trigger)
TutorialTarget.cs        - 目标元素定义 (Type, ElementName, Bounds)
TutorialTrigger.cs       - 触发器定义 (Type, TargetValue)
```

#### 服务层 (`Pulsar/Services/`)
```
Interfaces/ITutorialService.cs              - 教程服务接口
TutorialService.cs                          - 教程服务实现
Tutorial/TutorialOrchestrator.cs            - 教程编排器 (状态机核心) ✅ NEW
Tutorial/TriggerHandlers/ITriggerHandler.cs - 触发器接口 ✅ NEW
Tutorial/TriggerHandlers/WindowOpenedTriggerHandler.cs      ✅ NEW
Tutorial/TriggerHandlers/PageNavigatedTriggerHandler.cs     ✅ NEW
Tutorial/TriggerHandlers/SlotAddedTriggerHandler.cs         ✅ NEW
Tutorial/TriggerHandlers/RadialMenuShownTriggerHandler.cs   ✅ NEW
```

#### 辅助工具 (`Pulsar/Helpers/Tutorial/`) ✅ NEW
```
TutorialMarker.cs           - WPF Attached Property 用于标记 UI 元素
TutorialTargetRegistry.cs   - 运行时元素注册表和定位系统
```

#### UI 组件 (`Pulsar/Views/Tutorial/`)
```
TutorialOverlayWindow.xaml      - 遮罩窗口 XAML
TutorialOverlayWindow.xaml.cs   - 遮罩窗口代码后端
TutorialStepCard.xaml           - 指令卡片 XAML
TutorialStepCard.xaml.cs        - 指令卡片代码后端
```

### 3.2 修改文件

#### 配置模型 (`Pulsar/Models/`)
```
ProfilesConfig.cs  - 添加教程相关属性到 ProfileSettings
  + HasCompletedTutorial: bool
  + LastTutorialStep: string?
```

---

## 4. 架构说明

### 4.1 系统架构

```
┌─────────────────────────────────────────────────────────┐
│                    ITutorialService                      │
│  (服务层 - 对外接口)                                      │
└────────────────────┬────────────────────────────────────┘
                     │
                     ▼
┌─────────────────────────────────────────────────────────┐
│              TutorialOrchestrator                        │
│  (状态机 - 管理教程流程) [待实现]                         │
└────────────────────┬────────────────────────────────────┘
                     │
        ┌────────────┴────────────┐
        ▼                         ▼
┌──────────────────┐    ┌──────────────────┐
│ TutorialOverlay  │    │ TutorialStepCard │
│ Window           │    │                  │
│ (遮罩 + 聚光灯)   │    │ (指令卡片)        │
└──────────────────┘    └──────────────────┘
```

### 4.2 核心组件说明

#### TutorialOverlayWindow
**职责**: 全屏透明遮罩，实现聚光灯效果

**关键方法**:
- `SetSpotlight(Rect bounds)` - 设置聚光灯区域
- `ClearSpotlight()` - 清除聚光灯
- `SetCardContent(UIElement content)` - 设置卡片内容

**技术实现**:
- 使用 `CombinedGeometry.Exclude` 实现镂空效果
- 使用 Win32 API 实现点击穿透
- 圆角半径: 8px
- 遮罩透明度: 180/255 (70%)

#### TutorialStepCard
**职责**: 显示教程步骤说明和操作按钮

**关键方法**:
- `SetStep(TutorialStep, currentIndex, totalSteps)` - 设置步骤信息
- `PositionRelativeTo(Rect, ArrowDirection)` - 根据目标定位卡片
- `ShowContinueButton()` - 显示"继续"按钮

**UI 规格**:
- 最大宽度: 400px
- 内边距: 24px
- 圆角: 12px
- 阴影: 0 8px 32px rgba(0,0,0,0.3)

#### TutorialService
**职责**: 教程生命周期管理

**当前实现**:
- ✅ 启动/跳过/完成教程
- ✅ 状态持久化
- ✅ 事件通知 (StepChanged, TutorialCompleted, TutorialSkipped)

**待实现**:
- ⏳ 与 TutorialOrchestrator 集成
- ⏳ 步骤导航逻辑
- ⏳ 暂停/恢复功能

---

## 5. 待完成工作

### 5.1 Phase 2 剩余工作 (优先级: 高) 🔴

#### Milestone 2.4: 完善 TutorialOrchestrator
**文件**: `Pulsar/Services/Tutorial/TutorialOrchestrator.cs`

**需要完成**:
1. 添加剩余 5 个教程步骤定义 (Step 5-9)
   - Step 5: 添加 Notepad Slot (Switch Mode)
   - Step 6: 测试切换模式
   - Step 7: 添加 Notepad Profile Slot (Command Mode)
   - Step 8: 测试命令模式
   - Step 9: 完成总结

2. 完善 `GetTargetBounds()` 方法
   - 实现托盘图标定位 (使用 Win32 API)
   - 实现窗口定位
   - 添加 Fallback 逻辑

**参考**: `Docs/architecture/TUTORIAL_SYSTEM.md` 第 4.2 节 (Step 5-9 详细定义)

#### Milestone 2.5: 集成到 TutorialService
**文件**: `Pulsar/Services/TutorialService.cs`

**需要完成**:
1. 添加 `TutorialOrchestrator` 依赖注入
2. 在 `StartTutorialAsync()` 中调用 `_orchestrator.StartAsync()`
3. 实现暂停/恢复功能
4. 连接 Orchestrator 事件到 Service 事件

**代码示例**:
```csharp
public class TutorialService : ITutorialService
{
    private readonly TutorialOrchestrator _orchestrator;
    
    public TutorialService(
        IConfigService configService,
        SettingsViewModel settingsViewModel,
        RadialMenuViewModel radialMenuViewModel,
        ILogger<TutorialService> logger)
    {
        _orchestrator = new TutorialOrchestrator(
            configService, 
            logger, 
            settingsViewModel, 
            radialMenuViewModel);
            
        _orchestrator.StepChanged += (s, step) => 
            StepChanged?.Invoke(this, new TutorialStepChangedEventArgs(step));
        _orchestrator.TutorialCompleted += (s, e) => 
            TutorialCompleted?.Invoke(this, EventArgs.Empty);
    }
    
    public async Task StartTutorialAsync()
    {
        await _orchestrator.StartAsync();
    }
}
```

#### Milestone 2.6: UI 标记
**文件**: 相关 XAML 文件

**需要完成**:
1. 在 `SettingsWindow.xaml` 中添加标记
2. 在 `SettingsSlotsPage.xaml` 中标记 "添加槽位" 按钮
3. 在导航栏中标记 "槽位配置" 项

**代码示例**:
```xml
<Button x:Name="AddSlotButton"
        tutorial:TutorialMarker.Id="AddSlotButton"
        Content="添加槽位" />
```

#### Milestone 2.7: App.xaml.cs 集成
**文件**: `Pulsar/App.xaml.cs`

**需要完成**:
1. 在 `ConfigureServices()` 中注册 `ITutorialService`
2. 在 `OnStartup()` 中添加首次启动检测逻辑

**代码示例**:
```csharp
// ConfigureServices
serviceCollection.AddSingleton<ITutorialService, TutorialService>();

// OnStartup
var tutorialService = Services.GetRequiredService<ITutorialService>();
var configService = Services.GetRequiredService<IConfigService>();

if (!configService.Current.Settings.HasCompletedTutorial)
{
    await Task.Delay(1000); // 等待 UI 初始化
    await tutorialService.StartTutorialAsync();
}
```

**参考**: `Docs/architecture/TUTORIAL_SYSTEM_PART2.md` 第 6 节

### 5.2 Phase 3: Enhanced Process Picker (优先级: 中)

**目标**: 改进 Slot 编辑对话框的进程选择体验

**需要实现**:
1. `CommonApplications` 静态数据库 (20+ 常用应用)
2. `EnhancedProcessPickerViewModel` (3 个 Tab: 正在运行/常用应用/手动输入)
3. 集成到 Slot 编辑对话框

**参考**: `Docs/architecture/TUTORIAL_SYSTEM_PART3.md` 第 8.1 节

### 5.3 Phase 4: Integration & Polish (优先级: 中)

**需要完成**:
1. 在 `App.xaml.cs` 中注册 `ITutorialService`
2. 首次启动检测逻辑
3. 在 `SettingsGeneralPage` 添加"重新开始教程"按钮
4. 中断恢复对话框
5. UI/UX 优化和测试

**参考**: `Docs/architecture/TUTORIAL_SYSTEM_PART2.md` 第 6 节

---

## 6. 开发指南

### 6.1 开发环境

- **IDE**: Visual Studio 2022 或 Rider
- **Framework**: .NET 8.0
- **语言**: C# 12
- **UI 框架**: WPF + WPF UI (Wpf.Ui NuGet 包)

### 6.2 构建命令

```bash
# 构建项目
dotnet build "G:\0_Playground\Pulsar_Project\Pulsar\Pulsar\Pulsar.csproj"

# 运行项目
dotnet run --project "G:\0_Playground\Pulsar_Project\Pulsar\Pulsar\Pulsar.csproj"

# 清理构建
dotnet clean "G:\0_Playground\Pulsar_Project\Pulsar\Pulsar\Pulsar.csproj"
```

### 6.3 代码规范

**遵循项目现有规范**:
- 使用 `CommunityToolkit.Mvvm` 的 `[ObservableProperty]` 和 `[RelayCommand]`
- 构造函数注入依赖
- 使用 `ILogger<T>` 记录日志
- 异步方法使用 `async Task` 并以 `Async` 结尾

**UI 规范** (重要！):
- ❌ 不要使用 `Appearance="Primary"` (已知 Bug)
- ✅ 使用 `Style="{StaticResource PulsarPrimaryButtonStyle}"`
- ✅ 使用 `Style="{StaticResource PulsarSecondaryButtonStyle}"`
- ✅ 使用 `Style="{StaticResource PulsarDangerButtonStyle}"`

**参考**: `Docs/lessons/WPFUI_BUTTON_PRIMARY_BUG.md`

### 6.4 调试技巧

**测试教程系统**:
1. 删除配置文件中的 `HasCompletedTutorial` 标志
2. 重启应用，教程应自动触发
3. 使用 `_logger.LogInformation()` 跟踪状态变化

**配置文件位置**:
```
%AppData%\Pulsar\Profiles.json
```

**日志文件位置**:
```
%AppData%\Pulsar\Logs\pulsar-yyyyMMdd.log
```

---

## 7. 测试建议

### 7.1 单元测试

**优先测试**:
- `TutorialOrchestrator` 状态机逻辑
- 触发器检测准确性
- 步骤导航正确性

**示例**:
```csharp
[TestMethod]
public async Task StartAsync_ShouldShowFirstStep()
{
    var orchestrator = new TutorialOrchestrator(...);
    await orchestrator.StartAsync();
    
    Assert.AreEqual("step1_welcome", orchestrator.CurrentStep.Id);
}
```

### 7.2 集成测试

**测试场景**:
1. 首次启动自动触发教程
2. 完整走完 9 个步骤
3. 中途跳过教程
4. 中途关闭应用，重启后恢复
5. 在设置中重新启动教程

### 7.3 手动测试清单

**Phase 2 完成后必测**:
- [ ] 教程在首次启动时自动显示
- [ ] 聚光灯正确高亮目标元素
- [ ] 点击穿透在聚光灯区域生效
- [ ] 所有触发器正确检测用户操作
- [ ] 步骤卡片位置正确，不超出屏幕边界
- [ ] 跳过教程后不再自动显示
- [ ] 完成教程后标记为已完成
- [ ] 在设置中可以重新启动教程

**参考**: `Docs/architecture/TUTORIAL_SYSTEM_PART4.md` 第 10.3 节

---

## 8. 已知问题

### 8.1 LSP 错误 (可忽略)

**现象**: 
```
ERROR: 当前上下文中不存在名称"InitializeComponent"
```

**原因**: XAML 代码生成在构建时完成，LSP 在编辑时可能未检测到

**解决**: 运行 `dotnet build` 后错误消失

### 8.2 托盘图标定位

**挑战**: 系统托盘图标位置难以精确获取

**解决方案** (已设计，待实现):
1. 使用 Win32 API `FindWindow("Shell_TrayWnd")` 查找托盘区域
2. Fallback: 高亮整个托盘区域 (右下角 200×40)
3. Fallback: 跳过托盘图标步骤

**参考**: `Docs/architecture/TUTORIAL_SYSTEM_PART3.md` 第 8.3 节

### 8.3 SendKeys 参数复杂性

**问题**: 原设计要求用户输入 `Current Time: {ENTER}%date% %time%`，对新手不友好

**解决**: 已简化为 `Hello from Pulsar!{ENTER}`

**未来增强**: 提供 SendKeys 模板库 (Phase 3+)

---

## 9. 参考文档

### 9.1 设计文档

| 文档 | 路径 | 说明 |
|------|------|------|
| **索引文档** | `Docs/architecture/TUTORIAL_SYSTEM_INDEX.md` | 总览和快速导航 |
| **Part 1: 架构设计** | `Docs/architecture/TUTORIAL_SYSTEM.md` | 数据模型、教程流程 |
| **Part 2: 技术实现** | `Docs/architecture/TUTORIAL_SYSTEM_PART2.md` | 代码实现细节 |
| **Part 3: 挑战与解决方案** | `Docs/architecture/TUTORIAL_SYSTEM_PART3.md` | 关键技术难点 |
| **Part 4: 实施计划** | `Docs/architecture/TUTORIAL_SYSTEM_PART4.md` | 路线图、测试、脚本 |

### 9.2 项目文档

| 文档 | 路径 | 说明 |
|------|------|------|
| **AI Agent 指南** | `AGENTS.md` | AI 开发规范和约定 |
| **架构总览** | `ARCHITECTURE.md` | 系统架构说明 |
| **对话框系统** | `Docs/architecture/DIALOG_SYSTEM.md` | 对话框开发指南 |
| **UI 最佳实践** | `Docs/guides/UI_BEST_PRACTICES.md` | WPF UI 开发规范 |
| **已知坑点** | `Docs/lessons/` | WPF 常见问题和解决方案 |

### 9.3 外部参考

- [VS Code Welcome Experience](https://code.visualstudio.com/docs/getstarted/introvideos)
- [Figma Onboarding](https://www.figma.com/community/)
- [Material Design Onboarding](https://material.io/design/communication/onboarding.html)

---

## 10. 联系方式

### 10.1 问题反馈

如有疑问，请参考以下资源:

1. **设计文档** - 查看 `Docs/architecture/TUTORIAL_SYSTEM_*.md`
2. **代码注释** - 所有关键方法都有详细注释
3. **Git 历史** - 查看提交记录了解实现思路

### 10.2 后续开发建议

**优先级排序**:
1. 🔴 **高优先级**: Phase 2 (状态机和触发器) - 核心功能
2. 🟡 **中优先级**: Phase 3 (进程选择器) - UX 改进
3. 🟢 **低优先级**: Phase 4 (优化和测试) - 锦上添花

**预估工作量**:
- Phase 2: 2-3 周 (1 名开发者)
- Phase 3: 1 周
- Phase 4: 1 周

**建议分工**:
- 后端开发者: TutorialOrchestrator + 触发器处理器
- 前端开发者: UI 集成 + TutorialMarker
- 全栈开发者: 可独立完成所有工作

---

## 11. 附录

### 11.1 教程步骤概览

| 步骤 | ID | 类型 | 目标 | 触发器 | 预计时长 |
|------|----|----|------|--------|---------|
| 1 | step1_welcome | Instruction | None | ButtonClick | 5s |
| 2 | step2_open_settings | WaitForAction | TrayIcon | WindowOpened | 10s |
| 3 | step3_settings_overview | Instruction | Window | ButtonClick | 8s |
| 4 | step4_navigate_slots | WaitForNavigation | UIElement | PageNavigated | 10s |
| 5 | step5_add_notepad_slot | WaitForAction | UIElement | SlotAdded | 20s |
| 6 | step6_test_switch_mode | WaitForAction | None | RadialMenuShown | 15s |
| 7 | step7_add_command_slot | WaitForAction | UIElement | SlotAdded | 25s |
| 8 | step8_test_command_mode | WaitForAction | None | RadialMenuShown | 15s |
| 9 | step9_completion | Instruction | None | ButtonClick | 10s |

**总时长**: ~2 分钟

### 11.2 文件结构树

```
Pulsar/Pulsar/
├── Models/
│   ├── Tutorial/
│   │   ├── TutorialStep.cs          ✅ 已完成
│   │   ├── TutorialTarget.cs        ✅ 已完成
│   │   └── TutorialTrigger.cs       ✅ 已完成
│   └── ProfilesConfig.cs            ✅ 已修改
│
├── Services/
│   ├── Interfaces/
│   │   └── ITutorialService.cs      ✅ 已完成
│   ├── Tutorial/                    ✅ 已创建
│   │   ├── TutorialOrchestrator.cs  🟡 80% 完成 (核心逻辑已实现，需添加 Step 5-9)
│   │   └── TriggerHandlers/         ✅ 已完成
│   │       ├── ITriggerHandler.cs                      ✅
│   │       ├── WindowOpenedTriggerHandler.cs           ✅
│   │       ├── PageNavigatedTriggerHandler.cs          ✅
│   │       ├── SlotAddedTriggerHandler.cs              ✅
│   │       └── RadialMenuShownTriggerHandler.cs        ✅
│   └── TutorialService.cs           🟡 需集成 Orchestrator
│
├── Views/
│   └── Tutorial/
│       ├── TutorialOverlayWindow.xaml      ✅ 已完成
│       ├── TutorialOverlayWindow.xaml.cs   ✅ 已完成
│       ├── TutorialStepCard.xaml           ✅ 已完成
│       └── TutorialStepCard.xaml.cs        ✅ 已完成
│
└── Helpers/
    └── Tutorial/                    ✅ 已创建
        ├── TutorialMarker.cs        ✅ 已完成
        └── TutorialTargetRegistry.cs ✅ 已完成
```

---

## 📌 快速开始指南

### 对于接手开发者 (2026-03-15 更新)

#### 当前进度
✅ Phase 1: 100% 完成  
🟡 Phase 2: 80% 完成 (核心基础设施已就绪)  
⏳ 剩余工作: 集成和完善 (预计 3-5 天)

#### 下一步行动计划

**第 1 步: 完善 TutorialOrchestrator** (1-2 天)
1. 打开 `Pulsar/Services/Tutorial/TutorialOrchestrator.cs`
2. 在 `InitializeSteps()` 方法中添加 Step 5-9 的定义
3. 参考 `Docs/architecture/TUTORIAL_SYSTEM.md` 第 4.2 节获取详细步骤定义
4. 完善 `GetTargetBounds()` 方法实现托盘图标和窗口定位

**第 2 步: 集成到 TutorialService** (半天)
1. 打开 `Pulsar/Services/TutorialService.cs`
2. 添加 `TutorialOrchestrator` 依赖注入
3. 连接 Orchestrator 事件到 Service 事件
4. 实现暂停/恢复功能

**第 3 步: UI 标记** (半天)
1. 在 `SettingsWindow.xaml` 中添加 `tutorial:TutorialMarker.Id` 标记
2. 在 `SettingsSlotsPage.xaml` 中标记关键按钮
3. 在导航栏中标记 "槽位配置" 项

**第 4 步: App.xaml.cs 集成** (半天)
1. 在 `ConfigureServices()` 中注册 `ITutorialService`
2. 在 `OnStartup()` 中添加首次启动检测逻辑

**第 5 步: 测试和调试** (1-2 天)
1. 删除 `%AppData%\Pulsar\Profiles.json` 中的 `HasCompletedTutorial` 标志
2. 运行应用，测试完整教程流程
3. 修复 Bug 和边界情况
4. 优化 UI 和动画

#### 快速验证环境

```bash
# 1. 构建项目
dotnet build "G:\0_Playground\Pulsar_Project\Pulsar\Pulsar\Pulsar.csproj"

# 2. 验证新文件存在
ls "G:\0_Playground\Pulsar_Project\Pulsar\Pulsar\Services\Tutorial\TriggerHandlers\"
ls "G:\0_Playground\Pulsar_Project\Pulsar\Pulsar\Helpers\Tutorial\"

# 3. 运行项目
dotnet run --project "G:\0_Playground\Pulsar_Project\Pulsar\Pulsar\Pulsar.csproj"
```

### 对于代码审查者

**重点检查**:
- ✅ 数据模型设计是否合理
- ✅ UI 组件是否符合设计规范
- ✅ 代码注释是否清晰
- ✅ 是否遵循项目编码规范

**已验证**:
- ✅ 项目编译通过
- ✅ 无警告无错误
- ✅ XAML 代码生成正常

---

**文档结束**

*如有任何疑问，请参考设计文档或查看代码注释。祝开发顺利！*
