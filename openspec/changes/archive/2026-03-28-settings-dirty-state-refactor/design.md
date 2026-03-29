## Context

`SettingsViewModel`（1942 行）是设置界面的核心 ViewModel，负责管理：
- 多配置页（Context）之间的切换
- 当前页 Slot 列表（`CurrentSlots: ObservableCollection<PluginSlot>`）
- 全局设置（`GeneralSettings: ProfileSettings`）
- 脏状态追踪（`HasUnsavedChanges`）→ 驱动 Save 按钮红点

当前脏状态通过 `MarkDirty()` 设置，但 `MarkDirty()` 被 **4 条非用户编辑路径**错误触发，导致用户未做任何改动时 Save 按钮已显示红点。

现有的保护机制 `_suppressSlotSync` 只能防止 `SyncSlotsToConfig()` 被调用，**不能**防止 `MarkDirty()` 被调用，两个职责混用在同一个布尔标志上。

## Goals / Non-Goals

**Goals:**
- 消除所有因加载/初始化/导航引起的脏状态误触发
- 建立单一、可审计的脏状态保护机制
- 保持现有功能语义完全不变（真实用户编辑仍然触发脏）
- 代码改动范围最小化，避免引入新的回归风险

**Non-Goals:**
- 重写 SettingsViewModel 的整体架构
- 修改任何接口（`IConfigService`、`IHotkeyService` 等）
- 改变 Save 的持久化逻辑
- 引入 Undo/Redo 能力

## Decisions

### 决策 1: 引入独立的 `_suppressDirty` 标志，与 `_suppressSlotSync` 分离

**选择**: 新增 `private bool _suppressDirty = false;`，在 `MarkDirty()` 入口处检查。

**为什么不复用 `_suppressSlotSync`**:
`_suppressSlotSync` 的语义是「不要把 UI 集合写回 config 草稿」，而 `_suppressDirty` 的语义是「当前操作是系统行为，不是用户编辑」。两者在某些路径上需要独立控制（例如：用户手动触发 Reload 时，需要 suppressDirty 但不一定需要 suppressSlotSync）。混用一个标志会使未来的维护更脆弱。

**实现**:
```csharp
private bool _suppressDirty = false;

private void MarkDirty()
{
    if (_suppressDirty) return;  // 新增保护
    _logger.LogDebug("MarkDirty called ...");
    HasUnsavedChanges = true;
    SaveCommand.NotifyCanExecuteChanged();
}

// Helper: 在静默块内执行 action
private void WithSuppressedDirty(Action action)
{
    _suppressDirty = true;
    try { action(); }
    finally { _suppressDirty = false; }
}

// Async 版本
private async Task WithSuppressedDirtyAsync(Func<Task> action)
{
    _suppressDirty = true;
    try { await action(); }
    finally { _suppressDirty = false; }
}
```

---

### 决策 2: `CurrentSlots` setter 改为「先清空再填充」而非「替换整个集合」

**问题根因**: `new ObservableCollection<PluginSlot>(sourceList)` 构造函数内部批量 Add，每次 Add 都触发 `CollectionChanged`，而此时事件订阅已经挂载。

**选择 A（当前方案）**: 替换整个集合引用 → 触发 CollectionChanged（误触发）

**选择 B（推荐）**: 保留集合引用，用 `_suppressDirty` 包裹填充过程：
```csharp
private void LoadSlotsFromSource(IEnumerable<PluginSlot> sourceList)
{
    WithSuppressedDirty(() =>
    {
        _currentSlots.Clear();
        foreach (var slot in sourceList.OrderBy(s => s.Slot))
            _currentSlots.Add(slot);
    });
}
```
这样 `CurrentSlots` 的集合引用保持稳定（对 XAML 绑定更友好），且填充过程完全静默。

**为什么不用选择 A + suppressDirty**: 也可以，但替换集合引用意味着每次切换页面都要重新建立所有 PropertyChanged 订阅，性能稍差且代码更复杂。选择 B 更干净。

---

### 决策 3: `InitializeSlotMetadata` 内的 `slot.Action` 修改需静默

`InitializeSlotMetadata` 在 `OnCurrentContextChanged` 中被调用（通过 `RefreshSlotParameterMetadata`），其内部会修正空/无效的 `slot.Action`（line 1510）。这触发 `slot.PropertyChanged → OnSlotPropertyChanged → MarkDirty`。

