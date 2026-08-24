using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using Pulsar.Models;
using Pulsar.Native;
using Pulsar.Services.Interfaces;

namespace Pulsar.Services.WindowSwitching
{
    /// <summary>
    /// 进程级元数据，按进程预取一次，避免每个窗口重复 GetProcessById / MainModule。
    /// </summary>
    internal sealed class ProcessMeta
    {
        public string ProcessName { get; init; } = string.Empty;

        public string ExePath { get; init; } = string.Empty;
    }

    internal sealed class WindowInventoryService
    {
        public Task<List<ProcessWindowInfo>> GetActiveWindowsAsync(
            Func<string, bool> isBlacklisted,
            Func<IntPtr, WindowTrackingSnapshot> snapshotWindow,
            Func<string, ImageSource?> extractIcon,
            IProcessRegistryService? processRegistryService)
        {
            return Task.Run(() =>
            {
                List<ProcessWindowInfo> results = EnumerateWindows(
                    processIdFilter: null,
                    metaByPid: null,
                    isBlacklisted,
                    snapshotWindow,
                    extractIcon);

                return results;
            });
        }

        /// <summary>
        /// 按单个进程 ID 枚举窗口。进程元数据只读取一次，窗口匹配走单一枚举通道。
        /// </summary>
        public Task<List<ProcessWindowInfo>> GetProcessWindowsAsync(
            int targetProcessId,
            Func<string, bool> isBlacklisted,
            Func<IntPtr, WindowTrackingSnapshot> snapshotWindow,
            Func<string, ImageSource?> extractIcon)
        {
            return Task.Run(() =>
            {
                var pidSet = new HashSet<int> { targetProcessId };
                var metaByPid = new Dictionary<int, ProcessMeta>();

                try
                {
                    using var proc = Process.GetProcessById(targetProcessId);
                    metaByPid[targetProcessId] = new ProcessMeta
                    {
                        ProcessName = proc.ProcessName,
                        ExePath = SafeMainModule(proc)
                    };
                }
                catch
                {
                    return new List<ProcessWindowInfo>();
                }

                return EnumerateWindows(pidSet, metaByPid, isBlacklisted, snapshotWindow, extractIcon);
            });
        }

