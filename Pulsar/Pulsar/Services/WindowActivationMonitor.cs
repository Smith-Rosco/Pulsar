// [Path]: Pulsar/Pulsar/Services/WindowActivationMonitor.cs

using System;
using Microsoft.Extensions.Logging;
using Pulsar.Native;

namespace Pulsar.Services
{
    /// <summary>
    /// 全局窗口激活监听器 - 使用 Windows Hook 实时追踪窗口焦点变化和窗口显示事件
    /// 用于解决手动切换窗口后 Quick Switch 失效的问题，
    /// 以及附属窗口（如 360 PDF Viewer）首次显示时无法进入历史记录的问题
    /// </summary>
    public class WindowActivationMonitor : IDisposable
    {
        private readonly ILogger<WindowActivationMonitor>? _logger;
        private IntPtr _foregroundHookHandle;
        private IntPtr _objectShowHookHandle;
        private PulsarNative.WinEventDelegate? _hookDelegate;
        private bool _isRunning;
        private readonly object _lock = new object();

        /// <summary>
        /// 窗口激活事件 - 当任何窗口获得焦点时触发
        /// </summary>
        public event Action<IntPtr>? WindowActivated;

        /// <summary>
        /// 窗口显示事件 - 当任何顶层窗口变为可见时触发
        /// 用于捕获附属窗口（owned windows）首次显示的场景
        /// </summary>
        public event Action<IntPtr>? WindowShown;

        public WindowActivationMonitor(ILogger<WindowActivationMonitor>? logger = null)
        {
            _logger = logger;
        }

        /// <summary>
        /// 启动全局窗口事件监听（前台切换 + 窗口显示）
        /// </summary>
        public void Start()
        {
            lock (_lock)
            {
                if (_isRunning)
                {
                    _logger?.LogWarning("[WindowActivationMonitor] Already running, ignoring Start() call");
                    return;
                }

                try
                {
                    _hookDelegate = WinEventProc;

                    _foregroundHookHandle = PulsarNative.SetWinEventHook(
                        PulsarNative.EVENT_SYSTEM_FOREGROUND,
                        PulsarNative.EVENT_SYSTEM_FOREGROUND,
                        IntPtr.Zero,
                        _hookDelegate,
                        0,
                        0,
                        PulsarNative.WINEVENT_OUTOFCONTEXT);

                    if (_foregroundHookHandle == IntPtr.Zero)
                    {
                        int error = System.Runtime.InteropServices.Marshal.GetLastWin32Error();
                        _logger?.LogError("[WindowActivationMonitor] Failed to set foreground hook. Win32 Error: {Error}", error);
                    }
                    else
                    {
                        _logger?.LogInformation("[WindowActivationMonitor] ✅ Foreground hook registered. Handle: {Handle}", _foregroundHookHandle);
                    }

                    _objectShowHookHandle = PulsarNative.SetWinEventHook(
                        PulsarNative.EVENT_OBJECT_SHOW,
                        PulsarNative.EVENT_OBJECT_SHOW,
                        IntPtr.Zero,
                        _hookDelegate,
                        0,
                        0,
                        PulsarNative.WINEVENT_OUTOFCONTEXT);

                    if (_objectShowHookHandle == IntPtr.Zero)
                    {
                        int error = System.Runtime.InteropServices.Marshal.GetLastWin32Error();
                        _logger?.LogError("[WindowActivationMonitor] Failed to set object show hook. Win32 Error: {Error}", error);
                    }
                    else
                    {
                        _logger?.LogInformation("[WindowActivationMonitor] ✅ Object show hook registered. Handle: {Handle}", _objectShowHookHandle);
                    }

                    _isRunning = true;
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "[WindowActivationMonitor] Exception during start");
                }
            }
        }

        /// <summary>
        /// 停止全局窗口事件监听
        /// </summary>
        public void Stop()
        {
            lock (_lock)
            {
                if (!_isRunning)
                {
                    return;
                }

                try
                {
                    if (_foregroundHookHandle != IntPtr.Zero)
                    {
                        PulsarNative.UnhookWinEvent(_foregroundHookHandle);
                        _foregroundHookHandle = IntPtr.Zero;
                    }

                    if (_objectShowHookHandle != IntPtr.Zero)
                    {
                        PulsarNative.UnhookWinEvent(_objectShowHookHandle);
                        _objectShowHookHandle = IntPtr.Zero;
                    }

                    _isRunning = false;
                    _hookDelegate = null;
                    _logger?.LogInformation("[WindowActivationMonitor] Stopped successfully");
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "[WindowActivationMonitor] Error during stop");
                }
            }
        }

        /// <summary>
        /// Windows Hook 回调函数 - 处理前台切换和窗口显示事件
        /// </summary>
        private void WinEventProc(IntPtr hWinEventHook, uint eventType,
            IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
        {
            if (idObject != 0 || idChild != 0)
            {
                return;
            }

            if (hwnd == IntPtr.Zero)
            {
                return;
            }

            try
            {
                if (eventType == PulsarNative.EVENT_SYSTEM_FOREGROUND)
                {
                    _logger?.LogDebug("[WindowActivationMonitor] 🔔 EVENT_SYSTEM_FOREGROUND received. HWND: {Hwnd}, Thread: {Thread}",
                        hwnd, dwEventThread);
                    WindowActivated?.Invoke(hwnd);
                }
                else if (eventType == PulsarNative.EVENT_OBJECT_SHOW)
                {
                    _logger?.LogDebug("[WindowActivationMonitor] 👁 EVENT_OBJECT_SHOW received. HWND: {Hwnd}, Thread: {Thread}",
                        hwnd, dwEventThread);
                    WindowShown?.Invoke(hwnd);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "[WindowActivationMonitor] Error in event handler. EventType: {EventType}, HWND: {Hwnd}",
                    eventType, hwnd);
            }
        }

        public void Dispose()
        {
            Stop();
        }
    }
}