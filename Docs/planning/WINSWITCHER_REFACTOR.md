# WinSwitcher 插件重构方案

## 实施状态

> 更新于 2026-08-26。以下部分已落地，其余仍待实施。

**已完成**
- 反馈按 `PluginErrorCode` 匹配：删除 `CreateWinSwitcherFailure` 字符串匹配（第四节）；插件所有错误路径均携带 `PluginErrorCode`。
- 错误消息本地化：补全 `Plugin.WinSwitcher.*` 资源键（第五节）。
- 启动逻辑统一：删除 `LaunchApplicationStrategy`；Switch Mode 中"已配置但未运行"的 Slot 改走 `PluginActionStrategy` → 插件动作。
- 调试残留清理：移除 `SmartSwitchAsync` 与 `PluginActionStrategy` 中的 `Debug.WriteLine`。
- 额外深化（稳定性/性能）：窗口枚举单次化（`WindowInventoryService.GetProcessWindowsAsync(processName)`，O(P×W)→O(W)）；WinEvent 事件管道（`WindowEventFeed`，回调只入队、后台消费 + 采样日志）；策略层失败反馈统一走 `ActionFeedbackService`。

**窗口切换领域重构（Window Eligibility，2026-08-26 落地）**

针对"快速切换落到不可见窗口"（KxWppQuickHelpBarContainer → Chrome Legacy Window）的四层闭环，全部落地：

1. **统一判定模块（候选 1）**：`IWindowEligibilityPolicy` / `WindowEligibilityPolicy` / `WindowEligibilitySnapshot`。
   原 `IsAltTabWindow` 与 `WindowInventoryService` 两处枚举的同一套 6 项判定，收敛为单一 policy 的
   `Evaluate(snapshot, processBlacklist?)`。新增**物理可见性规则**（矩形在虚拟屏幕内且非零面积，最小化豁免）——
   这是对"WS_VISIBLE 但屏幕外/零尺寸"幽灵的通用判定，取代逐个硬编码类名的补丁模式。
   `HWND→Snapshot` 是唯一 native seam（`FromHwnd`）；进程黑名单保持调用方作用域（发现/快速切换传入，
   显式激活传 null，文档语义不变）。删除了 `GetProcessActivationBlacklistPredicate` 死桩。
2. **用户排除规则（候选 2）**：`WindowEligibilityRule`（WindowClass 精确 / TitlePattern 正则 / ProcessName 限定），
   存储于 WinSwitcher `ExcludeRules` JSON 设置，`UpdateSettings` 解析后经 `WindowService.UpdateEligibilityRules`
   原子替换 policy 规则链。`ExcludeProcesses` 保留向后兼容。
3. **Window Inspector（候选 3）**：`GetWindowEligibilityReportAsync`（全桌面窗口 + 每窗口判定原因）+ `FlashWindow`；
   WinSwitcher 设置对话框新增入口；每行"闪烁定位"+"一键排除"（生成最具体的进程+类+标题正则规则，运行时立即生效 + 经 `ConfigEditSession` 持久化）。
4. **激活后校验 + MRU 剔除（候选 4）**：`ActivateWindowDetailedAsync` 激活成功后先复核物理可见性；
   不可见 → 逐出 MRU（`QuickSwitchEngine.RemoveFromHistory`）、永不入历史、托盘提示指向 Inspector。

**实现期设计决策（已定稿）**
- **RectState 规则维度已删除**：屏幕外/零尺寸已被物理硬规则先行排除，规则再表达完全冗余。
- **Allow 是绝对放行**：覆盖之前任何 Exclude；Exclude 为暂定排除，可被之后任何 Allow 救回；都不能覆盖硬规则。
- **标题规则代价控制**：仅当存在 TitlePattern 规则（`HasTitleDependentRules`）时才在热路径读取标题，
  无标题规则时零额外阻塞读取（`FromHwnd(captureTitle:)` + 发现路径二次判定）。
- **物理可见性规则对全部消费面生效**（含显式激活）：其语义是"这个窗口永远不是合法目标"，不违背
  "显式激活仍可达黑名单进程"的文档承诺。

