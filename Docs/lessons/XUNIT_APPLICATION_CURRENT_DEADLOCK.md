# xUnit 全量套件死锁：WPF `Application.Current` 进程全局状态污染

> 2026-09-04 · 候选 K 测试落地时发现。症状罕见（定向跑全绿、全量必挂、零输出），根因机制对**所有在单元测试中触碰 WPF 全局单例的代码**通用。

## 症状

- `dotnet test Pulsar.Tests`（全量）挂死：testhost 启动后**零输出**，跑 15+ 分钟无进展（无论 xunit 默认并行还是 `MaxParallelThreads=1` 串行）。
- 定向跑（`--filter FullyQualifiedName~AppStartupCoordinatorTests`）**19/19 秒过**。
- 排除嫌疑测试类后全量 **24 秒跑完**（1037/1037）。

## 根因机制（三步链条）

1. **污染源**：`ThemeServiceTests` / `SettingsSaveSessionTests` / `SettingsViewModelDirtyStateTests` / `BookmarkletScriptEditorContentSmokeTests` 等测试执行：
   ```csharp
   if (Application.Current == null)
   {
       _ = new Application();   // ← 之后 Application.Current 在整个进程里永远非 null
   }
   ```
   `Application.Current` 是 **AppDomain 全局静态**，xunit 的 collection 隔离**不隔离它**，测试进程存活期内一直残留。

2. **隐藏依赖**：被测生产代码（`AppStartupCoordinator.StartDeferredInitialization` 的 tutorial 分支）内联访问 `Application.Current.Dispatcher`。新测试 #7 依赖「单元测试宿主中 `Application.Current == null` → 抛 NRE → 被生产 try/catch 吞掉并 LogError」作为可观测信号。

3. **死锁**：全量跑时污染已发生 → `Application.Current` 非 null → 不再 NRE，而是 `Dispatcher.InvokeAsync(...)` 把回调**排进一个永不泵消息的 Dispatcher**（该 Application 在普通线程上构造，其 STA 线程没有 Run 消息循环）→ `await` 永不完成 → 测试永不结束 → xunit 挂死。

**为什么定向跑通过**：单独跑时没有前置测试污染，`Application.Current == null` 成立，NRE 路径如期触发。

## 修复（确定性 seam，而非全局状态假设）

生产代码 ctor 新增可选 seam（符合 ADR-017 hybrid injection 风格）：

```csharp
Func<System.Windows.Threading.Dispatcher> dispatcherProvider = null
// 赋值：
_dispatcherProvider = dispatcherProvider ?? (() => System.Windows.Application.Current?.Dispatcher);
// tutorial 分支：
var uiDispatcher = _dispatcherProvider();
await await uiDispatcher.InvokeAsync(...);
```

- **生产语义等价**：默认 fallback 与原内联访问是同一个对象；无 WPF Application 的宿主中两者都以 NRE 告终并被同一 catch 记录。
- **DI 无需改动**：`AddSingleton<IAppStartupCoordinator, AppStartupCoordinator>()` 类型注册下，MS.DI 对带默认值的可选参数自动采用默认值（容器无 `Func<Dispatcher>` 注册时回落 `null` → fallback 生效）。
- **测试注入 `() => null`**：断言不再依赖进程全局状态，任何运行组合下行为一致。

## 可复用规则

1. **单元测试永远不要对 `Application.Current` / `Dispatcher.CurrentDispatcher` / `SynchronizationContext.Current` 等进程全局单例的状态做任何假设**——包括「它应该是 null」这种看似安全的假设。任何兄弟测试都可能污染它。
2. **生产代码触碰 WPF 全局单例时，提供 ctor seam**（`Func<T>` 工厂），默认实现包住全局访问。这既是可测性修复，也符合依赖显式化的方向（ADR-017 精神）。
3. **「定向跑通过 + 全量挂死 + 零输出」的诊断套路**：① 先跑排除实验（`--filter FullyQualifiedName!~嫌疑类`）定位嫌疑；② detailed logger（`--logger "console;verbosity=detailed"`）+ 串行让卡点暴露在日志尾部；③ 对卡点前后的测试做全局状态审查（grep `new Application` / 静态字段）。
4. Moq 的 last-write-wins、表达式树 CS0854 等常规坑见 `Pulsar.Tests/Services/AppStartupCoordinatorTests.cs` 内注释。

## 关联

- 测试：`Pulsar.Tests/Services/AppStartupCoordinatorTests.cs`（#7 的注释记录了本次教训）
- 生产 seam：`Pulsar/Services/AppStartupCoordinator.cs`（`_dispatcherProvider`）
- 候选 K / ADR-017 / ADR-018：`Docs/decisions/017-*`、`Docs/decisions/018-*`
