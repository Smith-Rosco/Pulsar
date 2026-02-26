// [Path]: Pulsar/Pulsar/Plugins/WinSwitcher/WinSwitcherPlugin.cs

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Pulsar.Core.Plugin;
using Pulsar.Services.Interfaces;

namespace Pulsar.Plugins.WinSwitcher
{
    /// <summary>
    /// 窗口切换插件 - 处理应用程序的智能切换和启动
    /// </summary>
    public class WinSwitcherPlugin : IPluginConfigurable
    {
        private IWindowService? _windowService;
        private bool _showPreviews = true;
        private HashSet<string> _excludedProcesses = new();

        public string Id => "com.pulsar.winswitcher";
        public string DisplayName => "Window Switcher";
        public string Version => "1.0.0";
        public string Author => "Pulsar Team";
        public string Description => "Switch to running windows or launch new application instances.";
        public string Icon => "\uE8B8"; // Window/Switch Icon
        public bool CanDisable => false; // Core Plugin

        public void Initialize(IServiceProvider services)
        {
            _windowService = services.GetService(typeof(IWindowService)) as IWindowService;

            if (_windowService == null)
            {
                throw new InvalidOperationException("IWindowService service is not available");
            }

            Debug.WriteLine("[WinSwitcherPlugin] Initialized successfully");
        }

        public IEnumerable<PluginSettingDefinition> GetSettingsDefinition()
        {
            yield return PluginSettingDefinition.Create(
                key: "ShowPreviews",
                label: "Show Previews",
                type: PluginSettingType.Boolean,
                defaultValue: true,
                description: "Show window preview thumbnails in the switcher."
            );

            yield return PluginSettingDefinition.Create(
                key: "ExcludeProcesses",
                label: "Exclude Processes",
                type: PluginSettingType.String,
                defaultValue: "",
                description: "Comma-separated list of process names to ignore."
            );
        }

        public void UpdateSettings(Dictionary<string, object> settings)
        {
            if (settings.TryGetValue("ShowPreviews", out var showPreviewsObj))
            {
                if (showPreviewsObj is bool bVal) _showPreviews = bVal;
                else if (bool.TryParse(showPreviewsObj?.ToString(), out var bParsed)) _showPreviews = bParsed;
            }

            if (settings.TryGetValue("ExcludeProcesses", out var excludeObj) && excludeObj != null)
            {
                var excludeStr = excludeObj.ToString() ?? string.Empty;
                _excludedProcesses = excludeStr.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                                               .Select(p => p.Trim())
                                               .ToHashSet(StringComparer.OrdinalIgnoreCase);
            }

            Debug.WriteLine($"[WinSwitcherPlugin] Settings updated. ShowPreviews={_showPreviews}, Excluded count={_excludedProcesses.Count}");
        }

        public async Task<PluginResult> ExecuteAsync(
            string action,
            IReadOnlyDictionary<string, string> args,
            PulsarContext context)
        {
            if (_windowService == null)
            {
                return PluginResult.Error("Plugin not initialized");
            }

            return action.ToLowerInvariant() switch
            {
                "activate" => await ActivateWindowAsync(args, context),
                "launch" => await LaunchApplicationAsync(args, context),
                "switch" => await SmartSwitchAsync(args, context), // 智能切换或启动
                _ => PluginResult.Error($"Unknown action: {action}")
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
                return PluginResult.Error("Missing required parameter: app");
            }

            if (_excludedProcesses.Contains(processName))
            {
                Debug.WriteLine($"[WinSwitcherPlugin] 🛑 Process excluded by settings: {processName}");
                return PluginResult.Error($"Process is excluded by settings: {processName}");
            }

            if (_windowService == null)
            {
                return PluginResult.Error("Service not available");
            }

            Debug.WriteLine($"[WinSwitcherPlugin] Attempting to activate: {processName}");

            bool switched = await _windowService.SwitchToProcessAsync(processName);
            
            if (switched)
            {
                Debug.WriteLine($"[WinSwitcherPlugin] ✓ Successfully switched to: {processName}");
                return PluginResult.Ok($"Switched to {processName}");
            }
            else
            {
                Debug.WriteLine($"[WinSwitcherPlugin] ❌ Process not running: {processName}");
                return PluginResult.Error($"Process not running: {processName}");
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
                return PluginResult.Error("Missing required parameter: path");
            }
            // Launch is explicitly requested, so we might not check excluded processes here, 
            // but for SmartSwitch we should.
            // However, if the user explicitly wants to launch an app, maybe we shouldn't block it even if it's in the "Exclude from Switcher" list.
            // "ExcludeProcesses" usually means "Exclude from the list of switchable windows".
            // So Launch should probably be allowed.

            args.TryGetValue("arguments", out var arguments);

            Debug.WriteLine($"[WinSwitcherPlugin] Launching: {exePath}");

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
                Debug.WriteLine($"[WinSwitcherPlugin] ✓ Successfully launched: {exePath}");
                return PluginResult.Ok($"Launched {exePath}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WinSwitcherPlugin] ❌ Launch failed: {ex.Message}");
                return PluginResult.Error($"Launch failed: {ex.Message}");
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
                return PluginResult.Error("Missing required parameter: app");
            }

            if (_excludedProcesses.Contains(processName))
            {
                 Debug.WriteLine($"[WinSwitcherPlugin] 🛑 Process excluded by settings (SmartSwitch): {processName}");
                 // If excluded, we probably shouldn't switch to it.
                 // Should we fall back to Launch?
                 // If it's excluded from "Switching", maybe we treat it as "Not Running" for the purpose of switching?
                 // But if the setting is "Exclude from Switcher", it implies we shouldn't switch to it via the switcher.
                 // However, if the user invokes "SmartSwitch" explicitly for this app, they probably want to go there.
                 // But let's respect the setting as a "Block" for now to demonstrate the feature.
                 return PluginResult.Error($"Process is excluded by settings: {processName}");
            }

            if (_windowService == null)
            {
                return PluginResult.Error("Service not available");
            }

            Debug.WriteLine($"[WinSwitcherPlugin] Smart switch for: {processName}");

            // 1. 尝试切换
            bool switched = await _windowService.SwitchToProcessAsync(processName);
            if (switched)
            {
                Debug.WriteLine($"[WinSwitcherPlugin] ✓ Switched to existing window: {processName}");
                return PluginResult.Ok($"Switched to {processName}");
            }

            // 2. 切换失败，尝试启动
            if (args.TryGetValue("path", out var exePath) && !string.IsNullOrEmpty(exePath))
            {
                args.TryGetValue("arguments", out var arguments);

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
                    Debug.WriteLine($"[WinSwitcherPlugin] ✓ Launched new instance: {exePath}");
                    return PluginResult.Ok($"Launched {processName}");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[WinSwitcherPlugin] ❌ Launch failed: {ex.Message}");
                    return PluginResult.Error($"Launch failed: {ex.Message}");
                }
            }
            else
            {
                Debug.WriteLine($"[WinSwitcherPlugin] ❌ Cannot launch: No path specified");
                return PluginResult.Error($"Process not running and no path specified");
            }
        }
    }
}