**待实施**
- 继承 `PluginBase<T>` + 构造函数注入（第一、二节）：`WinSwitcherPlugin` 仍为手动 4 接口 + Service Locator。
- 提取 `WinSwitcherPluginMetadata` 工厂：`GetMetadata` 仍内联。
- `WindowSwitchStrategy` / `ProcessGroupStrategy` 完全走插件管道：因插件缺少 HWND 级激活动作，逐窗口切换保留为薄适配器（直调共享激活路径），已统一反馈路径。

---

## 现状概览

当前 WinSwitcher 插件是 Pulsar 最早的插件之一，采用旧式架构（手动实现 4 个接口、Service Locator 反模式），存在三层重复逻辑、错误消息未本地化、调试残留等问题。

## 重构目标

1. 继承 `PluginBase<T>`，使用构造函数注入，与 `CommandPlugin` 保持一致
2. 提取 `WinSwitcherPluginMetadata` 静态工厂类
3. 所有错误消息本地化，统一使用 `PluginErrorCode`
4. 策略层（`SlotStrategies`）通过插件管道执行，不再绕过插件
5. 清理调试残留代码

---

## 一、重构后的插件主体

```csharp
// Pulsar/Pulsar/Plugins/Core/WinSwitcher/WinSwitcherPlugin.cs

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Pulsar.Core.Localization;
using Pulsar.Core.Plugin;
using Pulsar.Core.Plugin.Metadata;
using Pulsar.Models;
using Pulsar.Services.Interfaces;

namespace Pulsar.Plugins.Core.WinSwitcher
{
    public class WinSwitcherPlugin : PluginBase<WinSwitcherPlugin>,
        IPluginMetadataProvider, IPluginConfigurable
    {
        private static readonly HashSet<string> AllowedExtensions = new(
            new[] { ".exe", ".bat", ".cmd", ".lnk" },
            StringComparer.OrdinalIgnoreCase);

        private readonly IWindowService _windowService;
        private readonly ITrayService _trayService;
        private readonly ILocalizationService _loc;
        private HashSet<string> _excludedProcesses = new(StringComparer.OrdinalIgnoreCase);

        // ---- 构造函数注入 ----
        public WinSwitcherPlugin(
            ILogger<WinSwitcherPlugin> logger,
            IWindowService windowService,
            ITrayService trayService,
            ILocalizationService loc)
            : base(logger)
        {
            _windowService = windowService ?? throw new ArgumentNullException(nameof(windowService));
            _trayService = trayService ?? throw new ArgumentNullException(nameof(trayService));
            _loc = loc ?? throw new ArgumentNullException(nameof(loc));
        }

        // ---- 插件元数据 ----
        public override string Id => "com.pulsar.winswitcher";
        public override string DisplayName => "App Switcher";
        public override string Version => "1.0.0";
        public override string Author => "Pulsar Team";
        public override string Description =>
            "Switch to an existing app, launch one directly, or switch first and launch only when needed.";
        public override string Icon => "\uE8A7";
        public override bool CanDisable => false;
        public override PluginTier Tier => PluginTier.Core;
        public override IEnumerable<string> Tags => new[] { "Apps", "Window Management", "Core" };
        public override string? DocumentationUrl =>
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Docs", "Plugins", "WinSwitcher.md");

        // ---- 配置定义 ----
        public IEnumerable<PluginSettingDefinition> GetSettingsDefinition()
        {
            yield return new PluginSettingDefinition
            {
                Key = "ExcludeProcesses",
                Label = "Discovery Blacklist",
                Type = PluginSettingType.String,
                DefaultValue = "",
                Description = "Comma-separated process names excluded from automatic window discovery. "
                    + "Explicit activate and switch actions still target those processes when selected directly.",
                MinLength = 0,
                MaxLength = 10000,
                Pattern = @"^[a-zA-Z0-9_,.\s\-]*$"
            };
        }

        public void UpdateSettings(Dictionary<string, object> settings)
        {
            if (settings.TryGetValue("ExcludeProcesses", out var excludeObj) && excludeObj != null)
            {
                var excludeStr = excludeObj.ToString() ?? string.Empty;
                _excludedProcesses = excludeStr
                    .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(p => p.Trim())
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                _windowService.UpdateBlacklist(_excludedProcesses);
            }

            Logger.LogInformation(
                "[WinSwitcher] Settings updated. ExcludedCount={ExcludedCount}",
                _excludedProcesses.Count);
        }

        public PluginConfigValidationResult ValidateSettings(Dictionary<string, object> settings)
        {
            var result = new PluginConfigValidationResult { IsValid = true };

            if (settings.TryGetValue("ExcludeProcesses", out var excludeObj) && excludeObj != null)
            {
                foreach (var process in excludeObj.ToString()!.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    var trimmed = process.Trim();

                    if (trimmed.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                    {
                        result.IsValid = false;
                        result.Errors.Add(string.Format(
                            _loc["Plugin.WinSwitcher.InvalidProcessName"], trimmed));
                    }

                    if (trimmed.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    {
                        Logger.LogWarning(
                            "[WinSwitcher] Process name '{ProcessName}' should not include .exe extension",
                            trimmed);
                    }

                    if (trimmed.Length > 255)
                    {
                        result.IsValid = false;
                        result.Errors.Add(_loc["Plugin.WinSwitcher.ProcessNameTooLong"]);
                    }
                }
            }

            return result;
        }

        // ---- 生命周期 ----
        protected override void OnInitialize(IServiceProvider services)
        {
            if (_excludedProcesses.Count > 0)
            {
                _windowService.UpdateBlacklist(_excludedProcesses);
                Logger.LogDebug(
                    "[WinSwitcher] Blacklist synchronized: {Count} entries",
                    _excludedProcesses.Count);
            }
        }

        // ---- 执行入口 ----
        public override async Task<PluginResult> ExecuteAsync(
            string action,
            IReadOnlyDictionary<string, string> args,
            PulsarContext context,
            CancellationToken cancellationToken = default)
        {
            return action.ToLowerInvariant() switch
            {
                "activate" => await ActivateWindowAsync(args),
                "launch"   => await LaunchApplicationAsync(args),
                "switch"   => await SmartSwitchAsync(args),
                _          => UnknownActionError(action, "activate", "launch", "switch")
            };
        }

        // ---- 动作：切换窗口 ----
        private async Task<PluginResult> ActivateWindowAsync(
            IReadOnlyDictionary<string, string> args)
        {
            if (!TryGetRequiredArg(args, "app", out var processName))
                return MissingParameterError("app", PluginErrorCode.MissingRequiredParameter);

            Logger.LogDebug("[WinSwitcher] Activating: {ProcessName}", processName);

            bool switched = await _windowService.SwitchToProcessAsync(processName);

            if (switched)
            {
                Logger.LogInformation("[WinSwitcher] Switched to: {ProcessName}", processName);
                return PluginResult.Ok(
                    string.Format(_loc["Plugin.WinSwitcher.SwitchedTo"], processName));
            }

            Logger.LogInformation("[WinSwitcher] Process not running: {ProcessName}", processName);
            return PluginResult.Error(
                string.Format(_loc["Plugin.WinSwitcher.ProcessNotRunning"], processName),
                PluginErrorSeverity.Recoverable,
                PluginErrorCode.NotFound);
        }

        // ---- 动作：启动应用 ----
        private async Task<PluginResult> LaunchApplicationAsync(
            IReadOnlyDictionary<string, string> args)
        {
            if (!TryGetRequiredArg(args, "path", out var exePath))
                return MissingParameterError("path", PluginErrorCode.MissingRequiredParameter);

            if (!Path.IsPathRooted(exePath))
            {
                return PluginResult.Error(
                    string.Format(_loc["Plugin.WinSwitcher.PathNotAbsolute"], exePath),
                    PluginErrorSeverity.Recoverable,
                    PluginErrorCode.InvalidConfiguration);
            }

            if (!File.Exists(exePath))
            {
                return PluginResult.Error(
                    string.Format(_loc["Plugin.WinSwitcher.AppNotFound"], exePath),
                    PluginErrorSeverity.Recoverable,
                    PluginErrorCode.NotFound);
            }

            var ext = Path.GetExtension(exePath);
            if (!AllowedExtensions.Contains(ext))
            {
                return PluginResult.Error(
                    string.Format(_loc["Plugin.WinSwitcher.UnsupportedFileType"], ext),
                    PluginErrorSeverity.Recoverable,
                    PluginErrorCode.InvalidConfiguration);
            }

            args.TryGetValue("arguments", out var arguments);

            Logger.LogInformation("[WinSwitcher] Launching: {ExePath} {Arguments}",
                exePath, arguments ?? "");

            try
            {
                await Task.Run(() =>
                {
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = exePath,
                        Arguments = arguments ?? string.Empty,
                        UseShellExecute = true,
                        WindowStyle = ProcessWindowStyle.Normal
                    };
                    Process.Start(startInfo);
                });

                Logger.LogInformation("[WinSwitcher] Launched: {ExePath}", exePath);
                return PluginResult.Ok(
                    string.Format(_loc["Plugin.WinSwitcher.Launched"], Path.GetFileName(exePath)));
            }
            catch (FileNotFoundException ex)
            {
                Logger.LogError(ex, "[WinSwitcher] File not found: {ExePath}", exePath);
                return PluginResult.Error(
                    _loc["Plugin.WinSwitcher.FileNotFound"],
                    PluginErrorSeverity.Recoverable,
                    PluginErrorCode.NotFound);
            }
            catch (UnauthorizedAccessException ex)
            {
                Logger.LogError(ex, "[WinSwitcher] Access denied: {ExePath}", exePath);
                return PluginResult.Error(
                    _loc["Plugin.WinSwitcher.AccessDenied"],
                    PluginErrorSeverity.Critical,
                    PluginErrorCode.AccessDenied);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "[WinSwitcher] Launch failed: {ExePath}", exePath);
                return PluginResult.Error(
                    string.Format(_loc["Plugin.WinSwitcher.LaunchFailed"], ex.Message),
                    PluginErrorSeverity.Critical,
                    PluginErrorCode.ExecutionFailed);
            }
        }

        // ---- 动作：智能切换（先切换，失败则启动） ----
        private async Task<PluginResult> SmartSwitchAsync(
            IReadOnlyDictionary<string, string> args)
        {
            if (!TryGetRequiredArg(args, "app", out var processName))
                return MissingParameterError("app", PluginErrorCode.MissingRequiredParameter);

            Logger.LogDebug("[WinSwitcher] Smart switch: {ProcessName}", processName);

            // 1. 尝试切换
            bool switched = await _windowService.SwitchToProcessAsync(processName);
            if (switched)
            {
                Logger.LogInformation("[WinSwitcher] Switched to existing: {ProcessName}", processName);
                return PluginResult.Ok(
                    string.Format(_loc["Plugin.WinSwitcher.SwitchedTo"], processName));
            }

            // 2. 切换失败，尝试启动
            if (args.TryGetValue("path", out var exePath) && !string.IsNullOrEmpty(exePath))
            {
                _trayService.ShowNotification(
                    _loc["Feedback.Launching"],
                    string.Format(_loc["Feedback.StartingFormat"], processName),
                    PulsarNotificationIcon.Info);

                return await LaunchApplicationAsync(args);
            }

            Logger.LogWarning("[WinSwitcher] No launch path for: {ProcessName}", processName);
            return PluginResult.Error(
                string.Format(_loc["Plugin.WinSwitcher.ProcessNotRunningNoPath"], processName),
                PluginErrorSeverity.Recoverable,
                PluginErrorCode.NotFound);
        }

        // ---- 元数据工厂 ----
        public PluginMetadata GetMetadata() => WinSwitcherPluginMetadata.Create(this);
    }
}
```

