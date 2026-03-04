using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
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
        private readonly ITrayService? _trayService;
        private IntPtr _hookHandle = IntPtr.Zero;
        private WindowHelper.WinEventDelegate? _winEventDelegate;
        private readonly Dictionary<IntPtr, DateTime> _processedWindows = new();
        private readonly object _lock = new();
        private const int REPROCESS_COOLDOWN_SECONDS = 5;
        private const int FULLSCREEN_TOLERANCE = 10;

        private CancellationTokenSource? _retryCts;
        private Task? _retryTask;
        private readonly SemaphoreSlim _scanGate = new(1, 1);
        private DateTime _lastSuccessNotificationUtc = DateTime.MinValue;
        private static readonly TimeSpan RetryInterval = TimeSpan.FromSeconds(2);
        private static readonly TimeSpan SuccessNotifyCooldown = TimeSpan.FromSeconds(30);

        public RemoteDesktopService(ILogger<RemoteDesktopService> logger, ITrayService trayService)
        {
            _logger = logger;
            _trayService = trayService;
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

                    // 主动重试扫描：解决“真全屏已存在/事件不触发/时机问题”
                    // 即使托盘在真全屏不可交互，也能自动处理并给出成功提示。
                    StartRetryLoopIfNeeded();
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

                    StopRetryLoop();

                    _logger.LogInformation("Remote Desktop fake fullscreen disabled");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to disable Remote Desktop fake fullscreen");
                }
            }
        }

        private void StartRetryLoopIfNeeded()
        {
            lock (_lock)
            {
                if (_retryTask != null && !_retryTask.IsCompleted)
                {
                    return;
                }

                _retryCts?.Cancel();
                _retryCts?.Dispose();
                _retryCts = new CancellationTokenSource();
                var token = _retryCts.Token;

                _retryTask = Task.Run(async () => await RetryLoopAsync(token), token);
            }
        }

        private void StopRetryLoop()
        {
            try
            {
                _retryCts?.Cancel();
            }
            catch
            {
                // ignored
            }
        }

        private async Task RetryLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    int count = ScanAndConvertAllRdpWindows();
                    if (count > 0)
                    {
                        TryNotifyConversionSuccess(count);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Retry scan loop error");
                }

                try
                {
                    await Task.Delay(RetryInterval, token);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }

        private void TryNotifyConversionSuccess(int convertedCount)
        {
            var nowUtc = DateTime.UtcNow;
            if (nowUtc - _lastSuccessNotificationUtc < SuccessNotifyCooldown)
            {
                return;
            }

            _lastSuccessNotificationUtc = nowUtc;

            // 说明：mstsc 真全屏时宿主桌面不可见，气泡提示可能被“遮住”。
            // 但一旦转换为伪全屏（露出任务栏），该提示通常可见。
            try
            {
                _trayService?.ShowNotification(
                    "Pulsar",
                    $"Remote Desktop converted: {convertedCount} window(s) -> fake fullscreen",
                    ToolTipIcon.Info);
            }
            catch
            {
                // ignored
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

                // 防止重复转换（使用时间窗口防抖）
                lock (_lock)
                {
                    if (_processedWindows.TryGetValue(hwnd, out DateTime lastProcessed))
                    {
                        if ((DateTime.Now - lastProcessed).TotalSeconds < REPROCESS_COOLDOWN_SECONDS)
                        {
                            return; // 冷却期内，跳过
                        }
                    }

                    _processedWindows[hwnd] = DateTime.Now;
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

                // 容错检测：允许±10像素误差（处理DPI缩放、边框等问题）
                bool isFullscreen = 
                    Math.Abs(rect.Left - screen.Bounds.Left) <= FULLSCREEN_TOLERANCE &&
                    Math.Abs(rect.Top - screen.Bounds.Top) <= FULLSCREEN_TOLERANCE &&
                    Math.Abs(rect.Right - screen.Bounds.Right) <= FULLSCREEN_TOLERANCE &&
                    Math.Abs(rect.Bottom - screen.Bounds.Bottom) <= FULLSCREEN_TOLERANCE;

                _logger.LogDebug(
                    "Window {Hwnd} fullscreen check: Rect=({L},{T},{R},{B}), Screen=({SL},{ST},{SR},{SB}), Result={Result}",
                    hwnd, rect.Left, rect.Top, rect.Right, rect.Bottom,
                    screen.Bounds.Left, screen.Bounds.Top, screen.Bounds.Right, screen.Bounds.Bottom,
                    isFullscreen);

                return isFullscreen;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error checking if window {Hwnd} is fullscreen", hwnd);
                return false;
            }
        }

        /// <summary>
        /// 将窗口转换为伪全屏（无边框窗口化）
        /// </summary>
        private bool ConvertToFakeFullscreen(IntPtr hwnd)
        {
            try
            {
                _logger.LogInformation("Detected RDP fullscreen window {Hwnd}, converting to fake fullscreen", hwnd);

                // 1. 移除 WS_POPUP 样式（真全屏标志）
                long style = WindowHelper.GetWindowLong(hwnd, WindowHelper.GWL_STYLE);
                long originalStyle = style;
                style &= ~WindowHelper.WS_POPUP;
                style |= WindowHelper.WS_OVERLAPPED;
                WindowHelper.SetWindowLong(hwnd, WindowHelper.GWL_STYLE, style);

                // 2. 移除 WS_EX_TOPMOST（置顶标志）
                long exStyle = WindowHelper.GetWindowLong(hwnd, WindowHelper.GWL_EXSTYLE);
                long originalExStyle = exStyle;
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
                    _logger.LogInformation(
                        "Successfully converted RDP window {Hwnd} to fake fullscreen. " +
                        "Style: 0x{OldStyle:X} -> 0x{NewStyle:X}, ExStyle: 0x{OldExStyle:X} -> 0x{NewExStyle:X}",
                        hwnd, originalStyle, style, originalExStyle, exStyle);
                    return true;
                }
                else
                {
                    _logger.LogWarning("SetWindowPos failed for window {Hwnd} during fake fullscreen conversion", hwnd);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to convert RDP window {Hwnd} to fake fullscreen", hwnd);
                return false;
            }
        }

        /// <summary>
        /// 扫描所有现有的远程桌面窗口并转换为伪全屏
        /// </summary>
        public int ScanAndConvertAllRdpWindows()
        {
            int convertedCount = 0;
            bool scanLockTaken = false;
            
            _logger.LogInformation("Starting scan for fullscreen RDP windows...");
            
            try
            {
                if (!_scanGate.Wait(0))
                {
                    return 0;
                }

                scanLockTaken = true;

                WindowHelper.EnumWindows((hwnd, lParam) =>
                {
                    try
                    {
                        // 只处理可见窗口
                        if (!WindowHelper.IsWindowVisible(hwnd))
                        {
                            return true;
                        }

                        // 检查是否为远程桌面窗口
                        if (!IsRemoteDesktopWindow(hwnd))
                        {
                            return true;
                        }

                        // 检查是否为全屏状态
                        if (!IsWindowFullscreen(hwnd))
                        {
                            return true;
                        }

                        // 清除旧缓存，允许重新处理
                        lock (_lock)
                        {
                            _processedWindows.Remove(hwnd);
                        }

                        // 执行转换
                        if (ConvertToFakeFullscreen(hwnd))
                        {
                            convertedCount++;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "Error processing window {Hwnd} during scan", hwnd);
                    }
                    
                    return true; // 继续枚举
                }, IntPtr.Zero);

                _logger.LogInformation("Scan completed. Converted {Count} RDP window(s) to fake fullscreen", convertedCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during RDP window scan");
            }
            finally
            {
                if (scanLockTaken)
                {
                    _scanGate.Release();
                }
            }

            return convertedCount;
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            DisableFakeFullscreen();

            try
            {
                _retryCts?.Cancel();
                _retryCts?.Dispose();
            }
            catch
            {
                // ignored
            }

            _scanGate.Dispose();
        }
    }
}
