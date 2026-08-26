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
        private readonly IWindowEligibilityPolicy _eligibilityPolicy;

        public WindowInventoryService(IWindowEligibilityPolicy eligibilityPolicy)
        {
            _eligibilityPolicy = eligibilityPolicy;
        }

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
            Func<string, bool>? isBlacklisted,
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
            Func<string, bool>? isBlacklisted,
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

                var virtualScreen = WindowEligibilitySnapshot.GetVirtualScreenRect();

                PulsarNative.EnumWindows((hWnd, _) =>
                {
                    PulsarNative.GetWindowThreadProcessId(hWnd, out uint processId);
                    if (processId == 0) return true;

                    try
                    {
                        using Process proc = Process.GetProcessById((int)processId);
                        if (proc.HasExited) return true;

                        var classBuilder = new StringBuilder(256);
                        PulsarNative.GetClassName(hWnd, classBuilder, classBuilder.Capacity);

                        // [Deepen] 与快速切换共用同一"可切换窗口"判定（结构规则 + 物理可见性 + 类名黑名单 + 进程黑名单）。
                        if (!_eligibilityPolicy.Evaluate(
                                BuildSnapshot(hWnd, processId, proc.ProcessName, classBuilder.ToString(), virtualScreen),
                                isBlacklisted).Included)
                        {
                            return true;
                        }

                        int length = PulsarNative.GetWindowTextLength(hWnd);
                        if (length == 0) return true;

                        StringBuilder sb = new(length + 1);
                        PulsarNative.GetWindowText(hWnd, sb, sb.Capacity);

                        string title = sb.ToString();
                        if (string.IsNullOrWhiteSpace(title) || title == "Program Manager") return true;

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

        /// <summary>
        /// 枚举全部顶层窗口并给出每窗口的判定报告（含标题 / 类名 / 矩形 / 原因），
        /// 供 Window Inspector 诊断。进程黑名单不参与；标题为诊断需要所以总是读取。
        /// </summary>
        public Task<IReadOnlyList<WindowEligibilityReport>> GetEligibilityReportAsync()
        {
            return Task.Run(() =>
            {
                List<WindowEligibilityReport> results = new();
                var virtualScreen = WindowEligibilitySnapshot.GetVirtualScreenRect();

                PulsarNative.EnumWindows((hWnd, _) =>
                {
                    PulsarNative.GetWindowThreadProcessId(hWnd, out uint processId);

                    string className;
                    var classBuilder = new StringBuilder(256);
                    PulsarNative.GetClassName(hWnd, classBuilder, classBuilder.Capacity);
                    className = classBuilder.ToString();

                    string processName = string.Empty;
                    if (processId != 0)
                    {
                        try
                        {
                            using Process proc = Process.GetProcessById((int)processId);
                            processName = proc.ProcessName;
                        }
                        catch
                        {
                        }
                    }

                    string title = WindowEligibilitySnapshot.ReadWindowText(hWnd);

                    var snapshot = new WindowEligibilitySnapshot
                    {
                        Hwnd = hWnd,
                        Pid = processId,
                        ProcessName = processName,
                        ClassName = className,
                        Title = title,
                        IsIconic = PulsarNative.IsIconic(hWnd),
                        IsVisible = PulsarNative.IsWindowVisible(hWnd),
                        IsCloaked = WindowEligibilitySnapshot.IsDwmCloaked(hWnd),
                        ExStyle = PulsarNative.GetWindowLong(hWnd, PulsarNative.GWL_EXSTYLE),
                        OwnerHwnd = PulsarNative.GetWindow(hWnd, PulsarNative.GW_OWNER),
                        Rect = WindowEligibilitySnapshot.TryGetWindowRect(hWnd),
                        VirtualScreenRect = virtualScreen
                    };

                    var verdict = _eligibilityPolicy.Evaluate(snapshot, processBlacklist: null);
                    results.Add(new WindowEligibilityReport(
                        hWnd,
                        title,
                        processName,
                        className,
                        snapshot.Rect,
                        verdict.Included,
                        verdict.Verdict));

                    return true;
                }, IntPtr.Zero);

                return (IReadOnlyList<WindowEligibilityReport>)results;
            });
        }

        private List<ProcessWindowInfo> EnumerateWindows(
            IReadOnlyCollection<int>? processIdFilter,
            IReadOnlyDictionary<int, ProcessMeta>? metaByPid,
            Func<string, bool>? isBlacklisted,
            Func<IntPtr, WindowTrackingSnapshot> snapshotWindow,
            Func<string, ImageSource?> extractIcon)
        {
            List<ProcessWindowInfo> results = new();
            int zOrderIndex = 0;
            var virtualScreen = WindowEligibilitySnapshot.GetVirtualScreenRect();

            PulsarNative.EnumWindows((hWnd, _) =>
            {
                PulsarNative.GetWindowThreadProcessId(hWnd, out uint processId);

                string className;
                var classBuilder = new StringBuilder(256);
                PulsarNative.GetClassName(hWnd, classBuilder, classBuilder.Capacity);
                className = classBuilder.ToString();

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
                    if (processId == 0)
                    {
                        return true;
                    }

                    try
                    {
                        using Process proc = Process.GetProcessById((int)processId);
                        if (proc.HasExited) return true;

                        processName = proc.ProcessName;
                        fullPath = SafeMainModule(proc);
                    }
                    catch
                    {
                        return true;
                    }
                }

                // [Deepen] 与快速切换共用同一"可切换窗口"判定。
                // 类名黑名单与物理可见性对全部消费面生效；进程黑名单由调用方决定作用域
                // （发现路径传入 isBlacklisted，显式激活路径传 null）。
                if (!_eligibilityPolicy.Evaluate(
                        BuildSnapshot(hWnd, processId, processName, className, virtualScreen),
                        isBlacklisted).Included)
                {
                    return true;
                }

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

                // [Deepen] 标题依赖规则（TitlePattern）：仅在存在此类规则时二次判定，
                // 使发现路径的标题规则生效，同时保持无标题规则时零额外读取（首次判定快照不含标题）。
                if (_eligibilityPolicy.HasTitleDependentRules)
                {
                    var titledSnapshot = BuildSnapshot(hWnd, processId, processName, className, virtualScreen) with { Title = title };
                    if (!_eligibilityPolicy.Evaluate(titledSnapshot, isBlacklisted).Included)
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

        /// <summary>
        /// 从枚举路径已取得的事实组装判定快照（避免像 FromHwnd 那样逐窗口重解析进程名/重读标题）。
        /// 标题由调用方在判定通过后按需读取，这里不填（GetWindowText 是阻塞调用）。
        /// </summary>
        private static WindowEligibilitySnapshot BuildSnapshot(
            IntPtr hwnd,
            uint pid,
            string processName,
            string className,
            PulsarNative.RECT virtualScreen)
        {
            return new WindowEligibilitySnapshot
            {
                Hwnd = hwnd,
                Pid = pid,
                ProcessName = processName,
                ClassName = className,
                IsIconic = PulsarNative.IsIconic(hwnd),
                IsVisible = PulsarNative.IsWindowVisible(hwnd),
                IsCloaked = WindowEligibilitySnapshot.IsDwmCloaked(hwnd),
                ExStyle = PulsarNative.GetWindowLong(hwnd, PulsarNative.GWL_EXSTYLE),
                OwnerHwnd = PulsarNative.GetWindow(hwnd, PulsarNative.GW_OWNER),
                Rect = WindowEligibilitySnapshot.TryGetWindowRect(hwnd),
                VirtualScreenRect = virtualScreen
            };
        }
    }
}

