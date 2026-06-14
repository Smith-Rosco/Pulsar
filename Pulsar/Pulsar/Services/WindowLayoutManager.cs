// [Path]: Pulsar/Pulsar/Services/WindowLayoutManager.cs

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Extensions.Logging;
using Pulsar.Models.Tutorial;
using Pulsar.Services.Interfaces;

namespace Pulsar.Services
{
    /// <summary>
    /// 教程窗口布局管理器实现
    /// </summary>
    public class WindowLayoutManager : IWindowLayoutManager
    {
        private readonly ILogger<WindowLayoutManager> _logger;
        private readonly Dictionary<string, Rect> _originalLayouts = new();

        public WindowLayoutManager(ILogger<WindowLayoutManager> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// 应用布局配置
        /// </summary>
        public async Task ApplyLayoutAsync(TutorialLayout layout)
        {
            try
            {
                // 应用目标窗口布局（如 SettingsWindow）
                if (layout.TargetWindow != null)
                {
                    // 假设目标窗口是 SettingsWindow
                    SetWpfWindowLayout("SettingsWindow", layout.TargetWindow);
                }

                // 应用外部窗口布局（如 Notepad）
                foreach (var kvp in layout.ExternalWindows)
                {
                    var processName = kvp.Key;
                    var windowLayout = kvp.Value;

                    // 等待窗口出现（最多等待 3 秒）
                    var hwnd = await WaitForWindowAsync(processName, TimeSpan.FromSeconds(3));
                    if (hwnd.HasValue)
                    {
                        SetExternalWindowLayout(hwnd.Value, windowLayout);
                    }
                    else
                    {
                        _logger.LogWarning("[WindowLayoutManager] External window not found: {ProcessName}", processName);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[WindowLayoutManager] Failed to apply layout");
            }
        }

        /// <summary>
        /// 恢复窗口原始布局
        /// </summary>
        public Task RestoreLayoutAsync()
        {
            try
            {
                foreach (var kvp in _originalLayouts)
                {
                    var windowTypeName = kvp.Key;
                    var originalBounds = kvp.Value;

                    var layout = new WindowLayout
                    {
                        Left = originalBounds.Left,
                        Top = originalBounds.Top,
                        Width = originalBounds.Width,
                        Height = originalBounds.Height,
                        IsRelative = false
                    };

                    SetWpfWindowLayout(windowTypeName, layout);
                }

                _originalLayouts.Clear();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[WindowLayoutManager] Failed to restore layout");
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// 获取外部进程窗口句柄
        /// </summary>
        public IntPtr? FindExternalWindow(string processName)
        {
            try
            {
                var processes = Process.GetProcessesByName(processName);
                if (processes.Length > 0)
                {
                    var process = processes[0];
                    var hwnd = process.MainWindowHandle;
                    if (hwnd != IntPtr.Zero)
                    {
                        return hwnd;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[WindowLayoutManager] Failed to find external window: {ProcessName}", processName);
            }

            return null;
        }

        /// <summary>
        /// 设置外部窗口布局
        /// </summary>
        public void SetExternalWindowLayout(IntPtr hwnd, WindowLayout layout)
        {
            try
            {
                var screen = SystemParameters.WorkArea;

                int x = layout.IsRelative
                    ? (int)(screen.Width * layout.Left)
                    : (int)layout.Left;
                int y = layout.IsRelative
                    ? (int)(screen.Height * layout.Top)
                    : (int)layout.Top;
                int width = layout.IsRelative
                    ? (int)(screen.Width * layout.Width)
                    : (int)layout.Width;
                int height = layout.IsRelative
                    ? (int)(screen.Height * layout.Height)
                    : (int)layout.Height;

                const uint SWP_NOZORDER = 0x0004;
                const uint SWP_SHOWWINDOW = 0x0040;

                SetWindowPos(hwnd, IntPtr.Zero, x, y, width, height, SWP_NOZORDER | SWP_SHOWWINDOW);

                _logger.LogInformation("[WindowLayoutManager] Set external window layout: hwnd={Hwnd}, x={X}, y={Y}, w={Width}, h={Height}",
                    hwnd, x, y, width, height);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[WindowLayoutManager] Failed to set external window layout");
            }
        }

        /// <summary>
        /// 设置 WPF 窗口布局
        /// </summary>
        public void SetWpfWindowLayout(string windowTypeName, WindowLayout layout)
        {
            try
            {
                // 查找窗口
                var window = System.Windows.Application.Current.Windows.OfType<Window>()
                    .FirstOrDefault(w => w.GetType().Name == windowTypeName && w.IsVisible);

                if (window == null)
                {
                    _logger.LogWarning("[WindowLayoutManager] WPF window not found: {WindowTypeName}", windowTypeName);
                    return;
                }

                // 保存原始布局（如果尚未保存）
                if (!_originalLayouts.ContainsKey(windowTypeName))
                {
                    _originalLayouts[windowTypeName] = new Rect(window.Left, window.Top, window.Width, window.Height);
                }

                var screen = SystemParameters.WorkArea;

                double left = layout.IsRelative
                    ? screen.Width * layout.Left
                    : layout.Left;
                double top = layout.IsRelative
                    ? screen.Height * layout.Top
                    : layout.Top;
                double width = layout.IsRelative
                    ? screen.Width * layout.Width
                    : layout.Width;
                double height = layout.IsRelative
                    ? screen.Height * layout.Height
                    : layout.Height;

                window.Left = left;
                window.Top = top;
                window.Width = width;
                window.Height = height;

                _logger.LogInformation("[WindowLayoutManager] Set WPF window layout: {WindowTypeName}, x={X}, y={Y}, w={Width}, h={Height}",
                    windowTypeName, left, top, width, height);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[WindowLayoutManager] Failed to set WPF window layout: {WindowTypeName}", windowTypeName);
            }
        }

        /// <summary>
        /// 等待窗口出现
        /// </summary>
        private async Task<IntPtr?> WaitForWindowAsync(string processName, TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            var stopwatch = Stopwatch.StartNew();

            while (stopwatch.Elapsed < timeout)
            {
                var hwnd = FindExternalWindow(processName);
                if (hwnd.HasValue)
                {
                    return hwnd;
                }

                await Task.Delay(200, cancellationToken);
            }

            return null;
        }

        // Win32 API Declarations
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
            int X, int Y, int cx, int cy, uint uFlags);
    }
}