---

## 二、提取的元数据工厂

```csharp
// Pulsar/Pulsar/Plugins/Core/WinSwitcher/WinSwitcherPluginMetadata.cs

using System;
using System.Collections.Generic;
using Pulsar.Core.Plugin;
using Pulsar.Core.Plugin.Metadata;

namespace Pulsar.Plugins.Core.WinSwitcher
{
    public static class WinSwitcherPluginMetadata
    {
        public static PluginMetadata Create(WinSwitcherPlugin plugin)
        {
            return new PluginMetadata
            {
                Id = plugin.Id,
                Display = new DisplayInfo
                {
                    Name = plugin.DisplayName,
                    Description = plugin.Description,
                    IconKey = plugin.Icon,
                    Category = "Apps",
                    Version = plugin.Version,
                    Author = plugin.Author,
                    DocumentationUrl = plugin.DocumentationUrl,
                    License = plugin.License,
                    IsPrimary = true
                },
                Schema = new ConfigSchema
                {
                    Version = 1,
                    Properties = new Dictionary<string, PropertySchema>
                    {
                        ["ExcludeProcesses"] = new PropertySchema
                        {
                            Type = "multiselect",
                            Description = "Process names excluded from discovery lists only.",
                            DefaultValue = "",
                            Placeholder = "Select processes to exclude..."
                        }
                    },
                    RequiredProperties = Array.Empty<string>()
                },
                UI = new UIHints
                {
                    Badge = "App",
                    AccentColor = "#2196F3",
                    ShowInQuickAccess = true,
                    SortOrder = 5,
                    IsFeatured = true
                },
                Capabilities = new PluginCapabilities
                {
                    SupportedActions = new List<string> { "switch", "launch", "activate" },
                    RequiresForegroundWindow = false,
                    Dependencies = new List<string>(),
                    CanDisable = plugin.CanDisable,
                    Tier = plugin.Tier,
                    MinPulsarVersion = plugin.MinPulsarVersion
                },
                Actions = BuildActions()
            };
        }

        private static Dictionary<string, SlotActionMetadata> BuildActions()
        {
            return new Dictionary<string, SlotActionMetadata>(StringComparer.OrdinalIgnoreCase)
            {
                ["switch"] = new SlotActionMetadata
                {
                    Name = "switch",
                    Label = "Switch Or Launch",
                    Description = "Switch to a running app window, or launch it when no matching window is found.",
                    SuggestedLabelTemplate = "Switch to {app}",
                    SuggestedIconKey = "E8AB",
                    SuggestedColorHex = "#2196F3",
                    Parameters = new List<SlotParameterMetadata>
                    {
                        BuildProcessNameParam(isRequired: true),
                        BuildLaunchPathParam(isRequired: false),
                        BuildArgumentsParam()
                    }
                },
                ["launch"] = new SlotActionMetadata
                {
                    Name = "launch",
                    Label = "Launch App",
                    Description = "Always launch an app using an explicit executable path.",
                    SuggestedLabelTemplate = "Launch {path}",
                    SuggestedIconKey = "E8AB",
                    SuggestedColorHex = "#2196F3",
                    Parameters = new List<SlotParameterMetadata>
                    {
                        BuildExecutablePathParam(),
                        BuildArgumentsParam()
                    }
                },
                ["activate"] = new SlotActionMetadata
                {
                    Name = "activate",
                    Label = "Switch Existing App",
                    Description = "Switch to an already running app window without launching a new instance.",
                    SuggestedLabelTemplate = "Switch to {app}",
                    SuggestedIconKey = "E8AB",
                    SuggestedColorHex = "#2196F3",
                    Parameters = new List<SlotParameterMetadata>
                    {
                        BuildProcessNameParam(isRequired: true)
                    }
                }
            };
        }

        private static SlotParameterMetadata BuildProcessNameParam(bool isRequired)
        {
            return new SlotParameterMetadata
            {
                Key = "app",
                Type = "string",
                Label = "Process Name",
                Description = "Executable process name used to find a running window.",
                IsRequired = isRequired,
                Group = isRequired ? SlotParameterGroup.Required : SlotParameterGroup.Optional,
                SummaryLabel = "App",
                SummaryMode = SlotParameterSummaryMode.RawValue,
                ConfiguredSummaryText = "app selected",
                MissingSummaryText = "app missing",
                PresentationHint = SlotParameterPresentationHint.QuickEdit,
                QuickEditPriority = 100,
                Placeholder = "chrome",
                Example = "chrome",
                InputHint = "Use the process name without .exe.",
                ValidationHint = "Pick the running app by process name, without .exe.",
                PickerIntent = SlotPickerIntent.Process,
                Validators = new List<ValidationRule> { new RequiredValidator() }
            };
        }

        private static SlotParameterMetadata BuildLaunchPathParam(bool isRequired = true)
        {
            return new SlotParameterMetadata
            {
                Key = "path",
                Type = "string",
                Label = isRequired ? "Executable Path" : "Launch Path",
                Description = isRequired
                    ? "Absolute path to the application to launch."
                    : "Optional fallback executable path used when the app is not already running.",
                IsRequired = isRequired,
                Group = isRequired ? SlotParameterGroup.Required : SlotParameterGroup.Optional,
                SummaryLabel = isRequired ? "App" : "Launch",
                SummaryMode = SlotParameterSummaryMode.SafeStateOnly,
                ConfiguredSummaryText = isRequired ? "path ready" : "fallback ready",
                MissingSummaryText = isRequired ? "path missing" : "switch only",
                PresentationHint = isRequired
                    ? SlotParameterPresentationHint.QuickEdit
                    : SlotParameterPresentationHint.DialogOnly,
                QuickEditPriority = 100,
                Placeholder = isRequired
                    ? "C:\\Windows\\System32\\notepad.exe"
                    : "C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe",
                Example = isRequired
                    ? "C:\\Windows\\System32\\notepad.exe"
                    : "C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe",
                InputHint = isRequired
                    ? "Use a full path to an executable, shortcut, or script."
                    : "Use an absolute path for reliable launching.",
                ValidationHint = isRequired
                    ? "Pick an executable, shortcut, or script to launch."
                    : "Add a fallback executable only if this slot should launch when no window is found.",
                PickerIntent = SlotPickerIntent.Process,
                Validators = isRequired
                    ? new List<ValidationRule> { new RequiredValidator() }
                    : null
            };
        }

        private static SlotParameterMetadata BuildArgumentsParam()
        {
            return new SlotParameterMetadata
            {
                Key = "arguments",
                Type = "string",
                Label = "Launch Arguments",
                Description = "Optional command-line arguments passed when launching the app.",
                IsRequired = false,
                Group = SlotParameterGroup.Advanced,
                SummaryLabel = "Args",
                SummaryMode = SlotParameterSummaryMode.SafeStateOnly,
                ConfiguredSummaryText = "args set",
                MissingSummaryText = "no args",
                PresentationHint = SlotParameterPresentationHint.DialogOnly,
                Placeholder = "--incognito",
                Example = "--new-window https://example.com",
                InputHint = "Applied only when a new process is launched.",
                ValidationHint = "Applied only when a new process is launched."
            };
        }
    }
}
```

