## Context

Pulsar 目前有两条生成“默认配置”的路径，但它们语义不一致：首次启动通过 `ConfigService.LoadAsync()` 在文件不存在时调用 `CreateDefaultConfig()`，先返回 fallback 配置，再在后台执行应用检测并演进到 smart config；而设置页中的 `ResetConfig()` 则直接构造并保存 `new ProfilesConfig()`。这导致 reset 后缺少 `Global` profile、默认 slots、初始检测元数据以及与 tutorial 相关的首启状态，用户体验与“刚安装完成”的实际行为脱节。

这次变更跨越 `SettingsViewModel`、`ConfigService` 和应用启动阶段的 tutorial 判断，属于配置生命周期层面的统一，而不是单一界面修补。项目现有方向已经明确偏向“首启即用”体验，因此 reset 需要回到同一条产品语义，而不是保留 clean-slate 分支。

## Goals / Non-Goals

**Goals:**
- 让 reset 与 first launch 共享同一条默认配置生成语义
- 明确 reset 后 tutorial 不保留，系统重新回到 fresh install 状态
- 让 reset 后重新允许 fallback config、后台应用检测与 smart config 演进
- 避免 `SettingsViewModel` 自行维护另一套默认值构造逻辑
- 保持现有公开接口和用户数据备份行为尽量稳定

**Non-Goals:**
- 重写整个配置系统或更换 `Profiles.json` 结构
- 改变 fallback config 或 smart config 中具体默认 slot 内容
- 引入新的持久化格式、外部依赖或迁移框架
- 在本次设计中扩展 tutorial 内容本身，只统一其 reset 后的触发语义

## Decisions

### 决策 1: Reset 语义统一为“重新进入首启状态”

**选择**: `Reset Configuration` 不再表示“保存一个空的 `ProfilesConfig` 对象”，而是表示“回到配置文件不存在时的系统状态”，从而复用首启默认配置生成入口。

**为什么不是继续保存 `new ProfilesConfig()`**:
- 它只反映模型默认值，不反映产品默认体验
- 它绕开 `CreateDefaultConfig()`，导致 fallback config、`Global` profile 和 smart detection 不会发生
- 它让 reset 的结果与 UI 文案“恢复默认”相违背

**替代方案**:
- 方案 A：reset 直接保存 `CreateFallbackConfig()` 的结果。优点是稳定，缺点是仍然与真实首启最终行为不同，无法重新进入后台检测语义。
- 方案 B：首次启动也改成 `new ProfilesConfig()`。优点是彻底简化，缺点是明显损失首启即用体验，与当前产品方向冲突。

选择当前方案，是因为它最符合用户理解：reset = 像第一次安装一样。

### 决策 2: 默认配置生成入口集中在 `ConfigService`

**选择**: 由 `ConfigService` 作为唯一的“默认配置工厂”和“首启流程调度器”，UI 层只表达“请求重置”，不直接构造默认对象。

**原因**:
- `ConfigService` 已经掌握 `_configPath`、fallback config、后台检测、smart config、缓存状态等关键上下文
- 把默认值逻辑留在 `SettingsViewModel` 会继续制造语义分叉
- 统一入口后，未来若默认配置内容变化，只需维护一处

**替代方案**:
- 在 `SettingsViewModel` 中复制 `CreateDefaultConfig()` / `CreateFallbackConfig()` 逻辑。拒绝，因会重复规则且难以保持一致。
- 新建独立 `IDefaultConfigFactory`。理论上更纯，但当前变更规模下会增加抽象成本；若后续默认配置逻辑继续扩张，再考虑抽离。

### 决策 3: Reset 应恢复 tutorial 与 detection 的 fresh-state 元数据

**选择**: reset 后 tutorial 相关状态不保留；`HasCompletedTutorial` 应为 `false`，`LastTutorialStep` 应为 `null`，并回到允许 initial detection 的状态。

**原因**:
- 用户已经明确 reset 后不保留 tutorial
- `App.xaml.cs` 当前通过 `HasCompletedTutorial` 判定是否触发 tutorial，必须恢复到 fresh-state 才能得到与首启一致的行为
- 若保留旧元数据，即使默认配置内容重建了，用户仍不会得到真正的 first-run 体验

