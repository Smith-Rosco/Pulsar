# Pulsar Interactive Tutorial System - Complete Design Document

**Status**: Draft for Review  
**Version**: v1.0.0  
**Last Updated**: 2026-03-15  
**Author**: Architecture Team  
**Related Documents**: [DIALOG_SYSTEM.md](./DIALOG_SYSTEM.md), [UI_BEST_PRACTICES.md](../guides/UI_BEST_PRACTICES.md)

---

## 📚 Document Structure

This design document is split into 4 parts for easier editing and review:

### Part 1: Overview & Architecture (TUTORIAL_SYSTEM.md)
- Project overview and design philosophy
- System architecture and core components
- Data models and interfaces
- Tutorial flow design (9 steps)

### Part 2: Technical Implementation (TUTORIAL_SYSTEM_PART2.md)
- TutorialOverlayWindow implementation
- State machine implementation
- Trigger detection handlers
- Integration points with existing code

### Part 3: Key Challenges & Solutions (TUTORIAL_SYSTEM_PART3.md)
- Enhanced process picker design
- UI element targeting strategy
- Tray icon location handling
- SendKeys parameter simplification
- Tutorial interruption and resume

### Part 4: Implementation Plan (TUTORIAL_SYSTEM_PART4.md)
- 6-week implementation roadmap
- Testing strategy and checklist
- Success metrics and KPIs
- Risk assessment
- Complete tutorial script text
- Glossary and references

---

## 🎯 Quick Summary

### What is it?

Pulsar Interactive Tutorial 是一个交互式引导系统，帮助新用户在 **30 秒内**理解并掌握 Pulsar 的核心功能。

### Key Features

1. ✅ **首次启动自动触发** - 新用户自动进入教程
2. ✅ **非侵入式设计** - 半透明遮罩 + 聚光灯效果，不阻塞操作
3. ✅ **完整功能覆盖** - 展示切换模式、命令模式、槽位配置
4. ✅ **零依赖示例** - 使用 Windows 内置的 Notepad
5. ✅ **可跳过/恢复** - 用户随时可以跳过或稍后继续
6. ✅ **设置中可重启** - 随时重新查看教程

### Tutorial Flow (9 Steps)

```
Step 1: Welcome & Introduction (5s)
   ↓
Step 2: Open Settings via Tray Icon (10s)
   ↓
Step 3: Settings Overview (8s)
   ↓
Step 4: Navigate to Slots Page (10s)
   ↓
Step 5: Add "Launch Notepad" Slot (20s)
   ↓
Step 6: Test Switch Mode (15s)
   ↓
Step 7: Add Notepad Command Slot (25s)
   ↓
Step 8: Test Command Mode (15s)
   ↓
Step 9: Completion & Summary (10s)

Total: ~2 minutes
```

### Architecture Overview

```
ITutorialService (Service Layer)
    ↓
TutorialOrchestrator (State Machine)
    ↓
TutorialOverlayWindow (Transparent Window with Spotlight)
    ↓
TutorialStepCard (Instruction Card with Arrow)
```

---

## 🚀 Implementation Timeline

| Phase | Duration | Deliverable |
|-------|----------|-------------|
| **Phase 1**: Core Infrastructure | Week 1-2 | Overlay + Card system |
| **Phase 2**: State Machine & Triggers | Week 3-4 | Complete tutorial flow |
| **Phase 3**: Enhanced Process Picker | Week 5 | Improved UX |
| **Phase 4**: Integration & Polish | Week 6 | Production ready |

**Total**: 6 weeks

---

## 📊 Success Metrics

| Metric | Target |
|--------|--------|
| Tutorial Completion Rate | > 70% |
| Time to Complete | < 2 minutes |
| Skip Rate | < 30% |
| Feature Adoption (7 days) | > 80% |

---

## 🔑 Key Design Decisions

### 1. Why Notepad as Demo?
- ✅ Windows 内置，100% 可用
- ✅ 启动快速（毫秒级）
- ✅ 易于演示 SendKeys 效果

### 2. Why Non-Intrusive Overlay?
- ✅ 不阻塞用户操作
- ✅ 聚光灯区域支持点击穿透
- ✅ 用户可随时跳过

### 3. Why 9 Steps?
- ✅ 覆盖核心功能（切换模式 + 命令模式）
- ✅ 控制在 2 分钟内完成
- ✅ 每步聚焦单一任务

### 4. Why Enhanced Process Picker?
- ✅ 解决"未打开应用无法选择"的痛点
- ✅ 提供常用应用预定义列表
- ✅ 减少输入错误

---

## 📖 How to Read This Document

### For Product Managers
- Read: Part 1 (Overview) + Part 4 (Implementation Plan)
- Focus: User flow, success metrics, timeline

### For Developers
- Read: All parts
- Focus: Part 2 (Technical Implementation) + Part 3 (Challenges)

### For Designers
- Read: Part 1 (Flow Design) + Part 4 (Tutorial Script)
- Focus: UI/UX specifications, visual design

### For QA
- Read: Part 4 (Testing Strategy)
- Focus: Manual testing checklist, edge cases

---

## 🔗 Related Documents

- [AGENTS.md](../../AGENTS.md) - AI agent operational guide
- [ARCHITECTURE.md](../../ARCHITECTURE.md) - System architecture overview
- [DIALOG_SYSTEM.md](./DIALOG_SYSTEM.md) - Dialog system architecture
- [UI_BEST_PRACTICES.md](../guides/UI_BEST_PRACTICES.md) - UI development guidelines
- [PLUGIN_DEVELOPMENT.md](../../PLUGIN_DEVELOPMENT.md) - Plugin development guide

---

## 📝 Review Checklist

Before approving this design, please verify:

- [ ] Tutorial flow covers all core features
- [ ] Each step has clear completion criteria
- [ ] UI mockups are clear and actionable
- [ ] Technical implementation is feasible
- [ ] Timeline is realistic
- [ ] Success metrics are measurable
- [ ] Risks are identified and mitigated
- [ ] Testing strategy is comprehensive

---

## 💬 Feedback & Questions

Please provide feedback on the following:

1. **Flow Design**: Is the 9-step flow logical and easy to follow?
2. **Technical Approach**: Are there better alternatives to the overlay + spotlight design?
3. **Process Picker**: Should we implement the enhanced picker in Phase 3 or defer to later?
4. **SendKeys Example**: Is "Hello from Pulsar!" simple enough, or should we use something else?
5. **Timeline**: Is 6 weeks realistic, or should we adjust scope?

---

## 📅 Next Steps

1. **Review Meeting** - Schedule with team (Product, Dev, Design, QA)
2. **Feedback Collection** - Gather comments and suggestions
3. **Design Iteration** - Update document based on feedback
4. **Approval** - Get sign-off from stakeholders
5. **Implementation** - Begin Phase 1 development

---

## 📄 Document History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| v1.0.0 | 2026-03-15 | Architecture Team | Initial draft |

---

**Status**: ✅ Ready for Review  
**Approval Required From**: Product Manager, Tech Lead, UX Designer

---

*For detailed content, please refer to the individual part documents.*

