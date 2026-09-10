// [Path]: Pulsar/Pulsar/Plugins/Core/WinSwitcher/WinSwitcherPlugin.cs

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
using Pulsar.Services.WindowSwitching;

namespace Pulsar.Plugins.Core.WinSwitcher
{
    /// <summary>
    /// 窗口切换插件 - 处理应用程序的智能切换和启动
    /// </summary>
    public partial class WinSwitcherPlugin : IPluginConfigurable, IPluginTiered, IPluginMetadataProvider, IPluginLifecycle
    {
        private const string LogPrefix = "[WinSwitcher]";
        
        // Initialized in Initialize() method with null check - guaranteed non-null after initialization
        private IWindowActivationService _windowActivationService = null!;
        private IProcessLauncher _processLauncher = null!;
        private IDiscoveryExclusionPolicy _exclusionPolicy = null!;
        private ILogger<WinSwitcherPlugin>? _logger;
        private ITrayService? _trayService;
        private ILocalizationService? _loc;

        public string Id => "com.pulsar.winswitcher";
        public string DisplayName => "App Switch";
        public string Version => "1.0.0";
        public string Author => "Pulsar Team";
        public string Description => "Switch to an existing app, launch one directly, or switch first and launch only when needed.";
        public string Icon => "\uE8A7"; // Open in new window icon
        public bool CanDisable => false; // Core plugin
        public PluginTier Tier => PluginTier.Core;
        
        // 新增元数据属性
        public IEnumerable<string> Tags => new[] { "Apps", "Window Management", "Core" };
        public string? DocumentationUrl => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Docs", "Plugins", "WinSwitcher.md");

        public void Initialize(IServiceProvider services)
        {
            _windowActivationService = (services.GetService(typeof(IWindowActivationService)) as IWindowActivationService)!;
            _logger = services.GetService(typeof(ILogger<WinSwitcherPlugin>)) as ILogger<WinSwitcherPlugin>;
            _trayService = services.GetService(typeof(ITrayService)) as ITrayService;
            _loc = services.GetService(typeof(ILocalizationService)) as ILocalizationService;

            if (_windowActivationService == null)
            {
                throw new InvalidOperationException("IWindowActivationService service is not available");
            }

            _processLauncher = (services.GetService(typeof(IProcessLauncher)) as IProcessLauncher)!;

            if (_processLauncher == null)
            {
                throw new InvalidOperationException("IProcessLauncher service is not available");
            }

            // [W2] 排除策略是独立于本插件生命周期的单一所有者：配置键解析/应用/持久化
            // 都在那里，插件只负责把内核激活时的 profile 配置转交过去。
            _exclusionPolicy = (services.GetService(typeof(IDiscoveryExclusionPolicy)) as IDiscoveryExclusionPolicy)!;

            if (_exclusionPolicy == null)
            {
                throw new InvalidOperationException("IDiscoveryExclusionPolicy service is not available");
            }

            _logger?.LogInformation($"{LogPrefix} Initialized successfully");
        }

        public IEnumerable<PluginSettingDefinition> GetSettingsDefinition()
        {
            yield return new PluginSettingDefinition
            {
                Key = "ExcludeProcesses",
                Label = "Discovery Blacklist",
                Type = PluginSettingType.String,
                DefaultValue = "",
                Description = "Comma-separated process names excluded from automatic window discovery. Explicit activate and switch actions still target those processes when selected directly.",
                MinLength = 0,
                MaxLength = 10000,
                Pattern = @"^[a-zA-Z0-9_,.\s\-]*$"
            };

            yield return new PluginSettingDefinition
            {
                Key = "EnableSwitchDiagnostics",
                Label = "Window Switch Diagnostics",
                Type = PluginSettingType.Boolean,
                DefaultValue = false,
                Description = "Record detailed window eligibility and activation information for troubleshooting."
            };

            yield return new PluginSettingDefinition
            {
                Key = "ExcludeRules",
                Label = "Exclusion Rules (JSON)",
                Type = PluginSettingType.String,
                DefaultValue = "",
                Description = "JSON array of window-identity exclusion/allow rules, e.g. [{\"Allow\":false,\"ProcessName\":\"chrome\",\"WindowClass\":\"Chrome_WidgetWin_1\",\"TitlePattern\":\"^Chrome Legacy Window$\"}]. Fields: Allow, ProcessName, WindowClass, TitlePattern (regex), RectState (OffScreen|ZeroSize). First match wins; Allow overrides earlier Exclude. Use the Window Inspector to generate rules.",
                MinLength = 0,
                MaxLength = 20000,
                Pattern = null
            };
        }

