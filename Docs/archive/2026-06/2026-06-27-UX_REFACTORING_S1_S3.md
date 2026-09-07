# UX 重构实施总结（S1-S3）

日期：2026-06-27
范围：以 UX 优化为目标的三阶段重构落地。

## S1 修复级

- 新增 `IWindowPlacementService` / `WindowPlacementService`：径向菜单与 NearMouse 对话框统一按监视器工作区钳制，物理像素/DIP 转换集中处理。
- `AnimationController.SyncCurrentLayout()`：直接布局更新后同步动画起点，修复首次退出子菜单从 0 开始的塌缩动画。
- 首次启动向导不再在构造函数中强制切换中文；按 配置语言 → 当前应用语言 → OS UI culture 推导默认语言。
- 热键调用快照：`HotkeyService.HotkeyInvoked` 上报触发组合，`RadialMenuViewModel` 仅在该组合的键释放时执行；支持 Win 修饰键。
- 菜单显示时不再吞掉菜单外鼠标事件，菜单外点击会关闭菜单并放行点击。
- 中心预览 Ellipse 的 Size 绑定改为显式 `CenterSlot.Size`。
- 本地化清理：对话框按钮、页面 Provider 文案、插件分组、HotkeyBox、文件对话框、日志统计等 50+ 资源键补齐 EN/zh-CN。

## S2 体验级

- `QuickSwitchPolicy`：快速切换时间窗/中心半径可配置，设置页新增 NumberBox（80-1500ms）。
- 子菜单分页：窗口数超过 slotsPerPage 时支持滚轮翻页、页码显示与边界反馈。
- 中心提示改为可取消的 transient hint（`_centerHintCts`），避免旧提示覆盖新提示。
- 设置页插件管理：状态筛选（All/Enabled/Disabled/Errors），Ctrl+F 聚焦搜索，Ctrl+R 刷新。
- `SlotWheelEditor`：Ctrl+←/→ 键盘重排、Enter 编辑、Delete 删除。
- 径向菜单视觉：标题/角标/背景 scrim token 化；`SlotOrb` 仅在激活时订阅渲染帧。

## S3 结构性

- `SettingsViewModel` 拆出 `SettingsViewModel.General.cs`（主题/热键/缓存），主文件聚焦槽位与密钥。
- `Themes/DialogTemplates.xaml` 统一 App 与 DialogHost 的 DataTemplate；对话框第三按钮改用 Pulsar 样式 + 主题色。
- 新增 `IActionFeedbackPresenter` / `ActionFeedbackPresenter`，插件反馈统一出口。
- `PluginResult.ErrorCode`：反馈层按错误码映射，不再依赖消息文本匹配（消息仍用于展示）。
- 主要插件（WinSwitcher/Command/Bookmarklet）错误路径补充 ErrorCode。

## 验证

- `dotnet build Pulsar/Pulsar/Pulsar.csproj`：0 warning / 0 error
- `dotnet test Pulsar/Pulsar.Tests/Pulsar.Tests.csproj`：367/367 passed（新增 6 个 UX 专项测试）