---

## 三、策略层改造

### 现状问题

`WindowSwitchStrategy` 和 `LaunchApplicationStrategy` 绕过插件管道，直接调用 `IWindowService` / `Process.Start`，导致：

- 无错误码 → 无结构化反馈
- 无熔断保护
- 无执行上下文追踪
- 三套重复的启动逻辑

### 重构方案

删除 `WindowSwitchStrategy` 和 `LaunchApplicationStrategy`，统一走 `PluginActionStrategy`：

```csharp
// 改造前：径向菜单构建策略时
if (runningWindows.Any())
{
    slot.Strategy = new WindowSwitchStrategy(window, windowService, ...);
}
else if (hasLaunchPath)
{
    slot.Strategy = new LaunchApplicationStrategy(config, trayService, loc);
}

// 改造后：统一走插件管道
slot.Strategy = new PluginActionStrategy(
    new PluginSlot
    {
        PluginId = "com.pulsar.winswitcher",
        Action   = runningWindows.Any() ? "switch" : "launch",
        Args     = new Dictionary<string, string>
        {
            ["app"]       = processName,
            ["path"]      = launchPath,
            ["arguments"] = launchArgs
        }
    },
    pluginRegistry,
    pulsarContext,
    trayService,
    feedbackService,
    usageTracker,
    feedbackPresenter
);
```

