// [Path]: Pulsar/Pulsar/Services/WindowActivationMonitor.cs

using System;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace Pulsar.Services
{
    /// <summary>
    /// 全局窗口激活监听器 - 使用 Windows Hook 实时追踪窗口焦点变化
    /// 用于解决手动切换窗口后 Quick Switch 失效的问题
    /// </summary>
    public class WindowActivationMonitor : IDisposable
    {
        private readonly ILogger<WindowActivationMonitor>? _logger;
        private IntPtr _hookHandle;
        private WinEventDelegate? _hookDelegate;
        private bool _isRunning;
        private readonly object _lock = new object();

        /// <summary>
        /// 窗口激活事件 - 当任何窗口获得焦点时触发
        /// </summary>
        public event Action<IntPtr>? WindowActivated;

        public WindowActivationMonitor(ILogger<WindowActivationMonitor>? logger = null)
        {
            _logger = logger;
        }

        /// <summary>
        /// 启动全局窗口激活监听
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
                    // 保持委托引用，防止被 GC 回收
                    _hookDelegate = WinEventProc;
                    
                    _logger?.LogInformation("[WindowActivationMonitor] Attempting to register WinEvent hook...");
                    
                    _hookHandle = SetWinEventHook(
                        EVENT_SYSTEM_FOREGROUND,
                        EVENT_SYSTEM_FOREGROUND,
                        IntPtr.Zero,
                        _hookDelegate,
                        0, // All processes
                        0, // All threads
                        WINEVENT_OUTOFCONTEXT);

                    if (_hookHandle == IntPtr.Zero)
                    {
                        int error = System.Runtime.InteropServices.Marshal.GetLastWin32Error();
                        _logger?.LogError("[WindowActivationMonitor] Failed to set WinEvent hook. Win32 Error: {Error}", error);
                        return;
                    }

                    _isRunning = true;
                    _logger?.LogInformation("[WindowActivationMonitor] ✅ Hook registered successfully. Handle: {Handle}", _hookHandle);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "[WindowActivationMonitor] Exception during start");
                }
            }
        }

        /// <summary>
        /// 停止全局窗口激活监听
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
                    if (_hookHandle != IntPtr.Zero)
                    {
                        UnhookWinEvent(_hookHandle);
                        _hookHandle = IntPtr.Zero;
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
        /// Windows Hook 回调函数
        /// </summary>
        private void WinEventProc(IntPtr hWinEventHook, uint eventType,
            IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
        {
            // 只处理窗口对象（排除菜单、滚动条等子对象）
            if (idObject != 0 || idChild != 0)
            {
                return;
            }

            if (eventType == EVENT_SYSTEM_FOREGROUND && hwnd != IntPtr.Zero)
            {
                try
                {
                    _logger?.LogDebug("[WindowActivationMonitor] 🔔 EVENT_SYSTEM_FOREGROUND received. HWND: {Hwnd}, Thread: {Thread}", 
                        hwnd, dwEventThread);
                    WindowActivated?.Invoke(hwnd);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "[WindowActivationMonitor] Error in WindowActivated event handler for HWND: {Hwnd}", hwnd);
                }
            }
        }

        public void Dispose()
        {
            Stop();
        }

        // ==========================================
        // Native Methods
        // ==========================================

        private delegate void WinEventDelegate(
            IntPtr hWinEventHook,
            uint eventType,
            IntPtr hwnd,
            int idObject,
            int idChild,
            uint dwEventThread,
            uint dwmsEventTime);

        [DllImport("user32.dll")]
        private static extern IntPtr SetWinEventHook(
            uint eventMin,
            uint eventMax,
            IntPtr hmodWinEventProc,
            WinEventDelegate lpfnWinEventProc,
            uint idProcess,
            uint idThread,
            uint dwFlags);

        [DllImport("user32.dll")]
        private static extern bool UnhookWinEvent(IntPtr hWinEventHook);

        // Event constants
        private const uint EVENT_SYSTEM_FOREGROUND = 0x0003;
        private const uint WINEVENT_OUTOFCONTEXT = 0x0000;
    }
}