**替代方案**:
- 保留 tutorial 完成状态，只重建 slots。拒绝，因为这会把 reset 变成“半首启”，语义不完整。
- 保留 `LastTutorialStep` 以便恢复未完成引导。拒绝，因为 reset 是重新开始，不是继续上一次。

### 决策 4: Reset 采用“移除当前配置并重新加载”的状态迁移，而不是“覆盖写入另一份默认配置”

**选择**: reset 更接近一个状态切换：备份当前 `Profiles.json` -> 清除当前配置文件/缓存 -> 重新通过 `LoadAsync()` 进入文件不存在路径。

**原因**:
- 首启路径是“文件不存在”触发的，最稳的统一方式就是回到这个前提
- 这样才能自然触发 `CreateDefaultConfig()` 和其内部的后台检测调度
- 它避免必须手动复制一整套“首启后续副作用”到 reset 流程里

**替代方案**:
- 先手动创建 fallback config，再显式调用检测。可行，但更像复制流程，不如复用既有状态机清晰。

### 决策 5: Reset 后当前会话应尽量立即看到 fallback 状态，再异步演进到 smart config

**选择**: 统一后的 reset 不应要求用户手动重启应用才能看到默认内容；它应在当前会话中重新加载出 fallback config，并允许后台检测继续把配置演进到 smart config。

**原因**:
- 与首次启动的交互节奏一致：先可用，再增强
- 保持设置页里的“Reset Complete”通知真实有效，而不是只完成了一半工作
- 降低用户对“为什么重置后还要重启”的困惑

**权衡**:
- reset 后配置可能在短时间内再次变化（fallback -> smart config），这与首启一致，但会带来异步观察窗口；测试需要覆盖该演进过程

## Risks / Trade-offs

- [Reset 删除/重建配置后，内存缓存仍保留旧 `_cachedConfig`] -> 在 reset 流程中必须显式清除缓存或通过统一 API 执行“忘记当前配置”
- [后台 detection 在 reset 触发后覆盖用户刚恢复出来的状态] -> 仅在真正 fresh-state 下调度 detection，并复用现有“文件存在则中止/覆盖受控”的保护逻辑
- [Tutorial 在 reset 后于当前会话立即触发，可能与设置窗口当前上下文冲突] -> 需要定义 reset 完成后的 UI 刷新顺序，避免 tutorial 在旧状态尚未清理时启动
- [将 reset 从“清空配置”改成“恢复首启体验”会改变部分高级用户预期] -> 更新确认文案，明确会恢复默认 profiles、重新引导，并保留备份文件以支持回退
- [测试不稳定：smart detection 异步执行依赖环境差异] -> 测试分层验证：同步部分验证 fallback / tutorial 元数据；异步部分通过可控桩或边界断言验证 detection 被重新允许

## Migration Plan

1. 在 `ConfigService` 中收敛“重置到首启状态”的入口，负责清理缓存并重新走无配置文件路径
2. 修改 `SettingsViewModel.ResetConfig()`，保留备份行为，但将“生成默认配置”的职责移交给 `ConfigService`
3. 校准 reset 完成后的 UI 重新加载顺序，确保设置页、热键、tutorial 判定看到的是新状态
4. 增加单元测试与必要的集成测试，验证 reset 与 first launch 在关键状态上等价
5. 手动验证：首次启动、执行 reset、等待 detection、tutorial 重启、配置文件落盘内容一致性
6. 回滚策略：若行为异常，可回退到原 reset 实现；由于备份文件仍保留，用户配置可人工恢复

## Open Questions

- reset 完成后，tutorial 是否应立即在当前会话启动，还是等用户下次打开主交互入口时再触发？当前首启是在 `App.xaml.cs` 启动阶段判断，reset 发生在设置页内，可能需要额外协调
- 是否需要显式暴露一个 `ResetToFirstLaunchAsync()` 风格的接口给 `IConfigService`，还是通过删除文件 + `LoadAsync()` 间接实现即可？前者更清晰，后者改动更小
- smart detection 在 reset 后是否应完全复用现有异步实现，还是需要增加一个“重置来源”的日志与遥测标记以便排查行为差异？
