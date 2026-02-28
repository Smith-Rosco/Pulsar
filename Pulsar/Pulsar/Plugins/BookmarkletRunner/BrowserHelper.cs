// [Path]: Pulsar/Pulsar/Plugins/BookmarkletRunner/BrowserHelper.cs

using System;
using System.Diagnostics;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace Pulsar.Plugins.BookmarkletRunner
{
    /// <summary>
    /// 浏览器检测和识别工具类
    /// </summary>
    internal static class BrowserHelper
    {
        public static ILogger? Logger { get; set; }
        // 支持的浏览器进程名（按优先级排序）
        private static readonly string[] BrowserProcessNames = 
        { 
            "msedge",   // Microsoft Edge
            "chrome",   // Google Chrome
            "firefox",  // Mozilla Firefox
            "brave"     // Brave Browser
        };

        /// <summary>
        /// 检查进程名是否为浏览器
        /// </summary>
        /// <param name="processName">进程名（不区分大小写）</param>
        /// <returns>如果是已知的浏览器进程则返回 true</returns>
        public static bool IsBrowserProcess(string processName)
        {
            if (string.IsNullOrEmpty(processName))
                return false;

            var normalizedName = processName.ToLowerInvariant();
            return BrowserProcessNames.Contains(normalizedName);
        }

        /// <summary>
        /// 查找第一个可用的浏览器窗口句柄
        /// </summary>
        /// <returns>找到的浏览器主窗口句柄，未找到则返回 IntPtr.Zero</returns>
        public static IntPtr FindBrowserWindow()
        {
            foreach (var browserName in BrowserProcessNames)
            {
                try
                {
                    Process[] processes = Process.GetProcessesByName(browserName);
                    foreach (var process in processes)
                    {
                        // 只选择有主窗口句柄的进程（排除后台进程和子进程）
                        if (process.MainWindowHandle != IntPtr.Zero)
                        {
                            Logger?.LogDebug("[BrowserHelper] Found browser: {Browser} (PID: {Pid})", browserName, process.Id);
                            return process.MainWindowHandle;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger?.LogDebug(ex, "[BrowserHelper] Error checking {Browser}", browserName);
                }
            }

            Logger?.LogDebug("[BrowserHelper] No browser window found");
            return IntPtr.Zero;
        }

        /// <summary>
        /// 智能选择目标浏览器窗口
        /// 优先使用上下文窗口（如果是浏览器），否则查找第一个可用浏览器
        /// </summary>
        /// <param name="contextWindowHandle">上下文窗口句柄</param>
        /// <param name="contextProcessName">上下文进程名</param>
        /// <returns>目标浏览器窗口句柄</returns>
        public static IntPtr GetTargetBrowserWindow(IntPtr contextWindowHandle, string contextProcessName)
        {
            // 策略 1：如果上下文窗口是浏览器，直接使用
            if (contextWindowHandle != IntPtr.Zero && IsBrowserProcess(contextProcessName))
            {
                Logger?.LogDebug("[BrowserHelper] Using context browser: {ProcessName}", contextProcessName);
                return contextWindowHandle;
            }

            // 策略 2：回退到查找第一个可用浏览器
            Logger?.LogDebug("[BrowserHelper] Context is not a browser, searching for available browser...");
            return FindBrowserWindow();
        }
    }
}
