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
    internal sealed class WindowInventoryService : IWindowInventoryService
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

                var meta = new ProcessMetaResolver(capacity: 1).Resolve(targetProcessId);
                if (meta == null)
                {
                    return new List<ProcessWindowInfo>();
                }

                metaByPid[targetProcessId] = meta;

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
                var resolver = new ProcessMetaResolver(processes.Length);

                foreach (var proc in processes)
                {
                    try
                    {
                        var meta = resolver.Resolve(proc.Id);
                        if (meta == null)
                        {
                            continue;
                        }

                        pidSet.Add(proc.Id);
                        metaByPid[proc.Id] = meta;
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
                var resolver = new ProcessMetaResolver();

                PulsarNative.EnumWindows((hWnd, _) =>
                {
                    PulsarNative.GetWindowThreadProcessId(hWnd, out uint processId);
                    if (processId == 0) return true;

                    try
                    {
                        var classBuilder = new StringBuilder(256);
                        PulsarNative.GetClassName(hWnd, classBuilder, classBuilder.Capacity);

                        // 先用廉价结构判定筛掉绝大多数窗口，再解析进程元数据（见 EnumerateWindows 同款注释）。
                        var snapshot = BuildSnapshot(hWnd, classBuilder.ToString(), virtualScreen);
                        if (!_eligibilityPolicy.EvaluateStructural(snapshot).Included)
                        {
                            return true;
                        }

                        var meta = resolver.Resolve((int)processId);
                        if (meta == null) return true;

                        // [Deepen] 与快速切换共用同一"可切换窗口"判定（结构规则 + 物理可见性 + 类名黑名单 + 进程黑名单）。
                        if (!_eligibilityPolicy.EvaluateIdentity(
                                snapshot with { ProcessName = meta.ProcessName }, isBlacklisted).Included)
                        {
                            return true;
                        }

                        int length = PulsarNative.GetWindowTextLength(hWnd);
                        if (length == 0) return true;

                        StringBuilder sb = new(length + 1);
                        PulsarNative.GetWindowText(hWnd, sb, sb.Capacity);

                        string title = sb.ToString();
                        if (string.IsNullOrWhiteSpace(title) || title == "Program Manager") return true;

                        string fullPath = meta.ExePath;

                        if (!results.ContainsKey(meta.ProcessName))
                        {
                            results[meta.ProcessName] = new RunningProcessInfo
                            {
                                ProcessName = meta.ProcessName,
                                ExePath = fullPath
                            };
                        }
                        else if (string.IsNullOrEmpty(results[meta.ProcessName].ExePath) && !string.IsNullOrEmpty(fullPath))
                        {
                            results[meta.ProcessName].ExePath = fullPath;
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

                // Inspector 是诊断面：它要报告"每个窗口为何被纳入/排除"，因此不做提前返回，
                // 逐窗口解析进程名与标题。这里的成本不重要（人工触发、非热路径），
                // 但字段组装必须与枚举路径同源，否则诊断结论会与真实判定分叉。
                var resolver = new ProcessMetaResolver();

                PulsarNative.EnumWindows((hWnd, _) =>
                {
                    var classBuilder = new StringBuilder(256);
                    PulsarNative.GetClassName(hWnd, classBuilder, classBuilder.Capacity);

                    var snapshot = BuildSnapshot(hWnd, classBuilder.ToString(), virtualScreen);

                    string processName = resolver.Resolve((int)snapshot.Pid)?.ProcessName ?? string.Empty;
                    string title = WindowEligibilitySnapshot.ReadWindowText(hWnd);
                    string className = snapshot.ClassName;

                    snapshot = snapshot with { ProcessName = processName, Title = title };

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

            // 单次枚举内按 pid 记忆化进程元数据：同一进程的多窗口只解析一次。
            var resolver = new ProcessMetaResolver();

            PulsarNative.EnumWindows((hWnd, _) =>
            {
                PulsarNative.GetWindowThreadProcessId(hWnd, out uint processId);

                // pid 过滤是纯字典/集合查找，放在最前面：按进程枚举时它一次就排除掉
                // 桌面上绝大多数窗口，连类名都不必读。
                if (processIdFilter != null && !processIdFilter.Contains((int)processId)) return true;

                string className;
                var classBuilder = new StringBuilder(256);
                PulsarNative.GetClassName(hWnd, classBuilder, classBuilder.Capacity);
                className = classBuilder.ToString();

                // ============ 第一道筛：只用廉价 native 事实 ============
                // 不含进程名 —— 解析进程元数据（全系统进程快照 + 打开进程句柄）比这里
                // 所有 native 读取加起来还贵一个量级，而它会淘汰掉桌面上 90%+ 的顶层窗口。
                var snapshot = BuildSnapshot(hWnd, className, virtualScreen);
                if (!_eligibilityPolicy.EvaluateStructural(snapshot).Included)
                {
                    return true;
                }

                // ============ 第二道筛：只对幸存窗口解析进程元数据 ============
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
                    var meta = resolver.Resolve((int)processId);
                    if (meta == null)
                    {
                        // 进程已退出或无法解析。
                        return true;
                    }

                    processName = meta.ProcessName;
                    fullPath = meta.ExePath;
                }

                snapshot = snapshot with { ProcessName = processName };

                // [Deepen] 与快速切换共用同一"可切换窗口"判定。
                // 进程黑名单由调用方决定作用域（发现路径传入 isBlacklisted，显式激活路径传 null）。
                if (!_eligibilityPolicy.EvaluateIdentity(snapshot, isBlacklisted).Included)
                {
                    return true;
                }

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
                // 复用已有快照 —— 重新 BuildSnapshot 会把全部 native 读取再做一遍。
                if (_eligibilityPolicy.HasTitleDependentRules
                    && !_eligibilityPolicy.EvaluateIdentity(snapshot with { Title = title }, isBlacklisted).Included)
                {
                    return true;
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
        /// 从枚举路径已取得的事实组装结构判定快照。字段组装统一由
        /// <see cref="WindowEligibilitySnapshot.FromHwndStructural"/> 负责 —— 枚举路径只负责
        /// 把已读到的类名和循环外读取的虚拟屏幕矩形传进去，避免重复读取。
        /// 进程名与标题由调用方在结构筛通过后按需填充。
        /// </summary>
        private static WindowEligibilitySnapshot BuildSnapshot(
            IntPtr hwnd,
            string className,
            PulsarNative.RECT virtualScreen)
            => WindowEligibilitySnapshot.FromHwndStructural(hwnd, className, virtualScreen);
    }
}

