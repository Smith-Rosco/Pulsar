// [Path]: Pulsar/Pulsar/Plugins/Core/WinSwitcher/WinSwitcherPlugin.cs

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Pulsar.Core.Plugin;
using Pulsar.Core.Plugin.Metadata;
using Pulsar.Services.Interfaces;

namespace Pulsar.Plugins.Core.WinSwitcher
{
    /// <summary>
    /// 窗口切换插件 - 处理应用程序的智能切换和启动
    /// </summary>
    public class WinSwitcherPlugin : IPluginConfigurable, IPluginTiered, IPluginMetadataProvider, IPluginLifecycle
    {
        private const string LogPrefix = "[WinSwitcher]";
        
        // Initialized in Initialize() method with null check - guaranteed non-null after initialization
        private IWindowService _windowService = null!;
        private ILogger<WinSwitcherPlugin>? _logger;
        private HashSet<string> _excludedProcesses = new();

        public string Id => "com.pulsar.winswitcher";
        public string DisplayName => "App Switcher";
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
            _windowService = (services.GetService(typeof(IWindowService)) as IWindowService)!;
            _logger = services.GetService(typeof(ILogger<WinSwitcherPlugin>)) as ILogger<WinSwitcherPlugin>;

            if (_windowService == null)
            {
                throw new InvalidOperationException("IWindowService service is not available");
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
        }

