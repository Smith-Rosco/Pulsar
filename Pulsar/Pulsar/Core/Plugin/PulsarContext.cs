using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using System.Threading;
using Pulsar.Models;
using Pulsar.Services.Interfaces;
using Pulsar.Native;

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
        public string TargetExePath => _resolvedExePath ?? string.Empty;
        
        /// <summary>
        /// 显示用进程名 - 首字母大写格式 (如 "Excel")
        /// 用于 UI 显示，提升用户体验
        /// </summary>
        [System.Text.Json.Serialization.JsonIgnore]
        public string DisplayProcessName => Pulsar.Helpers.ProcessNameFormatter.ToDisplayName(TargetProcessName);
        
        // === 共享存储 (用于插件间通信) ===
        public IReadOnlyDictionary<string, object>? SessionData { get; init; }

        // === 延迟加载任务 ===
        private readonly Lazy<Task<IReadOnlyList<ProcessWindowInfo>>> _windowsLazy;
        private readonly Lazy<Task<string?>> _clipboardLazy;
        private readonly Lazy<Task<string>> _targetExePathLazy;
        private string? _resolvedExePath;

        // 私有构造函数
        private PulsarContext(
            IntPtr hwnd, 
            string processName,
            int pid,
            Func<Task<string>> exePathFactory,
            Func<Task<IReadOnlyList<ProcessWindowInfo>>> windowsFactory,
            Func<Task<string?>> clipboardFactory)
        {
            TargetWindowHandle = hwnd;
            TargetProcessName = processName;
            TargetProcessId = pid;
            
            _targetExePathLazy = new Lazy<Task<string>>(async () =>
            {
                _resolvedExePath = await exePathFactory();
                return _resolvedExePath;
            }, LazyThreadSafetyMode.ExecutionAndPublication);
            _windowsLazy = new Lazy<Task<IReadOnlyList<ProcessWindowInfo>>>(windowsFactory);
            _clipboardLazy = new Lazy<Task<string?>>(clipboardFactory);
        }

        // === 异步访问接口 ===
        
        /// <summary>
        /// 获取目标进程的所有窗口（延迟加载）
        /// 需要权限: ReadProcessWindows
        /// </summary>
        public Task<IReadOnlyList<ProcessWindowInfo>> GetTargetProcessWindowsAsync()
        {
            return _windowsLazy.Value;
        }

        /// <summary>
        /// 获取剪贴板文本（延迟加载）
        /// 需要权限: ReadClipboard
        /// </summary>
        public Task<string?> GetClipboardTextAsync()
        {
            return _clipboardLazy.Value;
        }

        public Task<string> GetTargetExePathAsync()
        {
            return _targetExePathLazy.Value;
        }

        /// <summary>
        /// 捕获当前上下文 (轻量级，非阻塞)
        /// </summary>
        /// <param name="windowService">窗口服务</param>
        /// <param name="logger">日志记录器</param>
        /// <returns>上下文实例</returns>
        public static PulsarContext Capture(IWindowService windowService, ILogger? logger = null)
        {
            var hwnd = windowService.GetPreviousWindow();
            string processName = string.Empty;
            int pid = 0;
            
            try
            {
                if (hwnd != IntPtr.Zero)
                {
                    uint processId;
                    PulsarNative.GetWindowThreadProcessId(hwnd, out processId);
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
                logger?.LogWarning(ex, "[PulsarContext] Failed to get process info");
            }

            // 定义 Lazy 工厂

            var exePathFactory = new Func<Task<string>>(async () =>
            {
                if (pid <= 0)
                {
                    return string.Empty;
                }

                try
                {
                    return await Task.Run(() =>
                    {
                        using var process = Process.GetProcessById(pid);
                        return process.MainModule?.FileName ?? string.Empty;
                    });
                }
                catch (Exception ex)
                {
                    logger?.LogWarning(ex, "[PulsarContext] Failed to resolve target executable path");
                    return string.Empty;
                }
            });
            
            // 1. 窗口列表工厂
            var windowsFactory = new Func<Task<IReadOnlyList<ProcessWindowInfo>>>(async () => 
            {
                if (pid <= 0) return new List<ProcessWindowInfo>();
                return await windowService.GetProcessWindowsAsync(pid);
            });

            // 2. 剪贴板工厂 (需调度到 UI 线程)
            var clipboardFactory = new Func<Task<string?>>(async () =>
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
                    logger?.LogWarning(ex, "[PulsarContext] Failed to get clipboard");
                    return null;
                }
            });
            
            var context = new PulsarContext(hwnd, processName, pid, exePathFactory, windowsFactory, clipboardFactory);
            return context;
        }
    }
}
