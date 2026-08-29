using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using Pulsar.Native;

namespace Pulsar.Services.WindowSwitching
{
    /// <summary>
    /// 进程级元数据（进程名 + 可执行文件路径）。按 pid 解析一次，供同一次枚举内的所有窗口复用。
    /// </summary>
    internal sealed class ProcessMeta
    {
        public string ProcessName { get; init; } = string.Empty;

        public string ExePath { get; init; } = string.Empty;
    }

    /// <summary>
    /// 按 pid 解析进程元数据，并在单次枚举内按 pid 记忆化。
    /// <para>
    /// 两个关键取舍：
    /// 1. 走 <c>OpenProcess</c> + <c>QueryFullProcessImageName</c> 而非 <c>Process.MainModule</c>。
    ///    <c>MainModule</c> 会枚举目标进程的整个模块表，并在跨位数（32/64）或受保护进程上抛异常；
    ///    <c>QueryFullProcessImageName</c> 是一次定向调用，不走模块表、不抛异常。
    /// 2. 记忆化只在一次枚举内有效（本类实例即枚举的生命周期）。pid 会被系统复用，
    ///    跨枚举缓存需要额外的进程创建时间校验才安全 —— 一次枚举的时间窗内 pid 复用可忽略。
    ///    同一进程的多窗口（浏览器、编辑器）是常见情形，这层记忆化正是为它们准备的。
    /// </para>
    /// </summary>
    internal sealed class ProcessMetaResolver
    {
        private readonly Dictionary<int, ProcessMeta?> _cache;

        public ProcessMetaResolver(int capacity = 32)
        {
            _cache = new Dictionary<int, ProcessMeta?>(capacity);
        }

        /// <summary>
        /// 解析 pid 的元数据；进程已退出或无法解析时返回 null（调用方应跳过该窗口）。
        /// 同一 pid 的重复请求由记忆化直接命中，包括解析失败的负结果。
        /// </summary>
        public ProcessMeta? Resolve(int pid)
        {
            if (pid == 0)
            {
                return null;
            }

            if (_cache.TryGetValue(pid, out var cached))
            {
                return cached;
            }

            var meta = ResolveUncached(pid);
            _cache[pid] = meta;
            return meta;
        }

        private static ProcessMeta? ResolveUncached(int pid)
        {
            // 首选路径：一次 OpenProcess + QueryFullProcessImageName 同时得到进程名与完整路径。
            string exePath = TryGetImagePath(pid);
            if (exePath.Length > 0)
            {
                return new ProcessMeta
                {
                    ProcessName = Path.GetFileNameWithoutExtension(exePath),
                    ExePath = exePath
                };
            }

            // 回退路径：句柄打不开（受保护进程 / 权限不足）时仍尝试取进程名。
            // 路径留空 —— 调用方对空路径已有处理（跳过图标提取）。
            try
            {
                using var process = Process.GetProcessById(pid);
                return new ProcessMeta
                {
                    ProcessName = process.ProcessName,
                    ExePath = string.Empty
                };
            }
            catch
            {
                // 进程在枚举期间退出。
                return null;
            }
        }

        /// <summary>MAX_PATH 足够覆盖绝大多数进程；启用长路径的系统上回退到扩展长度上限。</summary>
        private const int ShortPathCapacity = 260;

        private const int ExtendedPathCapacity = 32768;

        private static string TryGetImagePath(int pid)
        {
            IntPtr handle = PulsarNative.OpenProcess(
                PulsarNative.PROCESS_QUERY_LIMITED_INFORMATION, false, pid);

            if (handle == IntPtr.Zero)
            {
                return string.Empty;
            }

            try
            {
                // 先用小缓冲（一次分配即命中绝大多数进程），仅在确实不够时才重试大缓冲，
                // 避免为每个进程都分配 32KB。
                string path = QueryImagePath(handle, ShortPathCapacity);
                return path.Length > 0 ? path : QueryImagePath(handle, ExtendedPathCapacity);
            }
            catch
            {
                return string.Empty;
            }
            finally
            {
                PulsarNative.CloseHandle(handle);
            }
        }

        private static string QueryImagePath(IntPtr handle, int capacity)
        {
            int size = capacity;
            var buffer = new StringBuilder(capacity);

            // StringBuilder 的封送已按空终止符设定 Length，直接 ToString 即为完整路径。
            return PulsarNative.QueryFullProcessImageName(handle, 0, buffer, ref size)
                ? buffer.ToString()
                : string.Empty;
        }
    }
}
