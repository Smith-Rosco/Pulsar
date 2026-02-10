using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Pulsar.Models;
using Pulsar.Services.Interfaces;

namespace Pulsar.Core.Plugin
{
    /// <summary>
    /// 上下文服务 - 按需异步加载环境信息
    /// </summary>
    public class PulsarContext
    {
        // === 基础信息 (轻量，同步获取) ===
        public IntPtr TargetWindowHandle { get; }
        public string TargetProcessName { get; }  // 大写，如 "EXCEL"
        public int TargetProcessId { get; }
        
        // === 共享存储 (用于插件间通信) ===
        public IReadOnlyDictionary<string, object>? SessionData { get; init; }

        // === 延迟加载任务 ===
        private readonly Lazy<Task<IReadOnlyList<ProcessWindowInfo>>> _windowsLazy;
        private readonly Lazy<Task<string?>> _clipboardLazy;
        private readonly Lazy<Task<string?>> _selectedTextLazy;

        // 私有构造函数
        private PulsarContext(
            IntPtr hwnd, 
            string processName, 
            int pid,
            Func<Task<IReadOnlyList<ProcessWindowInfo>>> windowsFactory,
            Func<Task<string?>> clipboardFactory,
            Func<Task<string?>> selectionFactory)
        {
            TargetWindowHandle = hwnd;
            TargetProcessName = processName;
            TargetProcessId = pid;
            
            _windowsLazy = new Lazy<Task<IReadOnlyList<ProcessWindowInfo>>>(windowsFactory);
            _clipboardLazy = new Lazy<Task<string?>>(clipboardFactory);
            _selectedTextLazy = new Lazy<Task<string?>>(selectionFactory);
        }

        // === 异步访问接口 ===
        
        /// <summary>
        /// 获取目标进程的所有窗口（延迟加载）
        /// </summary>
        public Task<IReadOnlyList<ProcessWindowInfo>> GetTargetProcessWindowsAsync() => _windowsLazy.Value;

        /// <summary>
        /// 获取剪贴板文本（延迟加载）
        /// </summary>
        public Task<string?> GetClipboardTextAsync() => _clipboardLazy.Value;

        /// <summary>
        /// 获取选中的文本（延迟加载，暂未实现）
        /// </summary>
        public Task<string?> GetSelectedTextAsync() => _selectedTextLazy.Value;

        /// <summary>
        /// 捕获当前上下文 (轻量级，非阻塞)
        /// </summary>
        public static PulsarContext Capture(IWindowService windowService)
        {
            var hwnd = windowService.GetPreviousWindow();
            string processName = string.Empty;
            int pid = 0;
            
            try
            {
                if (hwnd != IntPtr.Zero)
                {
                    uint processId;
                    NativeMethods.GetWindowThreadProcessId(hwnd, out processId);
                    pid = (int)processId;
                    
                    // 仅获取进程名，不做其他重型操作
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

            // 定义 Lazy 工厂
            
            // 1. 窗口列表工厂
            var windowsFactory = async () => 
            {
                if (pid <= 0) return (IReadOnlyList<ProcessWindowInfo>)new List<ProcessWindowInfo>();
                return await windowService.GetProcessWindowsAsync(pid);
            };

            // 2. 剪贴板工厂 (需调度到 UI 线程)
            var clipboardFactory = async () =>
            {
                try 
                {
                    // 使用 Application.Current.Dispatcher 确保在 UI 线程访问剪贴板
                    if (System.Windows.Application.Current == null) return null;
                    
                    return await System.Windows.Application.Current.Dispatcher.InvokeAsync(() => 
                    {
                        try { return System.Windows.Clipboard.GetText(); }
                        catch { return null; }
                    });
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[PulsarContext] Failed to get clipboard: {ex.Message}");
                    return null;
                }
            };
            
            // 3. 选中文本工厂 (占位)
            var selectionFactory = () => Task.FromResult<string?>(null);

            return new PulsarContext(hwnd, processName, pid, windowsFactory, clipboardFactory, selectionFactory);
        }
    }

    internal static class NativeMethods
    {
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        internal static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
    }
}
