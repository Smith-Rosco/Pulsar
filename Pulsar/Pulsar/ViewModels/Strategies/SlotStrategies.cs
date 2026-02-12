using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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

        public WindowSwitchStrategy(ProcessWindowInfo window)
        {
            _window = window;
        }

        public Task ExecuteAsync(SlotViewModel slot, RadialMenuViewModel context)
        {
            context.SetActionExecuted(true);

            if (!WindowHelper.IsWindow(_window.Handle))
            {
                System.Media.SystemSounds.Exclamation.Play();
                context.IsVisible = false;
                return Task.CompletedTask;
            }

            // [Fix] Hide first to prevent focus stealing issues or visual glitches
            context.IsVisible = false;

            WindowHelper.SetForegroundWindow(_window.Handle);
            if (WindowHelper.IsIconic(_window.Handle))
            {
                WindowHelper.ShowWindow(_window.Handle, 9); // SW_RESTORE
            }

            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Strategy for handling a process group (Switch to latest or Drill down).
    /// </summary>
    public class ProcessGroupStrategy : IActionStrategy
    {
        private readonly List<ProcessWindowInfo> _windows;

        public ProcessGroupStrategy(List<ProcessWindowInfo> windows)
        {
            _windows = windows;
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

            // If only one window, switch directly
            // User Requirement Update: Even for single windows, if it's a "Group" slot (Process level),
            // the logic in original code was:
            // "Directly switch to the most recently active window" (ExecuteSelection)
            // But HandleLeftClick had slightly different logic.
            // Let's unify: Click/Execute on Process Group -> Switch to Latest.
            // Wait, original Drill Down Logic:
            /*
                var validWindows = windows.Where(w => WindowHelper.IsWindow(w.Handle)).ToList();
                if (validWindows.Any())
                {
                    // Requirement: Directly switch to the most recently active window
                    SwitchToWindow(validWindows.First());
                }
            */
            // So default action is Switch. Drill down is via "EnterSubMenu" which was called in HandleLeftClick
            // BUT wait, HandleLeftClick called EnterSubMenu only if count > 1.
            
            // Wait, this strategy is for "ExecuteSelection" (Keyboard Release or Click).
            // If the user wants to drill down, that logic was inside HandleLeftClick explicitly calling EnterSubMenu.
            // Standard "Execute" means "Do the primary action".
            // For a Process Group, primary action is "Switch to most recent".
            
            // HOWEVER, if we want to support Drill Down, that's usually a specific interaction (like Hover + Time, or Click).
            // In the original code, HandleLeftClick did Drill Down. ExecuteSelection did Switch.
            
            // To support both, we might need a separate method on Strategy like "OnDrillDown" or handle it here.
            // Let's stick to Execute = Switch for now, as per original ExecuteSelection logic.
            
            var target = validWindows.First();
            await new WindowSwitchStrategy(target).ExecuteAsync(slot, context);
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
}
