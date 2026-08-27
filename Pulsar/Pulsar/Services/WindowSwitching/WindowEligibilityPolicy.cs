using System;
using System.Collections.Generic;
using System.Linq;
using Pulsar.Native;

namespace Pulsar.Services.WindowSwitching
{
    /// <summary>窗口为何被纳入或排除出"可切换窗口"集合。</summary>
    public enum WindowEligibilityVerdict
    {
        Eligible,

        /// <summary>Pulsar 自身的窗口。</summary>
        ExcludedSelf,

        /// <summary>WS_VISIBLE 未置位。</summary>
        ExcludedHidden,

        /// <summary>DWM cloaked（其他虚拟桌面 / 挂起的 UWP）。</summary>
        ExcludedCloaked,

        /// <summary>WS_EX_TOOLWINDOW（工具窗口，不参与 Alt-Tab）。</summary>
        ExcludedToolWindow,

        /// <summary>WS_CHILD 子窗口，不是独立可切换窗口。</summary>
        ExcludedChild,

        /// <summary>有 owner 且非 WS_EX_APPWINDOW（owned 弹窗，非独立任务）。</summary>
        ExcludedOwned,

        /// <summary>物理不可见：零尺寸矩形或与虚拟屏幕无正重叠（最小化窗口豁免）。</summary>
        ExcludedOffScreen,

        /// <summary>窗口类名命中类名黑名单（非用户可见的 helper/host 窗口）。</summary>
        ExcludedBlacklistedClass,

        /// <summary>进程名命中进程黑名单谓词。</summary>
        ExcludedBlacklistedProcess,

        /// <summary>命中用户配置的排除规则（<see cref="WindowEligibilityRule"/>）。</summary>
        ExcludedByRule
    }

    /// <summary>一次判定结论。</summary>
    public readonly record struct EligibilityResult(bool Included, WindowEligibilityVerdict Verdict);

    /// <summary>Inspector / 诊断用的一行窗口判定报告。</summary>
    public sealed record WindowEligibilityReport(
        IntPtr Hwnd,
        string Title,
        string ProcessName,
        string ClassName,
        PulsarNative.RECT? Rect,
        bool Included,
        WindowEligibilityVerdict Verdict);

    /// <summary>
    /// "可切换窗口"判定接口：输入结构性快照，输出是否纳入 + 原因。
    /// 纯逻辑 —— 无 P/Invoke、无锁；所有 native 事实都来自 <see cref="WindowEligibilitySnapshot"/>。
    /// 三个消费面（快速切换 IsAltTabWindow、发现枚举、进程激活枚举）共用同一判定，杜绝"每处各抄一遍"。
    /// </summary>
    public interface IWindowEligibilityPolicy
    {
        /// <summary>
        /// 判定窗口是否可切换。
        /// </summary>
        /// <param name="snapshot">窗口结构事实。</param>
        /// <param name="processBlacklist">进程黑名单谓词（按进程名）；null 表示不检查进程黑名单
        /// （显式激活路径保持"直接选中即切换"的既有语义）。</param>
        EligibilityResult Evaluate(WindowEligibilitySnapshot snapshot, Func<string, bool>? processBlacklist = null);

        /// <summary>
        /// 是否存在依赖标题的规则（TitlePattern）。为 true 时，调用方应读取窗口标题填入快照，
        /// 否则标题规则永不命中（快照 Title 为空）。
        /// </summary>
        bool HasTitleDependentRules { get; }

        /// <summary>当前生效的用户规则（有序）。</summary>
        IReadOnlyList<WindowEligibilityRule> Rules { get; }

        /// <summary>原子替换用户规则（忽略非身份规则）。</summary>
        void UpdateRules(IReadOnlyList<WindowEligibilityRule>? rules);
    }

    public sealed class WindowEligibilityPolicy : IWindowEligibilityPolicy
    {
        private readonly uint _ownPid;
        private readonly IReadOnlySet<string> _windowClassBlacklist;
        private volatile IReadOnlyList<WindowEligibilityRule> _rules = Array.Empty<WindowEligibilityRule>();

        public bool HasTitleDependentRules { get; private set; }

        public IReadOnlyList<WindowEligibilityRule> Rules => _rules;