`PluginActionStrategy` 已存在且无需修改——它通过 `IPluginRegistry.ExecuteAsync` 调用插件，自动获得：
- 熔断保护（Core 插件熔断由 `CorePluginFailureHandler` 处理）
- 结构化反馈（`ActionFeedbackService.Create`）
- 执行上下文追踪（`PluginExecutionContext`）

---

## 四、ActionFeedbackService 改造

### 现状问题

`ActionFeedbackService` 通过硬编码插件 ID 和英文字符串匹配来分发反馈：

```csharp
// 现状：脆弱的硬编码
if (string.Equals(pluginId, "com.pulsar.winswitcher", ...))
    return CreateWinSwitcherFailure(result.Message);

// CreateWinSwitcherFailure 内部
if (ContainsAny(message, "Missing required parameter", "Path must be absolute", ...))
```

### 重构方案

**依赖 `PluginErrorCode`，不再依赖字符串匹配**：

```csharp
// 改造后：匹配枚举，不匹配字符串
private ActionFeedback? CreateFromErrorCode(string pluginId, PluginErrorCode errorCode)
{
    return errorCode switch
    {
        PluginErrorCode.MissingRequiredParameter
        or PluginErrorCode.InvalidConfiguration
        or PluginErrorCode.UnknownAction
        or PluginErrorCode.UnsafePath
            => ConfigurationErrorFeedback(),

        PluginErrorCode.NotFound
            => NotFoundFeedback(),

        PluginErrorCode.AccessDenied
        or PluginErrorCode.ExecutionFailed
            => ExecutionFailedFeedback(),

        PluginErrorCode.TemporaryUnavailable
            => TemporaryUnavailableFeedback(),

        _ => null
    };
}
```

