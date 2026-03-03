using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Windows.Forms;
using Microsoft.Extensions.Logging;
using Pulsar.Native;
using Pulsar.Services.Interfaces;

namespace Pulsar.Services
{
    /// <summary>
    /// 远程桌面伪全屏服务实现
    /// 使用 SetWinEventHook 监听窗口事件，自动将远程桌面从真全屏转换为伪全屏
    /// </summary>
    public class RemoteDesktopService : IRemoteDesktopService
    {
        private readonly ILogger<RemoteDesktopService> _logger;
        private IntPtr _hookHandle = IntPtr.Zero;
        private WindowHelper.WinEventDelegate? _winEventDelegate;
        private readonly HashSet<IntPtr> _processedWindows = new();
        private readonly object _lock = new();

        public RemoteDesktopService(ILogger<RemoteDesktopService> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// 启用远程桌面伪全屏功能
        /// </summary>
        public void EnableFakeFullscreen()
        {
            lock (_lock)
            {
                if (_hookHandle != IntPtr.Zero)
                {
                    _logger.LogWarning("Remote Desktop fake fullscreen is already enabled");
                    return;
                }

                try
                {
                    // 创建委托并保持引用（防止 GC 回收）
                    _winEventDelegate = new WindowHelper.WinEventDelegate(WinEventProc);

                    // 注册事件钩子监听窗口位置/大小变化
                    _hookHandle = WindowHelper.SetWinEventHook(
                        WindowHelper.EVENT_OBJECT_LOCATIONCHANGE,
                        WindowHelper.EVENT_OBJECT_LOCATIONCHANGE,
                        IntPtr.Zero,
                        _winEventDelegate,
                        0, 0,
                        WindowHelper.WINEVENT_OUTOFCONTEXT
                    );

                    if (_hookHandle == IntPtr.Zero)
                    {
                        _logger.LogError("Failed to register SetWinEventHook");
                        return;
                    }

                    _logger.LogInformation("Remote Desktop fake fullscreen enabled");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to enable Remote Desktop fake fullscreen");
                }
            }
        }

        /// <summary>
        /// 禁用远程桌面伪全屏功能
        /// </summary>
        public void DisableFakeFullscreen()
        {
            lock (_lock)
            {
                if (_hookHandle == IntPtr.Zero)
                {
                    return;
                }

                try
                {
                    WindowHelper.UnhookWinEvent(_hookHandle);
                    _hookHandle = IntPtr.Zero;
                    _winEventDelegate = null;
                    _processedWindows.Clear();

                    _logger.LogInformation("Remote Desktop fake fullscreen disabled");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to disable Remote Desktop fake fullscreen");
                }
            }
        }

        /// <summary>
        /// 检测当前是否在远程桌面会话中
        /// </summary>
        public bool IsInRemoteDesktopSession()
        {
            return SystemInformation.TerminalServerSession;
        }

        /// <summary>
        /// Windows 事件钩子回调函数
        /// </summary>
        private void WinEventProc(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
        {
            try
            {
                // 忽略无效窗口句柄
                if (hwnd == IntPtr.Zero || !WindowHelper.IsWindow(hwnd))
                {
                    return;
                }

                // 检查是否为远程桌面窗口
                if (!IsRemoteDesktopWindow(hwnd))
                {
                    return;
                }

                // 检查是否为全屏状态
                if (!IsWindowFullscreen(hwnd))
                {
                    return;
                }

                // 防止重复转换（使用缓存）
                lock (_lock)
                {
                    if (_processedWindows.Contains(hwnd))
                    {
                        return;
                    }

                    _processedWindows.Add(hwnd);
                }

                // 执行伪全屏转换
                ConvertToFakeFullscreen(hwnd);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in WinEventProc callback");
            }
        }

        /// <summary>
        /// 检测是否为远程桌面窗口
        /// </summary>
        private bool IsRemoteDesktopWindow(IntPtr hwnd)
        {
            try
            {
                // 方法 1：检查窗口类名
                StringBuilder className = new StringBuilder(256);
                WindowHelper.GetClassName(hwnd, className, className.Capacity);
                
                if (className.ToString() == "TscShellContainerClass")
                {
                    return true;
                }

                // 方法 2：检查进程名
                WindowHelper.GetWindowThreadProcessId(hwnd, out uint processId);
                if (processId == 0)
                {
                    return false;
                }

                var process = Process.GetProcessById((int)processId);
                if (process.ProcessName.Equals("mstsc", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error checking if window is Remote Desktop");
            }

            return false;
        }

        /// <summary>
        /// 检测窗口是否为全屏状态
        /// </summary>
        private bool IsWindowFullscreen(IntPtr hwnd)
        {
            try
            {
                WindowHelper.GetWindowRect(hwnd, out WindowHelper.RECT rect);
                var screen = Screen.FromHandle(hwnd);

                // 窗口尺寸是否覆盖整个屏幕（包括任务栏）
                return rect.Left == screen.Bounds.Left &&
                       rect.Top == screen.Bounds.Top &&
                       rect.Right == screen.Bounds.Right &&
                       rect.Bottom == screen.Bounds.Bottom;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error checking if window is fullscreen");
                return false;
            }
        }

        /// <summary>
        /// 将窗口转换为伪全屏（无边框窗口化）
        /// </summary>
        private void ConvertToFakeFullscreen(IntPtr hwnd)
        {
            try
            {
                _logger.LogInformation("Detected RDP fullscreen, converting to fake fullscreen");

                // 1. 移除 WS_POPUP 样式（真全屏标志）
                long style = WindowHelper.GetWindowLong(hwnd, WindowHelper.GWL_STYLE);
                style &= ~WindowHelper.WS_POPUP;
                style |= WindowHelper.WS_OVERLAPPED;
                WindowHelper.SetWindowLong(hwnd, WindowHelper.GWL_STYLE, style);

                // 2. 移除 WS_EX_TOPMOST（置顶标志）
                long exStyle = WindowHelper.GetWindowLong(hwnd, WindowHelper.GWL_EXSTYLE);
                exStyle &= ~WindowHelper.WS_EX_TOPMOST;
                WindowHelper.SetWindowLong(hwnd, WindowHelper.GWL_EXSTYLE, exStyle);

                // 3. 调整窗口尺寸（留出任务栏空间）
                var screen = Screen.FromHandle(hwnd);
                bool success = WindowHelper.SetWindowPos(
                    hwnd,
                    WindowHelper.HWND_NOTOPMOST,
                    screen.WorkingArea.Left,
                    screen.WorkingArea.Top,
                    screen.WorkingArea.Width,
                    screen.WorkingArea.Height,
                    WindowHelper.SWP_FRAMECHANGED | WindowHelper.SWP_SHOWWINDOW
                );

                if (success)
                {
                    _logger.LogInformation("Successfully converted RDP to fake fullscreen");
                }
                else
                {
                    _logger.LogWarning("SetWindowPos failed during fake fullscreen conversion");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to convert RDP to fake fullscreen");
            }
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            DisableFakeFullscreen();
        }
    }
}
