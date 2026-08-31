using System;
using System.Collections.Generic;

namespace Pulsar.Services.WindowSwitching
{
    /// <summary>
    /// "可切换窗口"判定的单一实现：组合 <see cref="IWindowEligibilityPolicy"/>、持有进程黑名单状态，
    /// 并负责从 HWND 组装快照（<see cref="WindowEligibilitySnapshot.FromHwnd"/>，含基于
    /// <see cref="IWindowEligibilityPolicy.HasTitleDependentRules"/> 的条件标题读取）。
    /// <para>
    /// 收拢了此前散落在 WindowService 五个调用点的 "FromHwnd + Evaluate" 序列，使快照组装、
    /// 黑名单谓词与判定作用域只有一份实现。生产默认由 DI 注入；测试用 Moq 模拟接口即可。
    /// </para>
    /// </summary>
    public sealed class WindowEligibilityEvaluator : IWindowEligibilityEvaluator
    {
        private readonly IWindowEligibilityPolicy _policy;

        // [New] Dynamic blacklist - can be updated by plugins. Owned here so both
        // the discovery enumeration and every Evaluate(Discovery) share one predicate.
        private HashSet<string> _dynamicBlacklist;
        private readonly object _blacklistLock = new object();

        // System blacklist for known problematic processes (always excluded)
        private static readonly HashSet<string> _systemBlacklist = new(StringComparer.OrdinalIgnoreCase)
        {
            "applicationframehost", // UWP shell
            "systemsettings",       // Settings (when suspended)
            "searchapp",            // Search
            "textinputhost",        // Input Method / Emoji Panel
            "shellexperiencehost",  // Start Menu etc.
            "lockapp",              // Lock Screen
            "video.ui",             // Xbox Game Bar / Video Overlay
            "gamebar",              // Game Bar
            "yourphone",            // Phone Link background
            "calc"                  // Calculator often stays suspended
        };

        public WindowEligibilityEvaluator(IWindowEligibilityPolicy policy)
        {
            _policy = policy;

            // Initialize with default system blacklist
            lock (_blacklistLock)
            {
                _dynamicBlacklist = new HashSet<string>(_systemBlacklist, StringComparer.OrdinalIgnoreCase);
            }
        }

        public IReadOnlyList<WindowEligibilityRule> Rules => _policy.Rules;

        public bool HasTitleDependentRules => _policy.HasTitleDependentRules;

        public void UpdateRules(IReadOnlyList<WindowEligibilityRule>? rules)
            => _policy.UpdateRules(rules);

        public void UpdateBlacklist(IEnumerable<string> userBlacklist)
        {
            lock (_blacklistLock)
            {
                var merged = new HashSet<string>(_systemBlacklist, StringComparer.OrdinalIgnoreCase);
                foreach (var process in userBlacklist)
                {
                    if (!string.IsNullOrWhiteSpace(process))
                    {
                        merged.Add(process.Trim());
                    }
                }

                _dynamicBlacklist = merged;
            }
        }

        public bool IsDiscoveryBlacklisted(string processName)
        {
            lock (_blacklistLock)
            {
                return IsProcessNameBlacklisted(processName, _dynamicBlacklist);
            }
        }

        public EligibilityResult Evaluate(IntPtr hwnd, EligibilityScope scope)
            => EvaluateWithSnapshot(hwnd, scope).Result;

        public (EligibilityResult Result, WindowEligibilitySnapshot Snapshot) EvaluateWithSnapshot(
            IntPtr hwnd,
            EligibilityScope scope)
        {
            var snapshot = WindowEligibilitySnapshot.FromHwnd(hwnd, _policy.HasTitleDependentRules);
            var processBlacklist = scope == EligibilityScope.Discovery
                ? BuildProcessBlacklistPredicate()
                : null;

            return (_policy.Evaluate(snapshot, processBlacklist), snapshot);
        }

        /// <summary>
        /// 取当前进程黑名单的稳定引用，包成按进程名判定的谓词。锁内捕获引用
        /// （<see cref="UpdateBlacklist"/> 整体替换、不原地修改），谓词调用路径无锁。
        /// </summary>
        private Func<string, bool> BuildProcessBlacklistPredicate()
        {
            IEnumerable<string> blacklist;
            lock (_blacklistLock)
            {
                blacklist = _dynamicBlacklist;
            }

            return name => IsProcessNameBlacklisted(name, blacklist);
        }

        private static bool IsProcessNameBlacklisted(string? processName, IEnumerable<string> blacklist)
        {
            if (string.IsNullOrWhiteSpace(processName))
            {
                return false;
            }

            foreach (var entry in blacklist)
            {
                if (!string.IsNullOrWhiteSpace(entry) &&
                    string.Equals(entry, processName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
