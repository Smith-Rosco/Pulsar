# Pulsar Interactive Tutorial System Design (Part 4 - Final)

**This is the final part of the Tutorial System Design**

---

## 9. Implementation Roadmap

### Phase 1: Core Infrastructure (Week 1-2)

#### Milestone 1.1: Data Models & Service Interface
- [ ] Create `TutorialStep`, `TutorialTarget`, `TutorialTrigger` models
- [ ] Implement `ITutorialService` interface
- [ ] Add `HasCompletedTutorial` and `LastTutorialStep` to `ProfileSettings`
- [ ] Update `ConfigService` to persist tutorial state

#### Milestone 1.2: Overlay Window
- [ ] Create `TutorialOverlayWindow` with transparency
- [ ] Implement spotlight effect with `CombinedGeometry`
- [ ] Add click-through support for spotlight region
- [ ] Test on multiple monitors and DPI settings

#### Milestone 1.3: Instruction Card
- [ ] Create `TutorialStepCard` UserControl
- [ ] Implement arrow positioning logic
- [ ] Add animations (fade in, scale, arrow pulse)
- [ ] Support light/dark theme

**Deliverable**: Basic overlay + card system working in isolation

---

### Phase 2: State Machine & Triggers (Week 3-4)

#### Milestone 2.1: Tutorial Orchestrator
- [ ] Implement `TutorialOrchestrator` state machine
- [ ] Define all 9 tutorial steps
- [ ] Add step navigation (Next, Skip, Pause)
- [ ] Persist progress to config

#### Milestone 2.2: Trigger Handlers
- [ ] Implement `WindowOpenedTriggerHandler`
- [ ] Implement `PageNavigatedTriggerHandler`
- [ ] Implement `SlotAddedTriggerHandler`
- [ ] Implement `RadialMenuShownTriggerHandler`
- [ ] Add trigger detection tests

#### Milestone 2.3: Target Locator
- [ ] Create `TutorialMarker` attached property
- [ ] Implement `TutorialTargetRegistry`
- [ ] Add markers to key UI elements (AddSlotButton, Navigation items)
- [ ] Implement `TrayIconLocator` with Win32 API

**Deliverable**: Complete tutorial flow working end-to-end

---

### Phase 3: Enhanced Process Picker (Week 5)

#### Milestone 3.1: Common Applications Database
- [ ] Create `CommonApplications` static database
- [ ] Add 20+ common applications with icons
- [ ] Support multiple executable names per app

#### Milestone 3.2: Enhanced Picker UI
- [ ] Create `EnhancedProcessPickerViewModel`
- [ ] Design 3-tab UI (Running, Common, Manual)
- [ ] Implement search/filter functionality
- [ ] Add to Slot editing dialog

**Deliverable**: Improved process selection experience

---

### Phase 4: Integration & Polish (Week 6)

#### Milestone 4.1: App Integration
- [ ] Register `ITutorialService` in `App.xaml.cs`
- [ ] Add first-launch detection
- [ ] Implement resume dialog for interrupted tutorials
- [ ] Add "Restart Tutorial" button in Settings

#### Milestone 4.2: UI/UX Polish
- [ ] Fine-tune animations and transitions
- [ ] Add sound effects (optional)
- [ ] Test on different screen resolutions
- [ ] Accessibility improvements (keyboard navigation)

#### Milestone 4.3: Testing & Documentation
- [ ] User testing with 5+ participants
- [ ] Fix bugs and edge cases
- [ ] Update AGENTS.md with tutorial system info
- [ ] Create user-facing tutorial documentation

**Deliverable**: Production-ready tutorial system

---

## 10. Testing Strategy

### 10.1 Unit Tests

```csharp
[TestClass]
public class TutorialOrchestratorTests
{
    [TestMethod]
    public async Task StartAsync_ShouldShowFirstStep()
    {
        var orchestrator = new TutorialOrchestrator(...);
        await orchestrator.StartAsync();
        
        Assert.AreEqual("step1_welcome", orchestrator.CurrentStep.Id);
    }
    
    [TestMethod]
    public async Task NextStepAsync_ShouldAdvanceToNextStep()
    {
        var orchestrator = new TutorialOrchestrator(...);
        await orchestrator.StartAsync();
        await orchestrator.NextStepAsync();
        
        Assert.AreEqual("step2_open_settings", orchestrator.CurrentStep.Id);
    }
    
    [TestMethod]
    public async Task CompleteAsync_ShouldMarkTutorialAsCompleted()
    {
        var orchestrator = new TutorialOrchestrator(...);
        await orchestrator.StartAsync();
        
        // Advance through all steps
        for (int i = 0; i < 8; i++)
        {
            await orchestrator.NextStepAsync();
        }
        
        var config = await _configService.LoadAsync();
        Assert.IsTrue(config.Settings.HasCompletedTutorial);
    }
}
```

### 10.2 Integration Tests

