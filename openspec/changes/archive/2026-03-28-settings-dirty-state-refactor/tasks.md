## 1. 基础设施：_suppressDirty 保护机制

- [x] 1.1 在 `SettingsViewModel.cs` 中声明 `private bool _suppressDirty = false;` 字段
- [x] 1.2 修改 `MarkDirty()` 方法：在方法入口添加 `if (_suppressDirty) return;` 保护
- [x] 1.3 实现同步 helper：`private void WithSuppressedDirty(Action action)` — 设置标志、执行、finally 重置
- [x] 1.4 实现异步 helper：`private async Task WithSuppressedDirtyAsync(Func<Task> action)` — 设置标志、await、finally 重置

## 2. 修复路径 1：LoadSettings 期间抑制脏状态

- [x] 2.1 将 `LoadSettings()` 的整个 try 块内容包裹在 `WithSuppressedDirtyAsync` 中（替换现有的 `_suppressSlotSync = true/false` 手动模式，或在其基础上叠加）
- [x] 2.2 确认 `HasUnsavedChanges = false;`（line 439）仍在 suppressed 块内的末尾执行，保持加载后干净状态
- [ ] 2.3 验证：打开设置窗口后 Save 按钮红点不出现

## 3. 修复路径 2：OnCurrentContextChanged 中的 Slot 填充

- [x] 3.1 在 `OnCurrentContextChanged()` 中，将 `CurrentSlots = new ObservableCollection<>(sourceList)` 替换为：先解绑事件、Clear、逐项 Add（在 suppressDirty 内）、再重绑事件
- [x] 3.2 或者（更简洁）：将整个 `OnCurrentContextChanged` 方法体包裹在 `WithSuppressedDirty()` 内，包括 `RefreshSlotParameterMetadata()` 的调用
- [ ] 3.3 验证：在设置页面点击不同配置页（Profile/Global/Launcher），Save 按钮红点不出现

## 4. 修复路径 3：InitializeSlotMetadata 中的 slot.Action 赋值

- [x] 4.1 检查 `InitializeSlotMetadata()` 中 `slot.Action = actionMetadata.Name;`（line 1510）的赋值是否在所有调用路径上都被 suppressDirty 保护覆盖
- [x] 4.2 若存在从非 suppressed 路径调用 `InitializeSlotMetadata` 的情况（如 `CommitCreatedSlot` 之外），确认该赋值不应触发脏；若是用户提交后的调用，则不需要抑制
- [x] 4.3 为 `RefreshSlotParameterMetadata()`（在 `OnCurrentContextChanged` 中调用）确认其在 suppressDirty 范围内执行

## 5. 修复路径 4：GeneralSettings 赋值期间

- [x] 5.1 确认 `LoadSettings()` 中 `GeneralSettings = _config.Settings;` 的赋值在 suppressDirty 范围内
- [x] 5.2 验证 `OnGeneralSettingsPropertyChanged` 在加载期间不会调用 `MarkDirty()`
- [x] 5.3 验证用户在 UI 上修改 SlotsPerPage 等设置后，`MarkDirty()` 仍然正常触发

## 6. 修复路径 5：AddSlotDialog 草稿隔离

- [x] 6.1 检查 `CreateSlotDraft()` 和 `SetSlotDraftAction()` 是否在 SettingsViewModel 中直接触发 `OnSlotPropertyChanged`（草稿 slot 是否被加入 CurrentSlots 订阅）
- [x] 6.2 确认草稿 slot 在 `CommitCreatedSlot()` 调用前不挂载到 `CurrentSlots`，从而不触发 `OnCurrentSlotsCollectionChanged`
- [x] 6.3 若 `CreateSlotDraft` / `SetSlotDraftAction` 内部有路径触发 MarkDirty，将这两个方法的调用包裹在 suppressDirty 内
- [ ] 6.4 验证：打开 Add Slot 对话框后取消，Save 红点不出现

## 7. 单元测试

- [x] 7.1 在 `Pulsar.Tests/ViewModels/` 中创建或扩展 `SettingsViewModelDirtyStateTests.cs`
- [x] 7.2 编写测试：`LoadSettings_DoesNotSetDirty`——模拟加载后断言 `HasUnsavedChanges == false`
- [x] 7.3 编写测试：`SwitchContext_DoesNotSetDirty`——切换 CurrentContext 后断言脏状态不变
- [x] 7.4 编写测试：`CommitCreatedSlot_SetsDirty`——确认用户提交 slot 后脏状态为 true
- [x] 7.5 编写测试：`EditSlotLabel_SetsDirty`——直接修改 slot 属性后断言脏状态为 true
- [x] 7.6 运行 `dotnet test` 确认全部测试通过

## 8. 回归验证

- [x] 8.1 运行 `dotnet build Pulsar/Pulsar/Pulsar.csproj` 确认无编译错误
- [ ] 8.2 手动测试：打开设置窗口 → 不做任何操作 → 确认无红点
- [ ] 8.3 手动测试：切换多个配置页 → 确认无红点
- [ ] 8.4 手动测试：修改一个 slot 的 label → 确认红点出现 → Save → 红点消失
- [ ] 8.5 手动测试：打开 Add Slot 对话框 → 取消 → 确认无红点
- [ ] 8.6 手动测试：打开 Add Slot 对话框 → 确认添加 → 确认红点出现
- [ ] 8.7 手动测试：修改 SlotsPerPage → 确认红点出现