**选择**: 将 `RefreshSlotParameterMetadata()` 的调用（在 `OnCurrentContextChanged` 中）包裹在 `WithSuppressedDirty` 内。InitializeSlotMetadata 本身不改，保持其通用性——它在 `CommitCreatedSlot` 中的调用仍然需要触发脏。

---

### 决策 4: `LoadSettings` 中 `GeneralSettings` 赋值需静默

`LoadSettings` 已有 `_suppressSlotSync = true`，但 `GeneralSettings = _config.Settings` 会触发 `OnGeneralSettingsPropertyChanged → MarkDirty`，绕过了 suppressSlotSync。

**选择**: 将整个 `LoadSettings` 方法体包裹在 `WithSuppressedDirtyAsync` 内（原有的 `_suppressSlotSync` 保留，两者并行工作）。

---

### 决策 5: `AddSlotDialog` 草稿隔离

`AddSlotDialog` 调用 `CreateSlotDraft`，草稿 slot 被 `AddSlotViewModel` 持有并订阅其 `PropertyChanged`。此时 slot 并未进入 `CurrentSlots`，不应触发 SettingsViewModel 的脏状态。

当前问题：`CreateSlotDraft` → `InitializeSlotMetadata` → `slot.Action = ...` → `slot.PropertyChanged` → `OnSlotPropertyChanged`（SettingsViewModel 中已订阅） → `MarkDirty`。

**根因澄清**: `OnSlotPropertyChanged` 只订阅了已加入 `CurrentSlots` 的 slot（在 `OnCurrentSlotsCollectionChanged` 中挂载）。草稿 slot 尚未进入集合，**理论上不会触发 SettingsViewModel 的 OnSlotPropertyChanged**。

需要确认的是：`CreateSlotDraft` 中 `InitializeSlotMetadata` 设置 `slot.Action` 时，该 slot 是否已被 `CurrentSlots` 订阅。根据代码流，答案是**否**——草稿在 `CommitCreatedSlot` 调用 `CurrentSlots.Add(slot)` 之前是孤立的。

**结论**: BUG #2（AddSlotDialog）的实际触发路径是 `CommitCreatedSlot` 中 `CurrentSlots.Add(slot)` 触发的 `CollectionChanged → MarkDirty`，这是**合法的**脏触发（用户确认了添加）。若用户取消对话框，不触发脏，行为正确。此路径无需修复，但需要通过测试验证确认。

## Risks / Trade-offs

| 风险 | 缓解措施 |
|------|----------|
| `_suppressDirty = true` 期间发生异常导致标志永远为 true，之后所有编辑都静默 | `WithSuppressedDirty` 使用 try/finally，保证异常时也能复位 |
| 嵌套调用 `WithSuppressedDirty`（suppressDirty 内部再次 suppressDirty）造成提前解除 | 改用计数器 `_suppressDirtyDepth` 替代布尔值，`depth > 0` 时静默 |
| 修改 `CurrentSlots` 集合填充方式破坏现有 XAML 绑定 | 保留集合引用，仅清空再填充，ItemsControl 的 CollectionChanged 绑定不受影响 |
| `RefreshSlotParameterMetadata` 在其他路径中调用时被意外静默 | `WithSuppressedDirty` 只在 `OnCurrentContextChanged` 中的调用点包裹，不修改方法本身 |

## Migration Plan

1. 改动完全向后兼容，无数据迁移需求
2. 修改顺序：先添加 `_suppressDirtyDepth` 机制 → 再包裹各触发路径 → 最后补充单元测试
3. 回滚策略：改动集中在 `SettingsViewModel.cs` 单文件，Git revert 即可完整回滚
4. 验收方式：打开设置窗口后不做任何操作，Save 按钮不应有红点；切换配置页，红点不出现；添加 Slot 完成后红点出现（正确）

## Open Questions

- `ProfileSettings` 中的普通属性（非 `[ObservableProperty]`）在 `GeneralSettings = _config.Settings` 赋值时是否也会触发 `PropertyChanged`？需要运行时确认（目前假设 `SetProperty` 在值相同时不触发）。
- 是否需要为 `_suppressDirtyDepth` 考虑线程安全（`Interlocked`）？当前 SettingsViewModel 均在 UI 线程操作，暂不需要。