```csharp
[TestClass]
public class TutorialIntegrationTests
{
    [TestMethod]
    public async Task FullTutorialFlow_ShouldCompleteSuccessfully()
    {
        // Simulate user going through entire tutorial
        var tutorialService = GetService<ITutorialService>();
        
        await tutorialService.StartTutorialAsync();
        
        // Step 1: Welcome
        SimulateButtonClick("StartTutorial");
        
        // Step 2: Open Settings
        SimulateTrayIconClick();
        await WaitForWindow<SettingsWindow>();
        
        // Step 3: Settings Overview
        SimulateButtonClick("NextStep");
        
        // ... continue for all steps
        
        Assert.IsTrue(tutorialService.HasCompletedTutorial);
    }
}
```

### 10.3 Manual Testing Checklist

- [ ] First launch triggers tutorial automatically
- [ ] Tutorial can be skipped at any step
- [ ] Tutorial can be paused and resumed
- [ ] Spotlight correctly highlights target elements
- [ ] Click-through works in spotlight region
- [ ] Tutorial works on multiple monitors
- [ ] Tutorial works with different DPI settings (100%, 125%, 150%)
- [ ] Tutorial works in both light and dark themes
- [ ] Tutorial state persists across app restarts
- [ ] "Restart Tutorial" button works in Settings
- [ ] All trigger handlers detect user actions correctly
- [ ] Notepad slot creation works as expected
- [ ] SendKeys demo works in Notepad
- [ ] Tutorial completion marks flag correctly

---

## 11. Future Enhancements

### 11.1 Advanced Features (Post-MVP)

#### Interactive Hints System
- Context-sensitive tooltips for advanced features
- "Did you know?" tips on idle
- Feature discovery prompts

#### Tutorial Analytics
- Track completion rate
- Identify drop-off points
- A/B test different tutorial flows

#### Localization
- Multi-language support
- Region-specific examples (e.g., different date formats)

#### Video Tutorials
- Embedded video player for complex features
- Screen recording of tutorial steps
- YouTube integration

### 11.2 Plugin Tutorial System

Allow plugins to register their own tutorial steps:

```csharp
public interface IPluginTutorial
{
    string PluginId { get; }
    List<TutorialStep> GetTutorialSteps();
}

// Example: VbaRunner plugin tutorial
public class VbaRunnerTutorial : IPluginTutorial
{
    public string PluginId => "com.pulsar.vbarunner";
    
    public List<TutorialStep> GetTutorialSteps()
    {
        return new List<TutorialStep>
        {
            new TutorialStep
            {
                Id = "vba_step1",
                Title = "VBA Runner 简介",
                Description = "VBA Runner 可以在 Excel 中执行 VBA 脚本...",
                // ...
            }
        };
    }
}
```

---

## 12. Success Metrics

### 12.1 Quantitative Metrics

| Metric | Target | Measurement |
|--------|--------|-------------|
| **Tutorial Completion Rate** | > 70% | % of users who complete all 9 steps |
| **Time to Complete** | < 2 minutes | Average time from start to finish |
| **Skip Rate** | < 30% | % of users who skip tutorial |
| **Resume Rate** | > 50% | % of interrupted users who resume |
| **Feature Adoption** | > 80% | % of users who use taught features within 7 days |

### 12.2 Qualitative Metrics

- User feedback surveys (1-5 star rating)
- Support ticket reduction (fewer "how to use" questions)
- User testimonials and reviews

---

## 13. Risk Assessment

### 13.1 Technical Risks

| Risk | Probability | Impact | Mitigation |
|------|-------------|--------|------------|
| **Overlay blocks user input** | Medium | High | Implement click-through for spotlight region |
| **Tray icon location fails** | Medium | Medium | Fallback to highlight entire tray area |
| **Tutorial breaks on Windows updates** | Low | Medium | Comprehensive testing on Windows 10/11 |
| **Performance impact** | Low | Low | Lazy load tutorial assets, dispose properly |

### 13.2 UX Risks

| Risk | Probability | Impact | Mitigation |
|------|-------------|--------|------------|
| **Users skip tutorial** | High | Medium | Make tutorial engaging, show value upfront |
| **Tutorial too long** | Medium | High | Keep under 2 minutes, allow skip |
| **Instructions unclear** | Medium | High | User testing, iterate on copy |
| **Tutorial feels forced** | Low | Medium | Prominent skip button, never block |

---

## 14. Appendix

### 14.1 Tutorial Script (Full Text)

#### Step 1: Welcome
```
🎉 欢迎使用 Pulsar！

Pulsar 是一个基于肌肉记忆的快速启动器
让我们用 30 秒了解核心功能

核心特性：
• 全局热键触发，无需鼠标
• 两种模式：切换窗口 & 执行命令
• 空间定位，盲操作友好

[开始教程]  [稍后再说]
```

#### Step 2: Open Settings
```
📌 打开设置界面

请左键单击任务栏托盘中的 Pulsar 图标
（或右键选择"设置"）

💡 提示：托盘图标通常在屏幕右下角

[跳过教程]
```

#### Step 3: Settings Overview
```
⚙️ 设置界面导览

这里可以配置 Pulsar 的所有功能：

• 常规 - 热键、主题、启动项
• 槽位配置 - 为不同应用配置快捷操作
• 插件 - 管理功能扩展

接下来我们将配置一个实际案例

[下一步]
```