        /// <summary>
        /// 按进程名枚举窗口（覆盖该进程的整个进程树）。单次枚举、进程元数据按进程预取一次，
        /// 避免 SwitchToProcessAsync 对每个进程做一次全桌面枚举（O(P×W) → O(W)）。
        /// </summary>
        public Task<List<ProcessWindowInfo>> GetProcessWindowsAsync(
            string processName,
            Func<string, bool> isBlacklisted,
            Func<IntPtr, WindowTrackingSnapshot> snapshotWindow,
            Func<string, ImageSource?> extractIcon)
        {
            return Task.Run(() =>
            {
                string target = processName?.Trim() ?? string.Empty;
                if (target.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                {
                    target = target[..^4];
                }

                if (target.Length == 0)
                {
                    return new List<ProcessWindowInfo>();
                }

                Process[] processes;
                try
                {
                    processes = Process.GetProcessesByName(target);
                }
                catch
                {
                    return new List<ProcessWindowInfo>();
                }

                if (processes.Length == 0)
                {
                    return new List<ProcessWindowInfo>();
                }

                var pidSet = new HashSet<int>(processes.Length);
                var metaByPid = new Dictionary<int, ProcessMeta>(processes.Length);

                foreach (var proc in processes)
                {
                    try
                    {
                        pidSet.Add(proc.Id);
                        metaByPid[proc.Id] = new ProcessMeta
                        {
                            ProcessName = proc.ProcessName,
                            ExePath = SafeMainModule(proc)
                        };
                    }
                    catch
                    {
                        // 进程可能在快照与读取之间退出；跳过该实例。
                    }
                    finally
                    {
                        proc.Dispose();
                    }
                }

                if (pidSet.Count == 0)
                {
                    return new List<ProcessWindowInfo>();
                }

                return EnumerateWindows(pidSet, metaByPid, isBlacklisted, snapshotWindow, extractIcon);
            });
        }

        public Task<HashSet<string>> GetRunningProcessNamesAsync(Func<string, bool> isBlacklisted)
        {
            return Task.Run(async () =>
            {
                var processes = await GetRunningProcessesAsync(isBlacklisted);
                return processes
                    .Select(process => process.ProcessName)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
            });
        }

        public Task<List<RunningProcessInfo>> GetRunningProcessesAsync(Func<string, bool> isBlacklisted)
        {
            return Task.Run(() =>
            {
                Dictionary<string, RunningProcessInfo> results = new(StringComparer.OrdinalIgnoreCase);

                PulsarNative.EnumWindows((hWnd, _) =>
                {
                    if (!PulsarNative.IsWindowVisible(hWnd)) return true;

                    if (PulsarNative.DwmGetWindowAttribute(hWnd, PulsarNative.DWMWA_CLOAKED, out int isCloakedVal, sizeof(int)) == 0 && isCloakedVal != 0)
                    {
                        return true;
                    }

                    long exStyle = PulsarNative.GetWindowLong(hWnd, PulsarNative.GWL_EXSTYLE);
                    if ((exStyle & PulsarNative.WS_EX_TOOLWINDOW) != 0) return true;

                    IntPtr owner = PulsarNative.GetWindow(hWnd, PulsarNative.GW_OWNER);
                    if (owner != IntPtr.Zero && (exStyle & PulsarNative.WS_EX_APPWINDOW) == 0) return true;

                    int length = PulsarNative.GetWindowTextLength(hWnd);
                    if (length == 0) return true;

                    StringBuilder sb = new(length + 1);
                    PulsarNative.GetWindowText(hWnd, sb, sb.Capacity);

                    string title = sb.ToString();
                    if (string.IsNullOrWhiteSpace(title) || title == "Program Manager") return true;

                    PulsarNative.GetWindowThreadProcessId(hWnd, out uint processId);

                    try
                    {
                        using Process proc = Process.GetProcessById((int)processId);
                        if (proc.HasExited) return true;
                        if (isBlacklisted(proc.ProcessName)) return true;

                        string fullPath = SafeMainModule(proc);

                        if (!results.ContainsKey(proc.ProcessName))
                        {
                            results[proc.ProcessName] = new RunningProcessInfo
                            {
                                ProcessName = proc.ProcessName,
                                ExePath = fullPath
                            };
                        }
                        else if (string.IsNullOrEmpty(results[proc.ProcessName].ExePath) && !string.IsNullOrEmpty(fullPath))
                        {
                            results[proc.ProcessName].ExePath = fullPath;
                        }
                    }
                    catch
                    {
                    }

                    return true;
                }, IntPtr.Zero);

                return results.Values.ToList();
            });
        }

        private static string SafeMainModule(Process proc)
        {
            try { return proc.MainModule?.FileName ?? string.Empty; } catch { return string.Empty; }
        }

        private static List<ProcessWindowInfo> EnumerateWindows(
            IReadOnlyCollection<int>? processIdFilter,
            IReadOnlyDictionary<int, ProcessMeta>? metaByPid,
            Func<string, bool> isBlacklisted,
            Func<IntPtr, WindowTrackingSnapshot> snapshotWindow,
            Func<string, ImageSource?> extractIcon)
        {
            List<ProcessWindowInfo> results = new();
            int zOrderIndex = 0;

            PulsarNative.EnumWindows((hWnd, _) =>
            {
                if (!PulsarNative.IsWindowVisible(hWnd)) return true;

                if (PulsarNative.DwmGetWindowAttribute(hWnd, PulsarNative.DWMWA_CLOAKED, out int isCloakedVal, sizeof(int)) == 0 && isCloakedVal != 0)
                {
                    return true;
                }

                long exStyle = PulsarNative.GetWindowLong(hWnd, PulsarNative.GWL_EXSTYLE);
                if ((exStyle & PulsarNative.WS_EX_TOOLWINDOW) != 0) return true;

                IntPtr owner = PulsarNative.GetWindow(hWnd, PulsarNative.GW_OWNER);
                if (owner != IntPtr.Zero && (exStyle & PulsarNative.WS_EX_APPWINDOW) == 0) return true;

                PulsarNative.GetWindowThreadProcessId(hWnd, out uint processId);
                if (processIdFilter != null && !processIdFilter.Contains((int)processId)) return true;

                int length = PulsarNative.GetWindowTextLength(hWnd);
                if (processIdFilter == null && length == 0) return true;

                StringBuilder sb = new(length + 1);
                if (length > 0)
                {
                    PulsarNative.GetWindowText(hWnd, sb, sb.Capacity);
                }

                string title = sb.ToString();
                if (processIdFilter == null && (string.IsNullOrWhiteSpace(title) || title == "Program Manager")) return true;

                string processName;
                string fullPath;

                if (metaByPid != null)
                {
                    // 进程元数据已预取；进程若在快照后退出，则跳过。
                    if (!metaByPid.TryGetValue((int)processId, out var meta))
                    {
                        return true;
                    }

                    processName = meta.ProcessName;
                    fullPath = meta.ExePath;
                }
                else
                {
                    try
                    {
                        using Process proc = Process.GetProcessById((int)processId);
                        if (proc.HasExited) return true;
                        if (isBlacklisted(proc.ProcessName)) return true;

                        processName = proc.ProcessName;
                        fullPath = SafeMainModule(proc);
                    }
                    catch
                    {
                        return true;
                    }
                }

                ImageSource? iconSource = string.IsNullOrEmpty(fullPath) ? null : extractIcon(fullPath);

                DateTime zOrderActivationTime = DateTime.Now.AddSeconds(-zOrderIndex);
                WindowTrackingSnapshot tracking = snapshotWindow(hWnd);

                results.Add(new ProcessWindowInfo
                {
                    Title = string.IsNullOrEmpty(title) ? "Window" : title,
                    ProcessName = processName,
                    ExePath = fullPath,
                    Handle = hWnd,
                    AppIcon = iconSource,
                    LastActivationTime = zOrderActivationTime,
                    FirstSeenTime = tracking.FirstSeenTime,
                    RealActivationTime = tracking.ActivationTime
                });

                zOrderIndex++;
                return true;
            }, IntPtr.Zero);

            return results;
        }
    }
}
