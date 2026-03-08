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
        public string DisplayName => "Window Switcher";
        public string Version => "1.0.0";
        public string Author => "Pulsar Team";
        public string Description => "Switch to running windows or launch new application instances.";
        public string Icon => "\uE8B8"; // Window/Switch Icon
        public bool CanDisable => false; // Core plugin
        public PluginTier Tier => PluginTier.Core;
        
        // 新增元数据属性
        public IEnumerable<string> Tags => new[] { "Window Management", "Core", "Productivity" };
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
            yield return PluginSettingDefinition.Create(
                key: "ExcludeProcesses",
                label: "Blacklist (Exclude from Window List)",
                type: PluginSettingType.String,
                defaultValue: "",
                description: "Comma-separated list of process names to exclude from automatic window discovery (e.g., in Switch Mode). Does not affect explicit switching via Profiles.json."
            );
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
                    IconKey = "🪟", // Window emoji
                    Category = "Productivity",
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
                            Type = "string",
                            Description = "Comma-separated list of process names to exclude from automatic window discovery",
                            DefaultValue = "",
                            Placeholder = "e.g., notepad,calc"
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
                }
            };
        }
    }
}
