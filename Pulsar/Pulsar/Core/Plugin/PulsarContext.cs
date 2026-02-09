// [Path]: Pulsar/Pulsar/Core/Plugin/PulsarContext.cs

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Pulsar.Models;
using Pulsar.Services.Interfaces;

namespace Pulsar.Core.Plugin
{
    /// <summary>
    /// 不可变的上下文快照，在唤起轮盘瞬间冻结
    /// </summary>
    public readonly struct PulsarContext
    {
        // === 窗口信息 ===
        public IntPtr TargetWindowHandle { get; init; }
        public string TargetProcessName { get; init; }  // 大写，如 "EXCEL"
        public int TargetProcessId { get; init; }
        public IReadOnlyList<ProcessWindowInfo> TargetProcessWindows { get; init; }

        // === 用户输入 ===
        public string? SelectedText { get; init; }      // 预读取的选中文本
        public string? ClipboardText { get; init; }

        // === 共享存储 (用于插件间通信) ===
        public IReadOnlyDictionary<string, object>? SessionData { get; init; }

        /// <summary>
        /// 工厂方法 - 捕获当前上下文
        /// </summary>
        public static async Task<PulsarContext> CaptureAsync(IWindowService windowService)
        {
            var hwnd = windowService.GetPreviousWindow();
            
            // 获取进程名和进程ID
            string processName = string.Empty;
            int pid = 0;
            
            try
            {
                if (hwnd != IntPtr.Zero)
                {
                    uint processId;
                    NativeMethods.GetWindowThreadProcessId(hwnd, out processId);
                    pid = (int)processId;
                    
                    using (var process = Process.GetProcessById(pid))
                    {
                        processName = process.ProcessName.ToUpperInvariant();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PulsarContext] Failed to get process info: {ex.Message}");
            }

            // 获取该进程的所有窗口
            List<ProcessWindowInfo> processWindows = new List<ProcessWindowInfo>();
            if (pid > 0)
            {
                processWindows = await windowService.GetProcessWindowsAsync(pid);
            }

            // 获取剪贴板文本
            string? clipboardText = null;
            try
            {
                clipboardText = System.Windows.Clipboard.GetText();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PulsarContext] Failed to get clipboard text: {ex.Message}");
            }

            return new PulsarContext
            {
                TargetWindowHandle = hwnd,
                TargetProcessName = processName,
                TargetProcessId = pid,
                TargetProcessWindows = processWindows,
                ClipboardText = clipboardText,
                SelectedText = null // TODO: 实现异步预热读取选中文本
            };
        }
    }

    // 临时的 NativeMethods 引用 - 应该复用 WindowService 中的定义
    internal static class NativeMethods
    {
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        internal static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
    }
}
