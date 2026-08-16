# UX 重构实施总结（S4：交互契约与键盘可访问性）

日期：2026-08-16
范围：在 S1-S3 基础上，以“同一意图必须有一致行为”为核心原则的第四轮 UX 重构。

## 问题与决策

### 1. 径向菜单中心按钮行为矛盾（P0）
- 现状：根菜单中心 Slot 的标题/标签显示为 `Cancel/取消`，但左键点击只播放抖动动画，菜单不关闭；右键在根菜单同样只抖动。
- 决策：中心显示什么，就执行什么。根菜单左键点击中心 = 取消；右键 = 取消（与 Esc 等价）；子菜单中仍是“返回上层”。
- 变更：
  - `RadialMenuInputCoordinator.HandleGlobalMouseClickAsync` 删除无意义的 bounce 分支。
  - 移除已无调用方的 `OnRootBounceRequested` 事件与 `RadialMenuWindow.HandleRootBounceRequested`。

### 2. Esc 取消从未实现（P0）
- 现状：`FocusRestoreMode.RestorePrevious` 注释写明“按 Esc 或点击外部”，但径向菜单没有 Esc 处理。
- 决策：新增统一取消入口 `RadialMenuViewModel.CancelActiveMenu()`：
  - 根菜单：清空本次热键调用状态与 quick-switch 标记，恢复焦点并隐藏；
  - 子菜单：只返回根菜单，不误关整个会话。
- 双通道覆盖：
  - `RadialMenuWindow.PreviewKeyDown` 在窗口持有焦点时于 KeyDown 阶段取消；
  - `RadialMenuViewModel.HandleKeyUp` 在低层键盘钩子中作为失焦兜底。

### 3. 径向菜单键盘翻页（P1）
- `RadialMenuViewModel.HandlePagingKey(int direction)` 将 `←/→` 映射到既有滚轮翻页管线（复用 `HandleMouseWheel` 的页面导航、边界反馈与 transient hint）。
- `RadialMenuWindow.OnPreviewKeyDown` 处理 `Left/Right`，翻页成功时标记 `Handled`，避免把方向键漏给下层应用。

### 4. 对话框 Esc 取消（P1）
- `DialogHostViewModel.CancelFromKeyboard()`：
  - 普通对话框：以 `DialogResult.Cancelled` 关闭；
  - 向导模式：委托给 `IWizardDialogViewModel.SecondaryCommand`（当前向导均为 Cancel/Back 语义）。
- `DialogHostWindow` 使用 **bubbling `KeyDown`** 而非 `PreviewKeyDown`，确保 `HotkeyBox` 录制热键时按 Esc 仍优先执行“清除热键”，不会被窗口层抢走。

### 5. 设置页信息架构与本地化
- 常规页主题选择器不再硬编码 `Dark/Light`：
  - 使用既有 `Settings.General.Dark/Light` 本地化键；
  - 增加日/月图标，提高跨语言可读性；
  - 补充 `AutomationProperties.Name`。
- 渐进式披露：`Appearance` 与 `Global Hotkeys` 默认展开（核心高频设置），布局与缓存维护保持折叠。
- `SettingsPluginsPage.Title` 从硬编码字符串改为 `{lex:Locale Settings.Plugins.Title}`。

## 验证

- `dotnet build Pulsar/Pulsar/Pulsar.csproj`：0 warning / 0 error
- 新增/更新测试：
  - `DialogHostViewModelLocalizationTests`：Esc 普通关闭、Esc 委托向导 SecondaryCommand；
  - `GroupedSlotInteractionTests`：根菜单中心左键关闭、根菜单右键关闭。

## 后续修复

### SlotOrb 中心文本截断（2026-08-16）
- 现象：中心 Slot 的 `取消` 被显示为 `取..`。
- 根因：标题可读性优化给文本层加了固定 `MaxWidth=18 + CharacterEllipsis`，两个中文字符在 FontSize 11 下自然宽度约 22 设计单位，必然被截断；Viewbox 又使用 `DownOnly`，中心大 Orb 不会把标签放大。
- 修复：Viewbox 的 MaxWidth/MaxHeight 改为 `Size * 0.64` 并允许双向缩放；文本 MaxWidth 放宽到 40 设计单位。短中心标签（取消/Back/Cancel）完整显示，过长窗口/进程标题仍在缩放前省略。

## 后续候选（按优先级）

1. 径向菜单 `Enter` 执行当前悬停 Slot（需要先实现键盘焦点/高亮同步）。
2. 常规页 CardExpander 展开状态持久化到 `LocalUiPreferences.json`。
3. 插件页筛选快捷键 `Ctrl+E` 切换 Enabled/Errors 视图。
4. 教程卡片 `Step 1/9` 改为本地化 step format。