        public void UpdateSettings(Dictionary<string, object> settings)
        {
            // [W2] 本插件不再是排除策略的 relay：解析（逗号拆分 / JSON 规则 / 布尔开关）
            // 与运行时应用全部收拢到 Discovery Exclusion Policy（含启动引导与 Inspector 写入）。
            _exclusionPolicy.ApplyFromConfig(settings);

            _logger?.LogInformation(
                $"{LogPrefix} Settings applied via discovery exclusion policy. RuleCount={{RuleCount}}",
                _exclusionPolicy.Rules.Count);
        }

        public PluginConfigValidationResult ValidateSettings(Dictionary<string, object> settings)
        {
            var result = new PluginConfigValidationResult { IsValid = true };
            
            if (settings.TryGetValue("ExcludeProcesses", out var excludeObj) && excludeObj != null)
            {
                var excludeStr = excludeObj.ToString() ?? string.Empty;
                var processes = excludeStr.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                
                foreach (var process in processes)
                {
                    var trimmed = process.Trim();
                    
                    // 验证进程名不包含非法字符
                    if (trimmed.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                    {
                        result.IsValid = false;
                        result.Errors.Add(string.Format(
                            _loc?["Plugin.WinSwitcher.InvalidProcessName"] ?? "Invalid process name '{0}': contains illegal characters",
                            trimmed));
                    }
                    
                    // 警告：进程名不应包含 .exe 后缀（记录日志但不阻止）
                    if (trimmed.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    {
                        _logger?.LogWarning($"{LogPrefix} Process name '{{ProcessName}}' should not include .exe extension (will be auto-stripped)", trimmed);
                    }
                    
                    // 验证长度
                    if (trimmed.Length > 255)
                    {
                        result.IsValid = false;
                        result.Errors.Add(_loc?["Plugin.WinSwitcher.ProcessNameTooLong"] ?? "Process name exceeds maximum length (255 characters)");
                    }
                }
            }

            if (settings.TryGetValue("ExcludeRules", out var rulesObj) && rulesObj != null)
            {
                var rulesJson = rulesObj.ToString() ?? string.Empty;
                if (WindowEligibilityRuleSerializer.TryParse(rulesJson) == null)
                {
                    result.IsValid = false;
                    result.Errors.Add(_loc?["Plugin.WinSwitcher.InvalidExcludeRules"] ?? "ExcludeRules must be a valid JSON array of rule objects");
                }
            }

            return result;
        }

        public async Task OnEnableAsync()
        {
            _logger?.LogInformation($"{LogPrefix} Plugin enabled");

            // [W2] 无需重新同步：策略状态由 AppStartupCoordinator 在插件激活前引导、
            // 由内核 ApplyProfileAsync 的 UpdateSettings 调用应用，两者先于/伴随 OnEnable。
            await Task.CompletedTask;
        }

        public async Task OnDisableAsync()
        {
            _logger?.LogInformation($"{LogPrefix} Plugin disabled");

            // [W2] 有意不清理：排除策略独立于插件生命周期，用户的排除偏好（黑名单与规则）
            // 比插件活得更久。这也消除了旧实现"清黑名单但留规则"的不对称。
            await Task.CompletedTask;
        }
        
        public async Task OnUnloadAsync()
        {
            _logger?.LogInformation($"{LogPrefix} Plugin unloading - cleaning up resources");
            await OnDisableAsync();
        }

        public async Task<PluginResult> ExecuteAsync(
            string action,
            IReadOnlyDictionary<string, string> args,
            PulsarContext context,
            CancellationToken cancellationToken = default)
        {
            if (_windowActivationService == null)
            {
                return PluginResult.Error(_loc?["Plugin.WinSwitcher.WindowServiceNotInitialized"] ?? "WindowService not initialized", PluginErrorSeverity.Critical);
            }

            return action.ToLowerInvariant() switch
            {
                "activate" => await ActivateWindowAsync(args, context),
                "launch" => await LaunchApplicationAsync(args, context),
                "switch" => await SmartSwitchAsync(args, context), // 智能切换或启动
                _ => PluginResult.Error(string.Format(_loc?["Plugin.WinSwitcher.UnknownAction"] ?? "Unknown action: {0}. Supported: activate, launch, switch", action),
                    PluginErrorSeverity.Recoverable, PluginErrorCode.UnknownAction)
            };
        }

        /// <summary>
        /// 切换到指定进程的窗口
        /// </summary>
        private async Task<PluginResult> ActivateWindowAsync(
            IReadOnlyDictionary<string, string> args,
            PulsarContext context)
        {
            if (!args.TryGetValue("app", out var processName) || string.IsNullOrEmpty(processName))
            {
                return PluginResult.Error(_loc?["Plugin.WinSwitcher.MissingAppParam"] ?? "Missing required parameter: app", PluginErrorSeverity.Recoverable, PluginErrorCode.MissingRequiredParameter);
            }

            _logger?.LogDebug($"{LogPrefix} Attempting to activate: {{ProcessName}}", processName);

            bool switched = await _windowActivationService.SwitchToProcessAsync(processName);
            
            if (switched)
            {
                _logger?.LogInformation($"{LogPrefix} Successfully switched to: {{ProcessName}}", processName);
                return PluginResult.Ok(string.Format(_loc?["Plugin.WinSwitcher.SwitchedTo"] ?? "Switched to {0}", processName));
            }
            else
            {
                _logger?.LogInformation($"{LogPrefix} Process not running: {{ProcessName}}", processName);
                return PluginResult.Error(string.Format(_loc?["Plugin.WinSwitcher.ProcessNotRunning"] ?? "Process '{0}' is not running", processName),
                    PluginErrorSeverity.Recoverable, PluginErrorCode.NotFound);
            }
        }

        /// <summary>
        /// 启动应用程序
        /// </summary>
        private Task<PluginResult> LaunchApplicationAsync(
            IReadOnlyDictionary<string, string> args,
            PulsarContext context)
        {
            if (!args.TryGetValue("path", out var exePath) || string.IsNullOrEmpty(exePath))
            {
                return Task.FromResult(PluginResult.Error(_loc?["Plugin.WinSwitcher.MissingPathParam"] ?? "Missing required parameter: path", PluginErrorSeverity.Recoverable, PluginErrorCode.MissingRequiredParameter));
            }
            
            // 验证路径格式
            if (!Path.IsPathRooted(exePath))
            {
                return Task.FromResult(PluginResult.Error(
                    string.Format(_loc?["Plugin.WinSwitcher.PathNotAbsolute"] ?? "Path must be absolute: {0}", exePath),
                    PluginErrorSeverity.Recoverable, PluginErrorCode.InvalidConfiguration));
            }
            
            // 验证文件存在性
            if (!File.Exists(exePath))
            {
                return Task.FromResult(PluginResult.Error(
                    string.Format(_loc?["Plugin.WinSwitcher.AppNotFound"] ?? "Application not found: {0}", exePath),
                    PluginErrorSeverity.Recoverable, PluginErrorCode.NotFound));
            }
            
            // 验证文件扩展名白名单
            var allowedExtensions = new[] { ".exe", ".bat", ".cmd", ".lnk" };
            var ext = Path.GetExtension(exePath).ToLowerInvariant();
            if (!allowedExtensions.Contains(ext))
            {
                return Task.FromResult(PluginResult.Error(
                    string.Format(_loc?["Plugin.WinSwitcher.UnsupportedFileType"] ?? "Unsupported file type: {0}. Allowed: {1}", ext, string.Join(", ", allowedExtensions)),
                    PluginErrorSeverity.Recoverable, PluginErrorCode.InvalidConfiguration));
            }

            args.TryGetValue("arguments", out var arguments);

            _logger?.LogInformation($"{LogPrefix} Launching: {{ExePath}} {{Arguments}}", exePath, arguments ?? "");

            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = exePath,
                    Arguments = arguments ?? string.Empty,
                    UseShellExecute = true,
                    WindowStyle = ProcessWindowStyle.Normal
                };

                _processLauncher.Launch(startInfo);
                _logger?.LogInformation($"{LogPrefix} Successfully launched: {{ExePath}}", exePath);
                return Task.FromResult(PluginResult.Ok(string.Format(_loc?["Plugin.WinSwitcher.Launched"] ?? "Launched {0}", Path.GetFileName(exePath))));
            }
            catch (System.IO.FileNotFoundException ex)
            {
                _logger?.LogError(ex, $"{LogPrefix} File not found: {{ExePath}}", exePath);
                return Task.FromResult(PluginResult.Error(
                    string.Format(_loc?["Plugin.WinSwitcher.FileNotFound"] ?? "File not found: {0}", ex.Message),
                    PluginErrorSeverity.Recoverable, PluginErrorCode.NotFound));
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger?.LogError(ex, $"{LogPrefix} Access denied: {{ExePath}}", exePath);
                return Task.FromResult(PluginResult.Error(
                    string.Format(_loc?["Plugin.WinSwitcher.AccessDenied"] ?? "Access denied: {0}", ex.Message),
                    PluginErrorSeverity.Critical, PluginErrorCode.AccessDenied));
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                _logger?.LogError(ex, $"{LogPrefix} Win32 error launching: {{ExePath}}", exePath);
                return Task.FromResult(PluginResult.Error(
                    string.Format(_loc?["Plugin.WinSwitcher.LaunchFailed"] ?? "Launch failed: {0}", ex.Message),
                    PluginErrorSeverity.Recoverable, PluginErrorCode.ExecutionFailed));
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"{LogPrefix} Unexpected error launching: {{ExePath}}", exePath);
                return Task.FromResult(PluginResult.Error(
                    string.Format(_loc?["Plugin.WinSwitcher.LaunchFailed"] ?? "Launch failed: {0}", ex.Message),
                    PluginErrorSeverity.Critical, PluginErrorCode.ExecutionFailed));
            }
        }

