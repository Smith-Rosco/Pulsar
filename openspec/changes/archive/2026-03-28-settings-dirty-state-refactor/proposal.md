## Why

`SettingsViewModel` 中的脏状态（`HasUnsavedChanges`）会被三类非用户编辑操作错误触发：切换配置页、打开「添加 Slot」对话框、以及加载初始化时的内部元数据刷新。结果是 Save 按钮在用户尚未做任何修改时就显示红点，误导用户并破坏信任感。

## What Changes

- 引入统一的 `_suppressDirty` 保护标志，覆盖所有静默加载/初始化路径
- 修复 `CurrentSlots` setter：改为先挂载事件、再填充集合，避免构造时触发 `CollectionChanged`
- 修复 `InitializeSlotMetadata` 在 `OnCurrentContextChanged` 中静默修改 `slot.Action` 时触发 `OnSlotPropertyChanged → MarkDirty` 的路径
- 修复 `GeneralSettings` 赋值时在 `_suppressSlotSync` 之内但仍可触发 `OnGeneralSettingsPropertyChanged → MarkDirty` 的路径
- 将 `_suppressSlotSync`（仅防止 SyncSlotsToConfig）和 `_suppressDirty`（防止 MarkDirty）两个职责明确分离
- `AddSlotDialog` 中的草稿 slot 生命周期完全隔离在 `AddSlotViewModel` 内，`CommitCreatedSlot` 才是唯一合法的脏触发入口

## Capabilities

### New Capabilities
- `settings-dirty-state-guard`: 统一的脏状态保护机制——`_suppressDirty` 标志 + `WithSuppressedDirty()` helper，确保所有静默路径不触发 MarkDirty

### Modified Capabilities
<!-- 无现有 spec 需要修改 -->

## Impact

- **主要文件**: `Pulsar/ViewModels/SettingsViewModel.cs`
- **模型文件**: `Pulsar/Models/ProfilesConfig.cs`（`PluginSlot` 可能需要 `SuppressNotifications` 辅助）
- **无 API/接口变更**：改动完全在 ViewModel 内部，不影响 `IConfigService`、`ISettingsViewModel` 等接口
- **无破坏性变更**：行为对用户的改变是「更少的误报」，不改变任何功能语义
- **测试影响**: `Pulsar.Tests/ViewModels/` 中应补充针对脏状态的单元测试