**前提条件**：插件在返回 `PluginResult.Error` 时必须携带 `PluginErrorCode`。重构后的 WinSwitcher 已经满足这一点。

之后 `CreateWinSwitcherFailure` / `CreateCommandFailure` / `CreatePkiFailure` / `CreateBookmarkletFailure` 四个方法均可删除，`Create` 方法中的 `if (pluginId == ...)` 链也全部移除。

---

## 五、需要新增的本地化资源

```
<!-- Resources/Strings.resx 新增条目 -->
<data name="Plugin.WinSwitcher.PathNotAbsolute" xml:space="preserve">
    <value>Path must be absolute: {0}</value>
</data>
<data name="Plugin.WinSwitcher.AppNotFound" xml:space="preserve">
    <value>Application not found: {0}</value>
</data>
<data name="Plugin.WinSwitcher.UnsupportedFileType" xml:space="preserve">
    <value>Unsupported file type: {0}. Allowed: .exe, .bat, .cmd, .lnk</value>
</data>
<data name="Plugin.WinSwitcher.FileNotFound" xml:space="preserve">
    <value>File not found</value>
</data>
<data name="Plugin.WinSwitcher.AccessDenied" xml:space="preserve">
    <value>Access denied</value>
</data>
<data name="Plugin.WinSwitcher.LaunchFailed" xml:space="preserve">
    <value>Launch failed: {0}</value>
</data>
<data name="Plugin.WinSwitcher.ProcessNotRunningNoPath" xml:space="preserve">
    <value>Process '{0}' is not running and no launch path specified</value>
</data>
<data name="Plugin.WinSwitcher.InvalidProcessName" xml:space="preserve">
    <value>Invalid process name '{0}': contains illegal characters</value>
</data>
<data name="Plugin.WinSwitcher.ProcessNameTooLong" xml:space="preserve">
    <value>Process name exceeds maximum length (255 characters)</value>
</data>
```