        /// <summary>
        /// 智能切换：如果进程正在运行则切换，否则启动
        /// </summary>
        private async Task<PluginResult> SmartSwitchAsync(
            IReadOnlyDictionary<string, string> args,
            PulsarContext context)
        {
            if (!args.TryGetValue("app", out var processName) || string.IsNullOrEmpty(processName))
            {
                return PluginResult.Error(_loc?["Plugin.WinSwitcher.MissingAppParam"] ?? "Missing required parameter: app", PluginErrorSeverity.Recoverable, PluginErrorCode.MissingRequiredParameter);
            }

            _logger?.LogDebug($"{LogPrefix} Smart switch for: {{ProcessName}}", processName);

            // 1. 尝试切换
            bool switched = await _windowActivationService.SwitchToProcessAsync(processName);
            if (switched)
            {
                _logger?.LogInformation($"{LogPrefix} Switched to existing window: {{ProcessName}}", processName);
                return PluginResult.Ok(string.Format(_loc?["Plugin.WinSwitcher.SwitchedTo"] ?? "Switched to {0}", processName));
            }

            // 2. 切换失败，尝试启动
            if (args.TryGetValue("path", out var exePath) && !string.IsNullOrEmpty(exePath))
            {
                _trayService?.ShowNotification(
                    _loc?["Feedback.Launching"] ?? "Launching",
                    string.Format(_loc?["Feedback.StartingFormat"] ?? "Starting {0}...", processName),
                    PulsarNotificationIcon.Info);
                return await LaunchApplicationAsync(args, context);
            }
            else
            {
                _logger?.LogWarning($"{LogPrefix} Cannot launch: No path specified for {{ProcessName}}", processName);
                return PluginResult.Error(
                    string.Format(_loc?["Plugin.WinSwitcher.ProcessNotRunningNoPath"] ?? "Process '{0}' is not running and no launch path specified", processName),
                    PluginErrorSeverity.Recoverable, PluginErrorCode.NotFound);
            }
        }

    }
}
