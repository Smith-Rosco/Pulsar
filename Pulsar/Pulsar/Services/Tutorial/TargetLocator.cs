// [Path]: Pulsar/Pulsar/Services/Tutorial/TargetLocator.cs

using System;
using System.Runtime.InteropServices;
using System.Windows;
using Microsoft.Extensions.Logging;
using Pulsar.Helpers.Tutorial;
using Pulsar.Models.Tutorial;
using Pulsar.Services.Interfaces;

namespace Pulsar.Services.Tutorial
{
    /// <summary>
    /// 教程目标定位服务实现
    /// 负责查找和定位教程目标元素的屏幕坐标
    /// </summary>
    public class TargetLocator : ITargetLocator
    {
        private readonly ILogger<TargetLocator> _logger;

        public TargetLocator(ILogger<TargetLocator> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// 获取目标元素的屏幕坐标
        /// </summary>
        public Rect? GetTargetBounds(TutorialTarget target)
        {
            // Explicit bounds win over dynamic lookup.
            if (target.Bounds.HasValue)
            {
                return target.Bounds.Value;
            }

            switch (target.Type)
            {
                case TutorialTargetType.UIElement:
                    if (!string.IsNullOrEmpty(target.ElementName))
                    {
                        return TutorialTargetRegistry.GetElementBounds(target.ElementName);
                    }
                    break;

                case TutorialTargetType.TrayIcon:
                    return GetTrayIconBounds();

                case TutorialTargetType.Window:
                    if (!string.IsNullOrEmpty(target.ElementName))
                    {
                        return GetWindowBounds(target.ElementName);
                    }
                    break;
            }

            return null;
        }

        /// <summary>
        /// 获取托盘图标区域的屏幕坐标
        /// </summary>
        public Rect? GetTrayIconBounds()
        {
            try
            {
                // Win11: hidden icons flyout. If it is open and visible, spotlight it.
                var overflow = FindWindow("NotifyIconOverflowWindow", null);
                if (overflow != IntPtr.Zero && IsWindowVisible(overflow))
                {
                    var overflowBounds = TryGetToolbarOrWindowBoundsDip(overflow);
                    if (overflowBounds.HasValue)
                    {
                        return overflowBounds.Value;
                    }
                }

                // Try primary + secondary taskbars (multi-monitor).
                foreach (var taskbar in EnumerateTaskbarWindows())
                {
                    var bounds = TryGetTrayToolbarBoundsFromTaskbar(taskbar);
                    if (bounds.HasValue)
                    {
                        return bounds.Value;
                    }
                }

                return GetFallbackTrayBounds();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[TargetLocator] Failed to get tray icon bounds, using fallback");
                return GetFallbackTrayBounds();
            }
        }

        /// <summary>
        /// 获取托盘区域的 Fallback 坐标（屏幕右下角）
        /// </summary>
        private Rect GetFallbackTrayBounds()
        {
            // Prefer WorkArea so we don't accidentally point into the taskbar.
            var workArea = SystemParameters.WorkArea;

            // Assume tray is near the bottom-right of the primary work area.
            // This is a UX fallback only; exact tray bounds depend on taskbar layout.
            const double width = 220;
            const double height = 50;

            return new Rect(workArea.Right - width, workArea.Bottom - height, width, height);
        }

        /// <summary>
        /// 获取指定窗口的屏幕坐标
        /// </summary>
        public Rect? GetWindowBounds(string windowName)
        {
            try
            {
                // 查找窗口
                foreach (Window window in System.Windows.Application.Current.Windows)
                {
                    if (window.GetType().Name == windowName && window.IsVisible)
                    {
                        var source = System.Windows.PresentationSource.FromVisual(window);
                        var screenPointPx = window.PointToScreen(new System.Windows.Point(0, 0));

                        if (source?.CompositionTarget != null)
                        {
                            var fromDevice = source.CompositionTarget.TransformFromDevice;
                            var topLeftDip = fromDevice.Transform(screenPointPx);
                            var sizeDip = new System.Windows.Size(window.ActualWidth, window.ActualHeight);
                            return new Rect(topLeftDip, sizeDip);
                        }

                        // Fallback: PointToScreen is in pixels; assume 1:1 scaling.
                        return new Rect(screenPointPx, new System.Windows.Size(window.ActualWidth, window.ActualHeight));
                    }
                }

                _logger.LogWarning("[TargetLocator] Window not found: {WindowName}", windowName);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[TargetLocator] Failed to get window bounds for: {WindowName}", windowName);
                return null;
            }
        }

        private System.Collections.Generic.IEnumerable<IntPtr> EnumerateTaskbarWindows()
        {
            // Primary taskbar (single instance).
            var primary = FindWindow("Shell_TrayWnd", null);
            if (primary != IntPtr.Zero)
            {
                yield return primary;
            }

            // Secondary taskbars can have multiple instances.
            IntPtr after = IntPtr.Zero;
            while (true)
            {
                after = FindWindowEx(IntPtr.Zero, after, "Shell_SecondaryTrayWnd", null);
                if (after == IntPtr.Zero)
                {
                    break;
                }

                yield return after;
            }
        }

        private Rect? TryGetTrayToolbarBoundsFromTaskbar(IntPtr taskbarHandle)
        {
            if (taskbarHandle == IntPtr.Zero)
            {
                return null;
            }

            // Child windows: Shell_TrayWnd/Shell_SecondaryTrayWnd
            //  -> TrayNotifyWnd
            //      -> SysPager (optional)
            //          -> ToolbarWindow32 (icons)
            var trayNotify = FindWindowEx(taskbarHandle, IntPtr.Zero, "TrayNotifyWnd", null);
            if (trayNotify == IntPtr.Zero)
            {
                return null;
            }

            // Some builds place the toolbar directly under TrayNotifyWnd.
            // Others use SysPager.
            var toolbar = FindWindowEx(trayNotify, IntPtr.Zero, "ToolbarWindow32", null);
            if (toolbar == IntPtr.Zero)
            {
                var sysPager = FindWindowEx(trayNotify, IntPtr.Zero, "SysPager", null);
                if (sysPager != IntPtr.Zero)
                {
                    toolbar = FindWindowEx(sysPager, IntPtr.Zero, "ToolbarWindow32", null);
                }
            }

            // Prefer the toolbar bounds; otherwise fallback to the notify window bounds.
            if (toolbar != IntPtr.Zero)
            {
                var toolbarBounds = GetWindowBoundsDip(toolbar);
                if (toolbarBounds.HasValue && toolbarBounds.Value.Width > 0 && toolbarBounds.Value.Height > 0)
                {
                    return toolbarBounds.Value;
                }
            }

            return GetWindowBoundsDip(trayNotify);
        }

        private Rect? TryGetToolbarOrWindowBoundsDip(IntPtr windowHandle)
        {
            if (windowHandle == IntPtr.Zero)
            {
                return null;
            }

            var toolbar = FindWindowEx(windowHandle, IntPtr.Zero, "ToolbarWindow32", null);
            if (toolbar != IntPtr.Zero)
            {
                var toolbarBounds = GetWindowBoundsDip(toolbar);
                if (toolbarBounds.HasValue)
                {
                    return toolbarBounds.Value;
                }
            }

            return GetWindowBoundsDip(windowHandle);
        }

        private Rect? GetWindowBoundsDip(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero)
            {
                return null;
            }

            if (!GetWindowRect(hwnd, out RECT rectPx))
            {
                return null;
            }

            var dpi = GetDpiForWindowSafe(hwnd);
            var scale = 96.0 / dpi;

            var left = rectPx.Left * scale;
            var top = rectPx.Top * scale;
            var width = (rectPx.Right - rectPx.Left) * scale;
            var height = (rectPx.Bottom - rectPx.Top) * scale;

            return new Rect(left, top, width, height);
        }

        private static uint GetDpiForWindowSafe(IntPtr hwnd)
        {
            try
            {
                // Available on Win10+; returns the DPI for the monitor hosting the window.
                var dpi = GetDpiForWindow(hwnd);
                if (dpi != 0)
                {
                    return dpi;
                }
            }
            catch
            {
            }

            try
            {
                var dpi = GetDpiForSystem();
                if (dpi != 0)
                {
                    return dpi;
                }
            }
            catch
            {
            }

            return 96;
        }

        // Win32 API Declarations
        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr FindWindow(string? lpClassName, string? lpWindowName);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string lpszClass, string? lpszWindow);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern uint GetDpiForWindow(IntPtr hwnd);

        [DllImport("user32.dll")]
        private static extern uint GetDpiForSystem();

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }
    }
}
