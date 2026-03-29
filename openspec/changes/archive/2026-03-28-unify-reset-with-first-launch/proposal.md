## Why

`Reset Configuration` 目前直接保存 `new ProfilesConfig()`，而首次启动走的是 `ConfigService.CreateDefaultConfig()` 的 fallback + smart detection 流程，导致两条路径生成的默认配置内容和后续行为不一致。用户点击“恢复默认”时，预期应回到“刚安装、第一次启动时”的体验，而不是得到一份更空的 clean-slate 配置。

## What Changes

- 将“重置配置”重新定义为“回到首次启动状态”，而不是“清空为模型默认值”
- 统一 reset 与 first launch 的默认配置生成入口，避免 `SettingsViewModel` 私自构造另一套默认配置
- 明确 reset 后 tutorial 不保留，系统应像全新安装一样重新进入首启引导语义
- 允许 reset 后重新执行首次启动的默认配置增强流程，包括 fallback 配置、后台应用检测和 smart config 演进
- 调整相关 UI 文案与验证逻辑，使“默认值”始终指向同一套产品语义

## Capabilities

### New Capabilities
- `configuration-reset-defaults`: 定义默认配置生成、首次启动初始化、以及重置为默认时的统一行为契约

### Modified Capabilities
<!-- 无现有 spec 需要修改 -->

## Impact

- **主要文件**: `Pulsar/Pulsar/ViewModels/SettingsViewModel.cs`, `Pulsar/Pulsar/Services/ConfigService.cs`, `Pulsar/Pulsar/App.xaml.cs`
- **模型与状态**: `Pulsar/Pulsar/Models/ProfilesConfig.cs` 中的 tutorial / detection 元数据将参与统一语义
- **用户体验**: Reset 后的结果将从“清空配置”变为“恢复首次安装体验”，包括默认 profiles、slots、tutorial 与初始检测状态
- **测试影响**: 需要补充 `ConfigService`、`SettingsViewModel` 和首次启动/reset 流程相关测试，验证 reset 与 first launch 行为一致
- **无外部依赖变更**: 不引入新依赖，不改变公开 API；改动集中在配置生命周期与应用内状态流转
