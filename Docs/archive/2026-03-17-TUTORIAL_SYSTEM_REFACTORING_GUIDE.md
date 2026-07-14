# Pulsar Tutorial System - 架构重构指导文档

**文档版本**: v1.0  
**创建日期**: 2026-03-16  
**目标读者**: AI Agent / 开发者  
**预计工作量**: 5-7 天  
**优先级**: P0 (高优先级)

---

## 📋 目录

1. [执行摘要](#执行摘要)
2. [整体架构评估](#整体架构评估)
3. [关键问题清单](#关键问题清单)
4. [重构路线图](#重构路线图)
5. [详细重构指南](#详细重构指南)
6. [测试策略](#测试策略)
7. [验收标准](#验收标准)

---

## 📊 执行摘要

### 当前状态

- **完成度**: Phase 2 约 80% 完成
- **代码质量**: 7/10
- **架构评分**: 7.5/10
- **主要问题**: 职责过重、缺少配置加载器、内存泄漏风险

### 重构目标

1. ✅ 降低 TutorialOrchestrator 复杂度 (从 549 行降至 ~200 行)
2. ✅ 实现 JSON 配置加载器
3. ✅ 修复内存泄漏问题
4. ✅ 实现状态恢复机制
5. ✅ 完善错误处理
6. ✅ 添加单元测试

### 预期收益

| 指标 | 当前 | 目标 | 提升 |
|------|------|------|------|
| 代码可维护性 | 6/10 | 9/10 | +50% |
| 测试覆盖率 | 0% | 70% | +70% |
| 内存泄漏风险 | 高 | 低 | -80% |
| 教程完成率 | 70% | 85% | +15% |

---

## 🏗️ 整体架构评估

### 当前架构

```
ITutorialService (Service Layer)
    ↓
TutorialOrchestrator (549 行 - 职责过重)
    ├─ 状态机逻辑
    ├─ UI 窗口管理
    ├─ Win32 API 调用
    ├─ 触发器管理
    └─ 配置持久化
    ↓
TutorialOverlayWindow + TutorialStepCard
```

### 目标架构

```
ITutorialService (Service Layer)
    ↓
TutorialOrchestrator (简化至 ~200 行)
    ├─ ITargetLocator (新增 - 目标定位)
    ├─ IOverlayManager (新增 - UI 管理)
    ├─ ITriggerHandlerFactory (优化 - DI 支持)
    └─ TutorialStepLoader (实现 - JSON 加载)
    ↓
TutorialOverlayWindow + TutorialStepCard
```

### 架构优势

1. **单一职责**: 每个组件职责明确
2. **易于测试**: 可 Mock 所有依赖
3. **易于扩展**: 新增功能只需添加新服务
4. **配置分离**: JSON 配置与代码解耦

---

## 🚨 关键问题清单

### P0 - 必须修复 (阻塞发布)

| ID | 问题 | 影响 | 状态 | 文件 |
|----|------|------|------|------|
| P0-1 | TutorialStepLoader 未实现 | 无法运行 | ✅ 已修复 | TutorialStepLoader.cs |
| P0-2 | 内存泄漏 (事件未取消订阅) | 长期运行崩溃 | ✅ 已修复 | TutorialOrchestrator.cs:356-377 |
| P0-3 | GoToStepAsync 未实现 | 无法恢复教程 | ✅ 已修复 | TutorialService.cs:169 |
| P0-4 | 缺少错误边界 (async void) | 异常导致崩溃 | ✅ 已修复 | TutorialOrchestrator.cs:420-530 |
| P0-5 | 竞态条件 (快速点击) | 状态错误/跳步 | ✅ 已修复 | TutorialOrchestrator.cs:41 |
| P0-4 | 缺少错误边界处理 | 教程崩溃 | 0.5 天 | TutorialOrchestrator.cs:462 |

### P1 - 应该优化 (影响质量)

| ID | 问题 | 影响 | 工作量 | 文件 |
|----|------|------|--------|------|
| P1-1 | TutorialOrchestrator 职责过重 | 难以维护 | 3 天 | TutorialOrchestrator.cs |
| P1-2 | 触发器工厂缺少 DI | 依赖注入不完整 | 1 天 | TriggerHandlerFactory.cs |
| P1-3 | 教程步骤过多 (9 步) | 完成率低 | 2 天 | 设计调整 |
| P1-4 | 托盘图标定位不可靠 | 跨版本兼容性差 | 1 天 | TutorialOrchestrator.cs:256 |

### P2 - 可以延后 (优化项)

| ID | 问题 | 影响 | 工作量 |
|----|------|------|--------|
| P2-1 | 全屏遮罩性能优化 | 流畅度 | 2 天 |
| P2-2 | 缺少单元测试 | 质量保障 | 3 天 |

---

## 🗺️ 重构路线图

### Phase 1: 紧急修复 (2 天)

**目标**: 修复阻塞问题,确保系统可运行

- [ ] Day 1 上午: 实现 TutorialStepLoader (P0-1)
- [ ] Day 1 下午: 修复内存泄漏 (P0-2)
- [ ] Day 2 上午: 实现 GoToStepAsync (P0-3)
- [ ] Day 2 下午: 添加错误边界处理 (P0-4)

**验收标准**:
- ✅ 教程可以正常启动和完成
- ✅ 无内存泄漏警告
- ✅ 支持断点续传

### Phase 2: 架构重构 (3 天)

**目标**: 降低复杂度,提升可维护性

- [ ] Day 3: 提取 ITargetLocator 服务 (P1-1)
- [ ] Day 4: 提取 IOverlayManager 服务 (P1-1)
- [ ] Day 5: 优化触发器工厂 DI (P1-2)

**验收标准**:
- ✅ TutorialOrchestrator 降至 ~200 行
- ✅ 所有服务可独立测试
- ✅ 依赖注入完整

### Phase 3: 用户体验优化 (2 天)

**目标**: 提升教程完成率

- [ ] Day 6: 简化教程流程 (P1-3)
- [ ] Day 7: 优化托盘图标定位 (P1-4)

**验收标准**:
- ✅ 教程步骤减少至 6-7 步
- ✅ 托盘图标定位成功率 > 90%

---

## 📖 详细重构指南

### 重构 1: 实现 TutorialStepLoader (P0-1)

**优先级**: P0 (必须)  
**工作量**: 1 天  
**文件**: `Pulsar/Services/Tutorial/TutorialStepLoader.cs`

#### 问题描述

```csharp
// TutorialOrchestrator.cs:78
var steps = _stepLoader.LoadSteps(); // 当前未实现,返回空列表
```

当前 TutorialStepLoader 类存在但未实现,导致教程无法加载步骤。

#### 解决方案

**步骤 1: 创建 JSON 配置文件**

创建文件: `Pulsar/Assets/TutorialSteps.json`

```json
{
  "version": "1.0",
  "steps": [
    {
      "id": "step1_welcome",
      "title": "欢迎使用 Pulsar",
      "description": "Pulsar 是一个基于肌肉记忆的快速启动器\n让我们用 30 秒了解核心功能\n\n核心特性：\n• 全局热键触发，无需鼠标\n• 两种模式：切换窗口 & 执行命令\n• 空间定位，盲操作友好",
      "type": "Instruction",
      "focusMode": "AlwaysFocused",
      "layout": {
        "cardPosition": "Center",
        "cardSizeMode": "Fixed",
        "fixedCardWidth": 500,
        "fixedCardHeight": 400
      },
      "completionTrigger": {
        "type": "ButtonClick"
      }
    },
    {
      "id": "step2_open_settings",
      "title": "打开设置界面",
      "description": "请左键单击任务栏托盘中的 Pulsar 图标\n（或右键选择"设置"）\n\n💡 提示：托盘图标通常在屏幕右下角",
      "type": "WaitForAction",
      "focusMode": "AlwaysObserving",
      "target": {
        "type": "TrayIcon"
      },
      "layout": {
        "cardPosition": "BottomRight",
        "cardSizeMode": "Auto"
      },
      "completionTrigger": {
        "type": "WindowOpened",
        "targetValue": "SettingsWindow"
      }
    }
  ]
}
```

**注意**: 完整的 9 个步骤配置见附录 A。

**步骤 2: 实现 TutorialStepLoader**

```csharp
// Pulsar/Services/Tutorial/TutorialStepLoader.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Pulsar.Models.Tutorial;

namespace Pulsar.Services.Tutorial
{
    /// <summary>
    /// 教程步骤加载器 - 从 JSON 文件加载教程配置
    /// </summary>
    public class TutorialStepLoader
    {
        private readonly ILogger<TutorialStepLoader> _logger;
        private const string StepsFileName = "TutorialSteps.json";
        
        public TutorialStepLoader(ILogger<TutorialStepLoader> logger)
        {
            _logger = logger;
        }
        
        /// <summary>
        /// 加载教程步骤
        /// </summary>
        public List<TutorialStep> LoadSteps()
        {
            try
            {
                var filePath = GetStepsFilePath();
                
                if (!File.Exists(filePath))
                {
                    _logger.LogWarning("[TutorialStepLoader] Steps file not found: {Path}", filePath);
                    return GetFallbackSteps();
                }
                
                var json = File.ReadAllText(filePath);
                var config = JsonSerializer.Deserialize<TutorialConfig>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    ReadCommentHandling = JsonCommentHandling.Skip
                });
                
                if (config == null || config.Steps == null)
                {
                    _logger.LogError("[TutorialStepLoader] Failed to deserialize tutorial config");
                    return GetFallbackSteps();
                }
                
                // 验证配置
                ValidateConfig(config);
                
                _logger.LogInformation("[TutorialStepLoader] Loaded {Count} tutorial steps from {Path}", 
                    config.Steps.Count, filePath);
                
                return config.Steps;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[TutorialStepLoader] Error loading tutorial steps");
                return GetFallbackSteps();
            }
        }
        
        /// <summary>
        /// 获取步骤文件路径
        /// </summary>
        private string GetStepsFilePath()
        {
            // 尝试多个可能的路径
            var basePath = AppDomain.CurrentDomain.BaseDirectory;
            
            var paths = new[]
            {
                Path.Combine(basePath, "Assets", StepsFileName),
                Path.Combine(basePath, StepsFileName),
                Path.Combine(Directory.GetCurrentDirectory(), "Assets", StepsFileName)
            };
            
            foreach (var path in paths)
            {
                if (File.Exists(path))
                {
                    return path;
                }
            }
            
            return paths[0]; // 返回默认路径
        }
        
        /// <summary>
        /// 验证配置有效性
        /// </summary>
        private void ValidateConfig(TutorialConfig config)
        {
            if (config.Steps.Count == 0)
            {
                throw new InvalidOperationException("Tutorial config must contain at least one step");
            }
            
            // 验证步骤 ID 唯一性
            var ids = new HashSet<string>();
            foreach (var step in config.Steps)
            {
                if (string.IsNullOrEmpty(step.Id))
                {
                    throw new InvalidOperationException("Tutorial step must have an ID");
                }
                
                if (!ids.Add(step.Id))
                {
                    throw new InvalidOperationException($"Duplicate step ID: {step.Id}");
                }
            }
            
            _logger.LogDebug("[TutorialStepLoader] Config validation passed");
        }
        
        /// <summary>
        /// 获取 Fallback 步骤 (硬编码)
        /// </summary>
        private List<TutorialStep> GetFallbackSteps()
        {
            _logger.LogWarning("[TutorialStepLoader] Using fallback steps");
            
            return new List<TutorialStep>
            {
                new TutorialStep
                {
                    Id = "step1_welcome",
                    Title = "欢迎使用 Pulsar",
                    Description = "Pulsar 是一个基于肌肉记忆的快速启动器",
                    Type = TutorialStepType.Instruction,
                    FocusMode = TutorialFocusMode.AlwaysFocused,
                    Layout = new TutorialLayout
                    {
                        CardPosition = CardPosition.Center,
                        CardSizeMode = CardSizeMode.Auto
                    },
                    CompletionTrigger = new TutorialTrigger
                    {
                        Type = TutorialTriggerType.ButtonClick
                    }
                }
                // 添加更多 fallback 步骤...
            };
        }
    }
    
    /// <summary>
    /// 教程配置根对象
    /// </summary>
    public class TutorialConfig
    {
        public string Version { get; set; } = "1.0";
        public List<TutorialStep> Steps { get; set; } = new();
    }
}
```

**步骤 3: 更新项目文件**

确保 `TutorialSteps.json` 被复制到输出目录:

```xml
<!-- Pulsar.csproj -->
<ItemGroup>
  <None Update="Assets\TutorialSteps.json">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  </None>
</ItemGroup>
```

**步骤 4: 注册到 DI 容器**

```csharp
// App.xaml.cs - ConfigureServices
serviceCollection.AddSingleton<TutorialStepLoader>();
```

#### 验收标准

- [ ] JSON 文件存在且格式正确
- [ ] TutorialStepLoader 可以成功加载 9 个步骤
- [ ] 如果文件不存在,使用 Fallback 步骤
- [ ] 日志记录加载过程
- [ ] 验证步骤 ID 唯一性

#### 测试方法

```csharp
// 手动测试
var loader = new TutorialStepLoader(logger);
var steps = loader.LoadSteps();
Assert.AreEqual(9, steps.Count);
Assert.AreEqual("step1_welcome", steps[0].Id);
```

---

### 重构 2: 修复内存泄漏 (P0-2)

**优先级**: P0 (必须)  
**工作量**: 0.5 天  
**文件**: `Pulsar/Services/Tutorial/TutorialOrchestrator.cs`

#### 问题描述

```csharp
// TutorialOrchestrator.cs:153-156
_stepCard = new TutorialStepCard();
_stepCard.SetStep(step, _currentStepIndex, _steps.Count);
_stepCard.NextClicked += OnStepCardNextClicked;
_stepCard.SkipClicked += OnStepCardSkipClicked;
// ❌ 未在清理时取消订阅,导致内存泄漏
```

每次显示新步骤时创建新的 `TutorialStepCard`,但未取消旧卡片的事件订阅,导致:
1. 事件处理器累积
2. 旧对象无法被 GC 回收
3. 长期运行后内存占用增加

#### 解决方案

**步骤 1: 添加清理方法**

```csharp
// TutorialOrchestrator.cs
private TutorialStepCard? _stepCard;

/// <summary>
/// 清理步骤卡片资源
/// </summary>
private void CleanupStepCard()
{
    if (_stepCard != null)
    {
        _stepCard.NextClicked -= OnStepCardNextClicked;
        _stepCard.SkipClicked -= OnStepCardSkipClicked;
        _stepCard = null;
        
        _logger.LogDebug("[TutorialOrchestrator] Step card cleaned up");
    }
}
```

**步骤 2: 在 ShowStepAsync 中调用清理**

```csharp
private async Task ShowStepAsync(TutorialStep step)
{
    _logger.LogInformation("[TutorialOrchestrator] Showing step: {StepId}", step.Id);

    // 清理上一步的触发器
    CleanupCurrentTrigger();
    
    // ✅ 添加: 清理上一步的卡片
    CleanupStepCard();

    // 更新配置中的当前步骤
    await UpdateConfigAsync(s => s.LastTutorialStep = step.Id);

    // 创建或更新遮罩窗口
    EnsureOverlayWindow();

    // ... 其余代码保持不变 ...

    // 创建并显示步骤卡片
    _stepCard = new TutorialStepCard();
    _stepCard.SetStep(step, _currentStepIndex, _steps.Count);
    _stepCard.NextClicked += OnStepCardNextClicked;
    _stepCard.SkipClicked += OnStepCardSkipClicked;

    _overlayWindow!.SetCardContent(_stepCard);
    
    // ... 其余代码 ...
}
```

**步骤 3: 在 CompleteAsync 中调用清理**

```csharp
private async Task CompleteAsync()
{
    _logger.LogInformation("[TutorialOrchestrator] Tutorial completed");

    await UpdateConfigAsync(s =>
    {
        s.HasCompletedTutorial = true;
        s.LastTutorialStep = null;
    });

    CleanupCurrentTrigger();
    CleanupStepCard(); // ✅ 添加清理
    
    _overlayWindow?.Close();
    _overlayWindow = null;

    TutorialCompleted?.Invoke(this, EventArgs.Empty);
}
```

**步骤 4: 在 OnStepCardSkipClicked 中调用清理**

```csharp
private async void OnStepCardSkipClicked(object? sender, EventArgs e)
{
    _logger.LogInformation("[TutorialOrchestrator] Tutorial skipped by user");

    await UpdateConfigAsync(s =>
    {
        s.HasCompletedTutorial = true;
        s.LastTutorialStep = null;
    });

    CleanupCurrentTrigger();
    CleanupStepCard(); // ✅ 添加清理
    
    _overlayWindow?.Close();
    _overlayWindow = null;
}
```

#### 验收标准

- [ ] 每次显示新步骤前清理旧卡片
- [ ] 教程完成/跳过时清理卡片
- [ ] 使用内存分析工具验证无泄漏
- [ ] 日志记录清理操作

#### 测试方法

```csharp
// 使用 Visual Studio Diagnostic Tools
// 1. 启动教程
// 2. 连续点击"下一步" 9 次
// 3. 观察内存占用是否稳定
// 4. 重复 10 次,内存不应持续增长
```

---

### 重构 3: 实现 GoToStepAsync (P0-3)

**优先级**: P0 (必须)  
**工作量**: 1 天  
**文件**: `Pulsar/Services/TutorialService.cs`, `TutorialOrchestrator.cs`

#### 问题描述

```csharp
// TutorialService.cs:169-176
public async Task GoToStepAsync(string stepId)
{
    _logger.LogInformation("Navigating to tutorial step: {StepId}", stepId);
    
    // TODO: Implement step navigation
    // This will be implemented in Phase 2 with TutorialOrchestrator
    await Task.CompletedTask;
}
```

当前无法跳转到指定步骤,导致:
1. 无法实现断点续传
2. 无法支持"重新开始教程"功能
3. 用户体验差

#### 解决方案

**步骤 1: 在 TutorialOrchestrator 中添加方法**

```csharp
// TutorialOrchestrator.cs

/// <summary>
/// 跳转到指定步骤
/// </summary>
public async Task GoToStepAsync(int stepIndex)
{
    if (stepIndex < 0 || stepIndex >= _steps.Count)
    {
        _logger.LogError("[TutorialOrchestrator] Invalid step index: {Index}", stepIndex);
        throw new ArgumentOutOfRangeException(nameof(stepIndex), 
            $"Step index must be between 0 and {_steps.Count - 1}");
    }
    
    _logger.LogInformation("[TutorialOrchestrator] Jumping to step {Index}: {StepId}", 
        stepIndex, _steps[stepIndex].Id);
    
    _currentStepIndex = stepIndex;
    await ShowStepAsync(CurrentStep!);
}

/// <summary>
/// 根据步骤 ID 查找索引
/// </summary>
public int FindStepIndex(string stepId)
{
    var index = _steps.FindIndex(s => s.Id == stepId);
    
    if (index == -1)
    {
        _logger.LogWarning("[TutorialOrchestrator] Step not found: {StepId}", stepId);
    }
    
    return index;
}

/// <summary>
/// 获取所有步骤 ID
/// </summary>
public List<string> GetAllStepIds()
{
    return _steps.Select(s => s.Id).ToList();
}
```

**步骤 2: 在 TutorialService 中实现**

```csharp
// TutorialService.cs

public async Task GoToStepAsync(string stepId)
{
    _logger.LogInformation("Navigating to tutorial step: {StepId}", stepId);
    
    var stepIndex = _orchestrator.FindStepIndex(stepId);
    if (stepIndex == -1)
    {
        _logger.LogWarning("Step not found: {StepId}", stepId);
        return;
    }
    
    _isTutorialActive = true;
    await _orchestrator.GoToStepAsync(stepIndex);
}
```

**步骤 3: 实现恢复对话框**

```csharp
// TutorialService.cs

/// <summary>
/// 检查是否需要恢复教程
/// </summary>
public async Task CheckResumeAsync()
{
    var config = _configService.Current;
    
    if (!config.Settings.HasCompletedTutorial 
        && !string.IsNullOrEmpty(config.Settings.LastTutorialStep))
    {
        _logger.LogInformation("Detected incomplete tutorial: {StepId}", 
            config.Settings.LastTutorialStep);
        
        // TODO: 显示恢复对话框
        // 暂时自动恢复
        await GoToStepAsync(config.Settings.LastTutorialStep);
    }
}
```

**步骤 4: 在 App.xaml.cs 中调用**

```csharp
// App.xaml.cs - OnStartup

var tutorialService = Services.GetRequiredService<ITutorialService>();
var configService = Services.GetRequiredService<IConfigService>();

if (!configService.Current.Settings.HasCompletedTutorial)
{
    await Task.Delay(1000); // 等待 UI 初始化
    
    // ✅ 检查是否需要恢复
    await tutorialService.CheckResumeAsync();
    
    // 如果没有恢复,启动新教程
    if (!tutorialService.IsTutorialActive)
    {
        await tutorialService.StartTutorialAsync();
    }
}
```

#### 验收标准

- [ ] 可以跳转到任意步骤
- [ ] 跳转后状态正确
- [ ] 支持断点续传
- [ ] 日志记录跳转操作
- [ ] 处理无效步骤 ID

#### 测试方法

```csharp
// 手动测试
// 1. 启动教程,完成 3 个步骤
// 2. 关闭应用
// 3. 重新启动应用
// 4. 验证从第 4 步继续
```

---

### 重构 4: 添加错误边界处理 (P0-4)

**优先级**: P0 (必须)  
**工作量**: 0.5 天  
**文件**: `Pulsar/Services/Tutorial/TutorialOrchestrator.cs`

#### 问题描述

```csharp
// TutorialOrchestrator.cs:462-466
private async void OnTriggerFired()
{
    _logger.LogInformation("[TutorialOrchestrator] Trigger fired for step: {StepId}", CurrentStep?.Id);
    await NextStepAsync(); // ❌ 如果抛异常,整个教程崩溃
}
```

当前缺少错误处理,导致:
1. 任何步骤出错都会导致教程崩溃
2. 用户无法恢复
3. 影响主应用稳定性

#### 解决方案

**步骤 1: 添加错误处理包装器**

```csharp
// TutorialOrchestrator.cs

private async void OnTriggerFired()
{
    try
    {
        _logger.LogInformation("[TutorialOrchestrator] Trigger fired for step: {StepId}", CurrentStep?.Id);
        await NextStepAsync();
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "[TutorialOrchestrator] Error advancing to next step");
        await HandleErrorAsync(ex);
    }
}

private async void OnStepCardNextClicked(object? sender, EventArgs e)
{
    try
    {
        // 特殊处理步骤二
        if (CurrentStep?.Id == "step2_open_settings")
        {
            // ... 现有代码 ...
            return;
        }
        
        await NextStepAsync();
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "[TutorialOrchestrator] Error in next button handler");
        await HandleErrorAsync(ex);
    }
}

private async void OnStepCardSkipClicked(object? sender, EventArgs e)
{
    try
    {
        _logger.LogInformation("[TutorialOrchestrator] Tutorial skipped by user");
        
        await UpdateConfigAsync(s =>
        {
            s.HasCompletedTutorial = true;
            s.LastTutorialStep = null;
        });

        CleanupCurrentTrigger();
        CleanupStepCard();
        
        _overlayWindow?.Close();
        _overlayWindow = null;
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "[TutorialOrchestrator] Error in skip button handler");
        // 跳过时出错,强制清理
        ForceCleanup();
    }
}
```

**步骤 2: 实现错误处理逻辑**

```csharp
/// <summary>
/// 处理教程错误
/// </summary>
private async Task HandleErrorAsync(Exception ex)
{
    _logger.LogError(ex, "[TutorialOrchestrator] Tutorial error occurred");
    
    // 清理资源
    CleanupCurrentTrigger();
    CleanupStepCard();
    
    // 标记为已完成,避免重复触发
    await UpdateConfigAsync(s =>
    {
        s.HasCompletedTutorial = true;
        s.LastTutorialStep = null;
    });
    
    // 关闭遮罩窗口
    _overlayWindow?.Close();
    _overlayWindow = null;
    
    // TODO: 显示错误对话框
    // 暂时只记录日志
}

/// <summary>
/// 强制清理所有资源
/// </summary>
private void ForceCleanup()
{
    try
    {
        CleanupCurrentTrigger();
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "[TutorialOrchestrator] Error cleaning up trigger");
    }
    
    try
    {
        CleanupStepCard();
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "[TutorialOrchestrator] Error cleaning up step card");
    }
    
    try
    {
        _overlayWindow?.Close();
        _overlayWindow = null;
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "[TutorialOrchestrator] Error closing overlay window");
    }
}
```

#### 验收标准

- [ ] 所有异步事件处理器都有 try-catch
- [ ] 错误时自动清理资源
- [ ] 错误时标记教程为已完成
- [ ] 日志记录所有错误

#### 测试方法

```csharp
// 模拟错误场景
// 1. 在 NextStepAsync 中抛出异常
// 2. 验证教程正常关闭
// 3. 验证资源被清理
// 4. 验证不会影响主应用
```

---

## 📝 Phase 1 总结

完成 Phase 1 后,你应该已经:

✅ 实现了 TutorialStepLoader,可以从 JSON 加载步骤  
✅ 修复了内存泄漏问题  
✅ 实现了 GoToStepAsync,支持断点续传  
✅ 添加了错误边界处理,提升健壮性

**下一步**: 继续 Phase 2 - 架构重构

---

