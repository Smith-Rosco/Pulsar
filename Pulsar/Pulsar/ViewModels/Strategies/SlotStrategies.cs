using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Diagnostics;
using Pulsar.Core.Plugin;
using Pulsar.Models;
using Pulsar.Native;
using Pulsar.Services;
using Pulsar.Services.Interfaces; // [New]
using System.Windows.Forms; // [New] For ToolTipIcon

namespace Pulsar.ViewModels.Strategies
{
    /// <summary>
    /// Defines the execution behavior for a slot.
    /// </summary>
    public interface IActionStrategy
    {
        Task ExecuteAsync(SlotViewModel slot, RadialMenuViewModel context);
    }

    /// <summary>
    /// No-op strategy for empty slots.
    /// </summary>
    public class NoOpStrategy : IActionStrategy
    {
        public static readonly NoOpStrategy Instance = new();
        public Task ExecuteAsync(SlotViewModel slot, RadialMenuViewModel context) => Task.CompletedTask;
    }

    /// <summary>
    /// Strategy for executing a plugin command.
    /// </summary>
    public class PluginActionStrategy : IActionStrategy
    {
        private readonly PluginSlot _pluginSlot;
        private readonly PluginRegistry _registry;
        private readonly PulsarContext _pulsarContext;
        private readonly ITrayService _trayService; // [New]

        public PluginActionStrategy(
            PluginSlot pluginSlot, 
            PluginRegistry registry, 
            PulsarContext pulsarContext,
            ITrayService trayService) // [New]
        {
            _pluginSlot = pluginSlot;
            _registry = registry;
            _pulsarContext = pulsarContext;
            _trayService = trayService;
        }

        public async Task ExecuteAsync(SlotViewModel slot, RadialMenuViewModel context)
        {
            context.SetActionExecuted(true);
            
            // [Fix] Hide the menu IMMEDIATELY before executing the plugin.
            // This prevents infinite loops if the plugin simulates input (e.g., Ctrl release)
            // which would otherwise re-trigger the hotkey hook while the menu is still visible.
            context.IsVisible = false;

            var result = await _registry.ExecuteAsync(_pluginSlot.PluginId, _pluginSlot.Action, _pluginSlot.Args, _pulsarContext);

            // [New] Elegant Error Handling
            if (!result.Success)
            {
                // Audio Feedback
                System.Media.SystemSounds.Hand.Play();

                // Visual Feedback (Notification)
                _trayService.ShowNotification("操作失败", result.Message ?? "未知错误", ToolTipIcon.Error);
            }
        }
    }

    /// <summary>
    /// Strategy for switching to a specific window.
    /// </summary>
    public class WindowSwitchStrategy : IActionStrategy
    {
        private readonly ProcessWindowInfo _window;
        private readonly IPluginUsageTracker? _usageTracker;
        private readonly IPluginHealthMonitor? _healthMonitor;
        private readonly IPluginLogService? _logService;

        public WindowSwitchStrategy(ProcessWindowInfo window, 
            IPluginUsageTracker? usageTracker = null, 
            IPluginHealthMonitor? healthMonitor = null,
            IPluginLogService? logService = null)
        {
            _window = window;
            _usageTracker = usageTracker;
            _healthMonitor = healthMonitor;
            _logService = logService;
        }

        public Task ExecuteAsync(SlotViewModel slot, RadialMenuViewModel context)
        {
            context.SetActionExecuted(true);

            var stopwatch = Stopwatch.StartNew();
            bool success = false;

            try
            {
                if (!WindowHelper.IsWindow(_window.Handle))
                {
                    System.Media.SystemSounds.Exclamation.Play();
                    context.IsVisible = false;
                    _logService?.Log("com.pulsar.winswitcher", PluginLogLevel.Warning,
                        "Window handle is no longer valid",
                        null,
                        "switch",
                        new Dictionary<string, string>
                        {
                            ["app"] = _window.ProcessName,
                            ["title"] = _window.Title
                        },
                        stopwatch.ElapsedMilliseconds);
                    return Task.CompletedTask;
                }

                // [Fix] Hide first to prevent focus stealing issues or visual glitches
                context.IsVisible = false;

                WindowHelper.SetForegroundWindow(_window.Handle);
                if (WindowHelper.IsIconic(_window.Handle))
                {
                    WindowHelper.ShowWindow(_window.Handle, 9); // SW_RESTORE
                }

                success = true;
                return Task.CompletedTask;
            }
            finally
            {
                stopwatch.Stop();

                // Record statistics for WinSwitcher plugin
                _usageTracker?.RecordExecution("com.pulsar.winswitcher", success, stopwatch.ElapsedMilliseconds, _window.ProcessName);
                
                if (success)
                {
                    _healthMonitor?.RecordSuccess("com.pulsar.winswitcher");
                    _logService?.Log("com.pulsar.winswitcher", PluginLogLevel.Info,
                        "Switched window",
                        null,
                        "switch",
                        new Dictionary<string, string>
                        {
                            ["app"] = _window.ProcessName,
                            ["title"] = _window.Title
                        },
                        stopwatch.ElapsedMilliseconds);
                }
            }
        }
    }