        public WindowEligibilityPolicy(uint ownPid, IReadOnlySet<string>? windowClassBlacklist = null)
        {
            _ownPid = ownPid;
            _windowClassBlacklist = windowClassBlacklist ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        public void UpdateRules(IReadOnlyList<WindowEligibilityRule>? rules)
        {
            var normalized = rules?.Where(rule => rule.IsIdentityRule).ToArray()
                ?? Array.Empty<WindowEligibilityRule>();
            _rules = normalized;
            HasTitleDependentRules = normalized.Any(rule => !string.IsNullOrWhiteSpace(rule.TitlePattern));
        }

        public EligibilityResult Evaluate(WindowEligibilitySnapshot snapshot, Func<string, bool>? processBlacklist = null)
        {
            if (snapshot.Pid == _ownPid)
            {
                return Excluded(WindowEligibilityVerdict.ExcludedSelf);
            }

            if (!snapshot.IsVisible)
            {
                return Excluded(WindowEligibilityVerdict.ExcludedHidden);
            }

            if (snapshot.IsCloaked)
            {
                return Excluded(WindowEligibilityVerdict.ExcludedCloaked);
            }

            if ((snapshot.ExStyle & PulsarNative.WS_EX_TOOLWINDOW) != 0)
            {
                return Excluded(WindowEligibilityVerdict.ExcludedToolWindow);
            }

            if ((snapshot.Style & PulsarNative.WS_CHILD) != 0)
            {
                return Excluded(WindowEligibilityVerdict.ExcludedChild);
            }

            if (snapshot.OwnerHwnd != IntPtr.Zero && (snapshot.ExStyle & PulsarNative.WS_EX_APPWINDOW) == 0)
            {
                return Excluded(WindowEligibilityVerdict.ExcludedOwned);
            }

            if (IsPhysicallyInvisible(snapshot))
            {
                return Excluded(WindowEligibilityVerdict.ExcludedOffScreen);
            }

            if (IsWindowClassBlacklisted(snapshot.ClassName))
            {
                return Excluded(WindowEligibilityVerdict.ExcludedBlacklistedClass);
            }

            if (processBlacklist?.Invoke(snapshot.ProcessName) == true)
            {
                return Excluded(WindowEligibilityVerdict.ExcludedBlacklistedProcess);
            }

            // [Deepen] 用户规则链：Allow 是绝对放行（覆盖之前任何 Exclude），Exclude 为暂定排除
            // （会被之后的任何 Allow 救回）。不能覆盖上面的硬规则（结构 / 物理可见性 / 系统类名）。
            // 标题规则仅在快照携带 Title 时命中。
            bool excludedByRule = false;
            foreach (var rule in _rules)
            {
                if (!rule.Matches(snapshot))
                {
                    continue;
                }

                if (rule.Allow)
                {
                    return new EligibilityResult(true, WindowEligibilityVerdict.Eligible);
                }

                excludedByRule = true;
            }

            return excludedByRule
                ? new EligibilityResult(false, WindowEligibilityVerdict.ExcludedByRule)
                : new EligibilityResult(true, WindowEligibilityVerdict.Eligible);
        }

        private static EligibilityResult Excluded(WindowEligibilityVerdict verdict)
            => new(false, verdict);

        /// <summary>
        /// 物理可见性：最小化窗口豁免；否则矩形必须非空、非零尺寸且与虚拟屏幕有正重叠。
        /// 这是对"WS_VISIBLE 但屏幕外/零尺寸"这类幽灵窗口（KxWppQuickHelpBarContainer、
        /// Chrome Legacy Window）的通用判定 —— 无需再逐个硬编码类名。
        /// </summary>
        internal static bool IsPhysicallyInvisible(WindowEligibilitySnapshot snapshot)
        {
            if (snapshot.IsIconic)
            {
                return false;
            }

            if (snapshot.Rect is not { } rect)
            {
                return true;
            }

            if (rect.Right <= rect.Left || rect.Bottom <= rect.Top)
            {
                return true;
            }

            return !Intersects(rect, snapshot.VirtualScreenRect);
        }

        /// <summary>两个矩形是否有正的相交面积。</summary>
        internal static bool Intersects(PulsarNative.RECT a, PulsarNative.RECT b)
            => a.Left < b.Right && a.Right > b.Left && a.Top < b.Bottom && a.Bottom > b.Top;

        private bool IsWindowClassBlacklisted(string className)
            => !string.IsNullOrEmpty(className) && _windowClassBlacklist.Contains(className);
    }
}
