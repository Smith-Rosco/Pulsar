using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using Pulsar.Core.Plugin;
using Pulsar.Core.Messages;
using Pulsar.Core.Localization;
using Pulsar.Models;
using Pulsar.Native;
using Pulsar.Services;
using Pulsar.Services.ActionFeedback;
using Pulsar.Services.Interfaces; // [New]

namespace Pulsar.ViewModels.Strategies
{
    /// <summary>
    /// Defines the execution behavior for a slot.
    /// </summary>
    public interface IActionStrategy
    {
        Task ExecuteAsync(SlotViewModel slot, IMenuSession context, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// No-op strategy for empty slots.
    /// </summary>
    public class NoOpStrategy : IActionStrategy
    {
        public static readonly NoOpStrategy Instance = new();
        public Task ExecuteAsync(SlotViewModel slot, IMenuSession context, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    /// <summary>
    /// Strategy for executing a plugin command.
    /// </summary>
    public class PluginActionStrategy : IActionStrategy
    {
        private readonly PluginSlot _pluginSlot;
        private readonly IPluginRegistry _registry;
        private readonly PulsarContext _pulsarContext;
        private readonly ITrayService _trayService; // [New]
        private readonly IActionFeedbackService _feedbackService;
        private readonly IActionFeedbackPresenter _feedbackPresenter;
        private readonly IPluginUsageTracker? _usageTracker;

        public PluginActionStrategy(
            PluginSlot pluginSlot, 
            IPluginRegistry registry, 
            PulsarContext pulsarContext,
            ITrayService trayService,
            IActionFeedbackService feedbackService,
            IPluginUsageTracker? usageTracker = null,
            IActionFeedbackPresenter? feedbackPresenter = null)
        {
            _pluginSlot = pluginSlot;
            _registry = registry;
            _pulsarContext = pulsarContext;
            _trayService = trayService;
            _feedbackService = feedbackService;
            _usageTracker = usageTracker;
            _feedbackPresenter = feedbackPresenter ?? new ActionFeedbackPresenter(trayService);
        }

        public async Task ExecuteAsync(SlotViewModel slot, IMenuSession context, CancellationToken cancellationToken = default)
        {
            context.SetActionExecuted(true);
            
            // [Fix] Hide the menu IMMEDIATELY before executing the plugin.
            // This prevents infinite loops if the plugin simulates input (e.g., Ctrl release)
            // which would otherwise re-trigger the hotkey hook while the menu is still visible.
            context.IsVisible = false;

            var result = await _registry.ExecuteAsync(_pluginSlot.PluginId, _pluginSlot.Action, _pluginSlot.Args, _pulsarContext);

            if (result.Success)
            {
                // [Fix] winSwitcher's "switch" action launched from a not-running slot
                // (plugin path) must publish TutorialActionKind.Switch so the tutorial
                // advances on first launch — otherwise the first trigger silently fails
                // to progress and only the second (switch) trigger works.
                var kind = string.Equals(_pluginSlot.PluginId, "com.pulsar.winswitcher", StringComparison.OrdinalIgnoreCase)
                    && string.Equals(_pluginSlot.Action, "switch", StringComparison.OrdinalIgnoreCase)
                        ? TutorialActionKind.Switch
                        : TutorialActionKind.Command;

                WeakReferenceMessenger.Default.Send(new ActionExecutionMessage(
                    kind,
                    _pluginSlot.PluginId,
                    _pluginSlot.Action,
                    success: true));
            }

            // [New] Elegant Error Handling
            if (!result.Success)
            {
                var feedback = _feedbackService.Create(_pluginSlot.PluginId, _pluginSlot.Action, result);
                _feedbackPresenter.Present(result, feedback);
            }
        }
    }

    /// <summary>
    /// Strategy for switching to a specific window.
    /// </summary>
    public class WindowSwitchStrategy : IActionStrategy
    {
        private readonly ProcessWindowInfo _window;
        private readonly IWindowService _windowService;
        private readonly IPluginUsageTracker? _usageTracker;
        private readonly IPluginHealthMonitor? _healthMonitor;
        private readonly IPluginLogService? _logService;
        private readonly ILogger<WindowSwitchStrategy>? _logger;
        private readonly IActionFeedbackService? _feedbackService;
        private readonly IActionFeedbackPresenter? _feedbackPresenter;

        public WindowSwitchStrategy(ProcessWindowInfo window,
            IWindowService windowService,
            IPluginUsageTracker? usageTracker = null, 
            IPluginHealthMonitor? healthMonitor = null,
            IPluginLogService? logService = null,
            ILogger<WindowSwitchStrategy>? logger = null,
            IActionFeedbackService? feedbackService = null,
            IActionFeedbackPresenter? feedbackPresenter = null)
        {
            _window = window;
            _windowService = windowService;
            _usageTracker = usageTracker;
            _healthMonitor = healthMonitor;
            _logService = logService;
            _logger = logger;
            _feedbackService = feedbackService;
            _feedbackPresenter = feedbackPresenter;
        }

        public Task ExecuteAsync(SlotViewModel slot, IMenuSession context, CancellationToken cancellationToken = default)
        {
            _logger?.LogInformation("[WinSwitch] ExecuteAsync START: hWnd=0x{Hwnd:X} title='{Title}' process='{Process}'",
                _window.Handle.ToInt64(), _window.Title, _window.ProcessName);

            context.SetActionExecuted(true);

            var stopwatch = Stopwatch.StartNew();
            bool success = false;

            try
            {
                // Prevent focus restore from undoing the switch after menu dismisses.
                _windowService.SetFocusRestoreMode(FocusRestoreMode.NoRestore);
                _logger?.LogInformation("[WinSwitch] SetFocusRestoreMode=NoRestore, hiding menu...");

                // Hide first to avoid focus-steal and visual glitches while switching foreground windows.
                context.IsVisible = false;
                _logger?.LogInformation("[WinSwitch] Menu hidden, calling ActivateWindow(0x{Hwnd:X})...",
                    _window.Handle.ToInt64());

                if (!_windowService.ActivateWindow(_window))
                {
                    _logger?.LogWarning("[WinSwitch] ActivateWindow FAILED for 0x{Hwnd:X}", _window.Handle.ToInt64());
                    _logService?.Log("com.pulsar.winswitcher", PluginLogLevel.Warning,
                        "Window activation failed",
                        null,
                        "switch",
                        new Dictionary<string, string>
                        {
                            ["app"] = _window.ProcessName,
                            ["title"] = _window.Title
                        },
                        stopwatch.ElapsedMilliseconds);
                    PresentSwitchFailure();
                    return Task.CompletedTask;
                }

                success = true;
                _logger?.LogInformation("[WinSwitch] ActivateWindow SUCCESS for 0x{Hwnd:X} elapsed={Elapsed}ms",
                    _window.Handle.ToInt64(), stopwatch.ElapsedMilliseconds);
                WeakReferenceMessenger.Default.Send(new ActionExecutionMessage(
                    TutorialActionKind.Switch,
                    "com.pulsar.winswitcher",
                    "switch",
                    success: true));
                return Task.CompletedTask;
            }
            finally
            {
                stopwatch.Stop();

                // Record statistics for WinSwitcher plugin
                _usageTracker?.RecordExecution("com.pulsar.winswitcher", success, stopwatch.ElapsedMilliseconds, _window.ProcessName, slot.SlotIndex, "Task");
                
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

        private void PresentSwitchFailure()
        {
            if (_feedbackService != null && _feedbackPresenter != null)
            {
                var result = PluginResult.Error(
                    "Window activation failed",
                    PluginErrorSeverity.Recoverable,
                    PluginErrorCode.ExecutionFailed);
                var feedback = _feedbackService.Create("com.pulsar.winswitcher", "switch", result);
                _feedbackPresenter.Present(result, feedback);
                return;
            }

            System.Media.SystemSounds.Exclamation.Play();
        }
    }

    /// <summary>
    /// Strategy for handling a process group (Switch to latest or Drill down).
    /// </summary>
    public class ProcessGroupStrategy : IActionStrategy
    {
        private readonly List<ProcessWindowInfo> _windows;
        private readonly IWindowService _windowService;
        private readonly IPluginUsageTracker? _usageTracker;
        private readonly IPluginHealthMonitor? _healthMonitor;
        private readonly IPluginLogService? _logService;
        private readonly ILogger<ProcessGroupStrategy>? _logger;
        private readonly IActionFeedbackService? _feedbackService;
        private readonly IActionFeedbackPresenter? _feedbackPresenter;

        public ProcessGroupStrategy(List<ProcessWindowInfo> windows,
            IWindowService windowService,
            IPluginUsageTracker? usageTracker = null, 
            IPluginHealthMonitor? healthMonitor = null,
            IPluginLogService? logService = null,
            ILogger<ProcessGroupStrategy>? logger = null,
            IActionFeedbackService? feedbackService = null,
            IActionFeedbackPresenter? feedbackPresenter = null)
        {
            _windows = windows;
            _windowService = windowService;
            _usageTracker = usageTracker;
            _healthMonitor = healthMonitor;
            _logService = logService;
            _logger = logger;
            _feedbackService = feedbackService;
            _feedbackPresenter = feedbackPresenter;
        }

        public async Task ExecuteAsync(SlotViewModel slot, IMenuSession context, CancellationToken cancellationToken = default)
        {
            // [Enhancement] Use WindowService's smart window selection
            var currentForegroundHandle = _windowService.GetPreviousWindow();
            _logger?.LogDebug(
                "[ProcessGroupStrategy] Direct trigger for '{Label}' with {WindowCount} candidates. CurrentForegroundHandle={CurrentForeground}",
                slot.Label,
                _windows.Count,
                currentForegroundHandle);

            var target = _windowService.SelectTargetWindow(
                _windows,
                new WindowSelectionRequest
                {
                    Intent = WindowSelectionIntent.GroupedRootDirectTrigger,
                    SkipMode = WindowSelectionSkipMode.None,
                    CurrentForegroundHandle = currentForegroundHandle,
                    PreferredMonitorRect = GetCursorMonitorRect()
                }).SelectedWindow;
            
            if (target == null)
            {
                context.IsVisible = false;
                PresentNoTarget();
                return;
            }

            await new WindowSwitchStrategy(
                target, _windowService, _usageTracker, _healthMonitor, _logService,
                feedbackService: _feedbackService,
                feedbackPresenter: _feedbackPresenter).ExecuteAsync(slot, context);
        }

        private void PresentNoTarget()
        {
            if (_feedbackService != null && _feedbackPresenter != null)
            {
                var result = PluginResult.Error(
                    "No matching window available",
                    PluginErrorSeverity.Recoverable,
                    PluginErrorCode.NotFound);
                var feedback = _feedbackService.Create("com.pulsar.winswitcher", "switch", result);
                _feedbackPresenter.Present(result, feedback);
                return;
            }

            System.Media.SystemSounds.Exclamation.Play();
        }
        
        // Helper for the View Model to call explicitly for drill down
        public async Task EnterSubMenuAsync(IMenuSession context, string processName, int clickedSlotIndex)
        {
             await context.EnterSubMenuAsync(_windows, processName, clickedSlotIndex);
        }

        private static PulsarNative.RECT? GetCursorMonitorRect()
        {
            if (!PulsarNative.GetCursorPos(out var cursorPos))
            {
                return null;
            }

            var hMonitor = PulsarNative.MonitorFromPoint(cursorPos, PulsarNative.MONITOR_DEFAULTTONEAREST);
            if (hMonitor == IntPtr.Zero)
            {
                return null;
            }

            var monitorInfo = new PulsarNative.MONITORINFO
            {
                cbSize = System.Runtime.InteropServices.Marshal.SizeOf<PulsarNative.MONITORINFO>()
            };

            if (!PulsarNative.GetMonitorInfo(hMonitor, ref monitorInfo))
            {
                return null;
            }

            return monitorInfo.rcWork;
        }
    }
    
    /// <summary>
    /// Strategy for Back/Cancel (Center Button).
    /// </summary>
    public class BackActionStrategy : IActionStrategy
    {
        public Task ExecuteAsync(SlotViewModel slot, IMenuSession context, CancellationToken cancellationToken = default)
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
}