    /// <summary>
    /// Strategy for handling a process group (Switch to latest or Drill down).
    /// </summary>
    public class ProcessGroupStrategy : IActionStrategy
    {
        private readonly List<ProcessWindowInfo> _windows;
        private readonly IPluginUsageTracker? _usageTracker;
        private readonly IPluginHealthMonitor? _healthMonitor;
        private readonly IPluginLogService? _logService;

        public ProcessGroupStrategy(List<ProcessWindowInfo> windows, 
            IPluginUsageTracker? usageTracker = null, 
            IPluginHealthMonitor? healthMonitor = null,
            IPluginLogService? logService = null)
        {
            _windows = windows;
            _usageTracker = usageTracker;
            _healthMonitor = healthMonitor;
            _logService = logService;
        }

        public async Task ExecuteAsync(SlotViewModel slot, RadialMenuViewModel context)
        {
            var validWindows = _windows.Where(w => WindowHelper.IsWindow(w.Handle)).ToList();
            
            if (!validWindows.Any())
            {
                System.Media.SystemSounds.Exclamation.Play();
                // Trigger reload via context if possible, or just close
                context.IsVisible = false; 
                return;
            }

            // [Fix] Smart switch to the most recently ACTIVATED window (not most recently started)
            // Sort by LastActivationTime descending (most recent first)
            // This ensures we jump to the window the user was most recently using
            var target = validWindows.OrderByDescending(w => w.LastActivationTime).First();
            await new WindowSwitchStrategy(target, _usageTracker, _healthMonitor, _logService).ExecuteAsync(slot, context);
        }
        
        // Helper for the View Model to call explicitly for drill down
        public async Task EnterSubMenuAsync(RadialMenuViewModel context, string processName)
        {
             await context.EnterSubMenuAsync(_windows, processName);
        }
    }
    
    /// <summary>
    /// Strategy for Back/Cancel (Center Button).
    /// </summary>
    public class BackActionStrategy : IActionStrategy
    {
        public Task ExecuteAsync(SlotViewModel slot, RadialMenuViewModel context)
        {
            if (context.IsInSubMenu)
            {
                context.RestoreRootMenu();
            }
            else
            {
                context.IsVisible = false;
            }
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Strategy for launching a configured application that is not currently running.
    /// </summary>
    public class LaunchApplicationStrategy : IActionStrategy
    {
        private readonly PluginSlot _config;

        public LaunchApplicationStrategy(PluginSlot config)
        {
            _config = config;
        }

        public Task ExecuteAsync(SlotViewModel slot, RadialMenuViewModel context)
        {
            context.SetActionExecuted(true);
            context.IsVisible = false;

            try
            {
                // Extract path and arguments from config
                string? path = null;
                string? arguments = null;

                if (_config.Args.TryGetValue("path", out var pathValue))
                {
                    path = pathValue;
                }

                if (_config.Args.TryGetValue("arguments", out var argsValue))
                {
                    arguments = argsValue;
                }

                if (string.IsNullOrWhiteSpace(path))
                {
                    System.Media.SystemSounds.Hand.Play();
                    return Task.CompletedTask;
                }

                // Launch the application
                var startInfo = new ProcessStartInfo
                {
                    FileName = path,
                    Arguments = arguments ?? string.Empty,
                    UseShellExecute = true
                };

                Process.Start(startInfo);
            }
            catch
            {
                System.Media.SystemSounds.Hand.Play();
            }

            return Task.CompletedTask;
        }
    }
}
