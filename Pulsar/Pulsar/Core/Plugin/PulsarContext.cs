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
        private readonly Lazy<Task<string>> _targetExePathLazy;
        private string? _resolvedExePath;

        // 私有构造函数
        private PulsarContext(
            IntPtr hwnd, 
            string processName,
            int pid,
            Func<Task<string>> exePathFactory)
        {
            TargetWindowHandle = hwnd;
            TargetProcessName = processName;
            TargetProcessId = pid;
            
            _targetExePathLazy = new Lazy<Task<string>>(async () =>
            {
                _resolvedExePath = await exePathFactory();
                return _resolvedExePath;
            }, LazyThreadSafetyMode.ExecutionAndPublication);
        }

        // === 异步访问接口 ===
        
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
            
            var context = new PulsarContext(hwnd, processName, pid, exePathFactory);
            return context;
        }
    }
}