---

## 六、重构前后对比

| 维度 | 重构前 | 重构后 |
|------|--------|--------|
| 基类 | 手动实现 4 个接口 | 继承 `PluginBase<T>` |
| 依赖注入 | Service Locator | 构造函数注入 |
| 元数据 | 200 行内联方法 | 独立 `WinSwitcherPluginMetadata` 工厂 |
| 启动逻辑 | 3 处独立实现 | 1 处（`LaunchApplicationAsync`） |
| 策略层 | 绕过插件直接操作 | 统一走 `PluginActionStrategy` |
| 错误消息 | 硬编码英文 | 全部本地化 |
| 错误码 | 部分缺失 | 全部携带 `PluginErrorCode` |
| 反馈匹配 | 字符串匹配 | 枚举匹配 |
| 调试残留 | 3 处 `Debug.WriteLine` | 全部移除 |
| 代码行数 | ~533 行 | ~250 行（插件）+ ~180 行（元数据） |

---

## 七、迁移步骤

1. **新建** `WinSwitcherPluginMetadata.cs`，提取元数据工厂
2. **重写** `WinSwitcherPlugin.cs`，继承 `PluginBase<T>`，使用构造函数注入
3. **新增** 本地化资源条目到 `Strings.resx` 和 `Strings.zh-CN.resx`
4. **改造** `SlotStrategies.cs`：删除 `WindowSwitchStrategy` 和 `LaunchApplicationStrategy`，改为构建 `PluginSlot` 后走 `PluginActionStrategy`
5. **改造** `ActionFeedbackService.cs`：删除 `CreateWinSwitcherFailure` 及 `Create` 中的插件 ID 硬编码分支，改为纯 `PluginErrorCode` 匹配
6. **更新** DI 注册（`App.xaml.cs`），改为构造函数注入
7. **运行** `dotnet build && dotnet test` 验证