#### Step 4: Navigate to Slots
```
🎯 进入槽位配置

请点击左侧导航栏的"槽位配置"

槽位配置是 Pulsar 的核心功能：
• 为不同应用配置专属快捷操作
• 支持两种模式：切换窗口 & 执行命令

[跳过教程]
```

#### Step 5: Add Launch Notepad Slot
```
➕ 添加第一个槽位

我们将添加一个"打开记事本"的槽位

步骤：
1. 点击右上角的 [+ 添加槽位] 按钮
2. 选择"切换模式"
3. 插件类型选择"Window Switcher"
4. 进程名输入：notepad
5. 标签输入：记事本
6. 点击保存

💡 提示：如果记事本未运行，Pulsar 会自动启动它

[跳过教程]
```

#### Step 6: Test Switch Mode
```
🚀 测试切换模式

现在让我们测试刚才配置的槽位！

步骤：
1. 按下 Ctrl+Shift+Q 触发切换模式
2. 移动鼠标到"记事本"槽位
3. 释放鼠标，Pulsar 会打开记事本

💡 提示：轮盘菜单会在鼠标位置显示

[跳过教程]
```

#### Step 7: Add Notepad Command Slot
```
📝 配置记事本专属命令

现在记事本已经打开，我们为它配置一个快捷命令

步骤：
1. 在左侧进程列表中选择"Notepad"
2. 切换到"命令模式"标签页
3. 点击 [+ 添加槽位]
4. 插件类型选择"Simple Command"
5. 动作选择"sendkeys"
6. 参数 keys 输入：Hello from Pulsar!{ENTER}
7. 标签输入：插入问候
8. 点击保存

[跳过教程]
```

#### Step 8: Test Command Mode
```
⚡ 测试命令模式

最后一步！让我们测试刚才配置的命令

步骤：
1. 确保记事本窗口处于激活状态
2. 按下 Ctrl+Q 触发命令模式
3. 移动鼠标到"插入问候"槽位
4. 释放鼠标，查看记事本中的变化

💡 提示：命令模式会根据当前激活的应用显示不同的操作

[跳过教程]
```

#### Step 9: Completion
```
🎉 恭喜！教程完成

你已经掌握了 Pulsar 的核心功能：

✅ 切换模式 (Ctrl+Shift+Q)
   快速切换到其他应用

✅ 命令模式 (Ctrl+Q)
   为当前应用执行快捷操作

✅ 槽位配置
   自定义你的工作流

💡 提示：
• 在设置中可以随时重新查看教程
• 支持为每个应用配置专属操作
• 更多插件请访问插件市场

[完成]
```

### 14.2 Glossary

- **Tutorial**: 交互式引导系统
- **Overlay**: 全屏半透明遮罩层
- **Spotlight**: 聚光灯效果，高亮目标区域
- **Instruction Card**: 指令卡片，显示当前步骤说明
- **Trigger**: 触发器，检测用户操作完成
- **Orchestrator**: 状态机，管理 Tutorial 流程
- **Coach Marks**: 教练标记，UI 引导模式

### 14.3 References

- [VS Code Welcome Experience](https://code.visualstudio.com/docs/getstarted/introvideos)
- [Figma Onboarding](https://www.figma.com/community/file/1234567890)
- [Notion Quick Start](https://www.notion.so/help/guides/get-started)
- [WPF Overlay Tutorial](https://docs.microsoft.com/en-us/dotnet/desktop/wpf/)
- [Material Design Onboarding](https://material.io/design/communication/onboarding.html)

---

## 15. Document Summary

### Key Decisions

1. ✅ **Non-intrusive Design**: Spotlight + Click-through overlay
2. ✅ **Zero Dependencies**: Use Notepad as demo application
3. ✅ **Complete Flow**: Cover both Switch Mode and Command Mode
4. ✅ **Enhanced Picker**: Support running/common/manual process selection
5. ✅ **Simplified Example**: Use "Hello from Pulsar!" instead of complex SendKeys
6. ✅ **Resume Support**: Persist progress for interrupted tutorials
7. ✅ **Flexible Triggers**: Support multiple trigger types (window, navigation, slot, hotkey)

### Architecture Highlights

- **Service Layer**: `ITutorialService` for clean separation
- **State Machine**: `TutorialOrchestrator` manages flow
- **UI Components**: `TutorialOverlayWindow` + `TutorialStepCard`
- **Target Locator**: `TutorialMarker` attached property + registry
- **Trigger System**: Pluggable handlers for different event types

### Implementation Timeline

- **Phase 1** (Week 1-2): Core infrastructure
- **Phase 2** (Week 3-4): State machine & triggers
- **Phase 3** (Week 5): Enhanced process picker
- **Phase 4** (Week 6): Integration & polish

**Total Estimated Time**: 6 weeks

---

**Document Status**: ✅ Complete and Ready for Review  
**Next Steps**: Review with team → Approve → Begin Phase 1 implementation

---

*End of Tutorial System Design Document*