        public void UpdateSettings(Dictionary<string, object> settings)
        {
            if (settings.TryGetValue("ExcludeProcesses", out var excludeObj) && excludeObj != null)
            {
                var excludeStr = excludeObj.ToString() ?? string.Empty;
                _excludedProcesses = excludeStr.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                                               .Select(p => p.Trim())
                                               .ToHashSet(StringComparer.OrdinalIgnoreCase);
                
                // [New] Update WindowService blacklist
                _windowService?.UpdateBlacklist(_excludedProcesses);
            }

            _logger?.LogInformation(
                $"{LogPrefix} Settings updated. ExcludedCount={{ExcludedCount}}",
                _excludedProcesses.Count);
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
                        result.Errors.Add($"Invalid process name '{trimmed}': contains illegal characters");
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
                        result.Errors.Add($"Process name '{trimmed}' exceeds maximum length (255 characters)");
                    }
                }
            }
            
            return result;
        }

        public async Task OnEnableAsync()
        {
            _logger?.LogInformation($"{LogPrefix} Plugin enabled");
            
            // 重新同步黑名单到 WindowService
            if (_excludedProcesses.Count > 0)
            {
                _windowService?.UpdateBlacklist(_excludedProcesses);
                _logger?.LogDebug($"{LogPrefix} Blacklist synchronized: {{Count}} entries", _excludedProcesses.Count);
            }
            
            await Task.CompletedTask;
        }
        
        public async Task OnDisableAsync()
        {
            _logger?.LogInformation($"{LogPrefix} Plugin disabled");
            
            // 清空黑名单（恢复默认系统黑名单）
            _windowService?.UpdateBlacklist(Enumerable.Empty<string>());
            
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
            PulsarContext context)
        {
            if (_windowService == null)
            {
                return PluginResult.Error("WindowService not initialized", PluginErrorSeverity.Critical);
            }

            return action.ToLowerInvariant() switch
            {
                "activate" => await ActivateWindowAsync(args, context),
                "launch" => await LaunchApplicationAsync(args, context),
                "switch" => await SmartSwitchAsync(args, context), // 智能切换或启动
                _ => PluginResult.Error($"Unknown action: {action}. Supported: activate, launch, switch", 
                    PluginErrorSeverity.Recoverable)
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
                return PluginResult.Error("Missing required parameter: app", PluginErrorSeverity.Recoverable);
            }

            _logger?.LogDebug($"{LogPrefix} Attempting to activate: {{ProcessName}}", processName);

            bool switched = await _windowService.SwitchToProcessAsync(processName);
            
            if (switched)
            {
                _logger?.LogInformation($"{LogPrefix} Successfully switched to: {{ProcessName}}", processName);
                return PluginResult.Ok($"Switched to {processName}");
            }
            else
            {
                _logger?.LogInformation($"{LogPrefix} Process not running: {{ProcessName}}", processName);
                return PluginResult.Error($"Process '{processName}' is not running", 
                    PluginErrorSeverity.Recoverable);
            }
        }

        /// <summary>
        /// 启动应用程序
        /// </summary>
        private async Task<PluginResult> LaunchApplicationAsync(
            IReadOnlyDictionary<string, string> args,
            PulsarContext context)
        {
            if (!args.TryGetValue("path", out var exePath) || string.IsNullOrEmpty(exePath))
            {
                return PluginResult.Error("Missing required parameter: path", PluginErrorSeverity.Recoverable);
            }
            
            // 验证路径格式
            if (!Path.IsPathRooted(exePath))
            {
                return PluginResult.Error($"Path must be absolute: {exePath}", PluginErrorSeverity.Recoverable);
            }
            
            // 验证文件存在性
            if (!File.Exists(exePath))
            {
                return PluginResult.Error($"Application not found: {exePath}", PluginErrorSeverity.Recoverable);
            }
            
            // 验证文件扩展名白名单
            var allowedExtensions = new[] { ".exe", ".bat", ".cmd", ".lnk" };
            var ext = Path.GetExtension(exePath).ToLowerInvariant();
            if (!allowedExtensions.Contains(ext))
            {
                return PluginResult.Error($"Unsupported file type: {ext}. Allowed: {string.Join(", ", allowedExtensions)}", 
                    PluginErrorSeverity.Recoverable);
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

                Process.Start(startInfo);
                _logger?.LogInformation($"{LogPrefix} Successfully launched: {{ExePath}}", exePath);
                return PluginResult.Ok($"Launched {Path.GetFileName(exePath)}");
            }
            catch (System.IO.FileNotFoundException ex)
            {
                _logger?.LogError(ex, $"{LogPrefix} File not found: {{ExePath}}", exePath);
                return PluginResult.Error($"File not found: {ex.Message}", PluginErrorSeverity.Recoverable);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger?.LogError(ex, $"{LogPrefix} Access denied: {{ExePath}}", exePath);
                return PluginResult.Error($"Access denied: {ex.Message}", PluginErrorSeverity.Critical);
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                _logger?.LogError(ex, $"{LogPrefix} Win32 error launching: {{ExePath}}", exePath);
                return PluginResult.Error($"Failed to launch: {ex.Message}", PluginErrorSeverity.Recoverable);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"{LogPrefix} Unexpected error launching: {{ExePath}}", exePath);
                return PluginResult.Error($"Launch failed: {ex.Message}", PluginErrorSeverity.Critical);
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
                return PluginResult.Error("Missing required parameter: app", PluginErrorSeverity.Recoverable);
            }

            _logger?.LogDebug($"{LogPrefix} Smart switch for: {{ProcessName}}", processName);

            // 1. 尝试切换
            bool switched = await _windowService.SwitchToProcessAsync(processName);
            if (switched)
            {
                _logger?.LogInformation($"{LogPrefix} Switched to existing window: {{ProcessName}}", processName);
                return PluginResult.Ok($"Switched to {processName}");
            }

            // 2. 切换失败，尝试启动
            if (args.TryGetValue("path", out var exePath) && !string.IsNullOrEmpty(exePath))
            {
                // 调用 LaunchApplicationAsync (已包含完整验证和错误处理)
                return await LaunchApplicationAsync(args, context);
            }
            else
            {
                _logger?.LogWarning($"{LogPrefix} Cannot launch: No path specified for {{ProcessName}}", processName);
                return PluginResult.Error($"Process '{processName}' is not running and no launch path specified", 
                    PluginErrorSeverity.Recoverable);
            }
        }

        /// <summary>
        /// 获取插件元数据
        /// </summary>
        public PluginMetadata GetMetadata()
        {
            return new PluginMetadata
            {
                Id = Id,
                Display = new DisplayInfo
                {
                    Name = DisplayName,
                    Description = Description,
                    IconKey = Icon,
                    Category = "Apps",
                    Version = Version,
                    Author = Author,
                    DocumentationUrl = DocumentationUrl,
                    License = "MIT"
                },
                Schema = new ConfigSchema
                {
                    Version = 1,
                    Properties = new Dictionary<string, PropertySchema>
                    {
                        ["ExcludeProcesses"] = new PropertySchema
                        {
                            Type = "multiselect",
                            Description = "Process names excluded from discovery lists only; direct activate and switch actions still target them.",
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
                    CanDisable = false,
                    Tier = PluginTier.Core,
                    MinPulsarVersion = "1.0.0"
                },
                Actions = new Dictionary<string, SlotActionMetadata>(StringComparer.OrdinalIgnoreCase)
                {
                    ["switch"] = new SlotActionMetadata
                    {
                        Name = "switch",
                        Label = "Switch Or Launch",
                        Description = "Switch to a running app window, or launch it when no matching window is found.",
                        Parameters = new List<SlotParameterMetadata>
                        {
                            new()
                            {
                                Key = "app",
                                Type = "string",
                                Label = "Process Name",
                                Description = "Executable process name used to find a running window.",
                                IsRequired = true,
                                Group = SlotParameterGroup.Required,
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
                            },
                            new()
                            {
                                Key = "path",
                                Type = "string",
                                Label = "Launch Path",
                                Description = "Optional fallback executable path used when the app is not already running.",
                                IsRequired = false,
                                Group = SlotParameterGroup.Optional,
                                SummaryLabel = "Launch",
                                SummaryMode = SlotParameterSummaryMode.SafeStateOnly,
                                ConfiguredSummaryText = "fallback ready",
                                MissingSummaryText = "switch only",
                                PresentationHint = SlotParameterPresentationHint.DialogOnly,
                                Placeholder = "C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe",
                                Example = "C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe",
                                InputHint = "Use an absolute path for reliable launching.",
                                ValidationHint = "Add a fallback executable only if this slot should launch when no window is found.",
                                PickerIntent = SlotPickerIntent.Process
                            },
                            new()
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
                                Placeholder = "--profile-directory=Default",
                                Example = "--new-window https://example.com",
                                InputHint = "Applied only when a new process is launched.",
                                ValidationHint = "Applied only when a new process is launched."
                            }
                        }
                    },
                    ["launch"] = new SlotActionMetadata
                    {
                        Name = "launch",
                        Label = "Launch App",
                        Description = "Always launch an app using an explicit executable path.",
                        Parameters = new List<SlotParameterMetadata>
                        {
                            new()
                            {
                                Key = "path",
                                Type = "string",
                                Label = "Executable Path",
                                Description = "Absolute path to the application to launch.",
                                IsRequired = true,
                                Group = SlotParameterGroup.Required,
                                SummaryLabel = "App",
                                SummaryMode = SlotParameterSummaryMode.SafeStateOnly,
                                ConfiguredSummaryText = "path ready",
                                MissingSummaryText = "path missing",
                                PresentationHint = SlotParameterPresentationHint.QuickEdit,
                                QuickEditPriority = 100,
                                Placeholder = "C:\\Windows\\System32\\notepad.exe",
                                Example = "C:\\Windows\\System32\\notepad.exe",
                                InputHint = "Use a full path to an executable, shortcut, or script.",
                                ValidationHint = "Pick an executable, shortcut, or script to launch.",
                                PickerIntent = SlotPickerIntent.Process,
                                Validators = new List<ValidationRule> { new RequiredValidator() }
                            },
                            new()
                            {
                                Key = "arguments",
                                Type = "string",
                                Label = "Launch Arguments",
                                Description = "Optional command-line arguments passed to the target application.",
                                IsRequired = false,
                                Group = SlotParameterGroup.Optional,
                                SummaryLabel = "Args",
                                SummaryMode = SlotParameterSummaryMode.SafeStateOnly,
                                ConfiguredSummaryText = "args set",
                                MissingSummaryText = "no args",
                                PresentationHint = SlotParameterPresentationHint.DialogOnly,
                                Placeholder = "--incognito",
                                Example = "--new-window https://example.com"
                            }
                        }
                    },
                    ["activate"] = new SlotActionMetadata
                    {
                        Name = "activate",
                        Label = "Switch Existing App",
                        Description = "Switch to an already running app window without launching a new instance.",
                        Parameters = new List<SlotParameterMetadata>
                        {
                            new()
                            {
                                Key = "app",
                                Type = "string",
                                Label = "Process Name",
                                Description = "Executable process name used to find the target window.",
                                IsRequired = true,
                                Group = SlotParameterGroup.Required,
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
                            }
                        }
                    }
                }
            };
        }
    }
}
