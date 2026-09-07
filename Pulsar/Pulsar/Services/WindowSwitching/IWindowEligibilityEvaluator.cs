using System;
using System.Collections.Generic;

namespace Pulsar.Services.WindowSwitching
{
    /// <summary>
    /// 判定作用域：Discovery（发现路径，进程黑名单生效）与 Explicit（显式激活，进程黑名单不参与）。
    /// </summary>
    public enum EligibilityScope
    {
        Discovery,
        Explicit
    }

    /// <summary>
    /// "可切换窗口"判定的单一入口：组合 <see cref="IWindowEligibilityPolicy"/>、持有进程黑名单状态，
    /// 并负责从 HWND 组装 <see cref="WindowEligibilitySnapshot"/>（含基于
    /// <see cref="IWindowEligibilityPolicy.HasTitleDependentRules"/> 的条件标题读取）。
    /// <para>
    /// 收拢了此前散落在 WindowService 五个调用点的 "FromHwnd + Evaluate" 序列，
    /// 使快照组装、黑名单谓词与判定作用域只有一份实现。
    /// </para>
    /// </summary>
    public interface IWindowEligibilityEvaluator
    {
        /// <summary>判定一个真实 HWND 是否可切换。</summary>
        EligibilityResult Evaluate(IntPtr hwnd, EligibilityScope scope);

        /// <summary>
        /// 判定一个真实 HWND 并返回其结构快照（供诊断日志使用）。
        /// 诊断路径需要快照字段（类名 / 矩形 / 样式等），故单独暴露。
        /// </summary>
        (EligibilityResult Result, WindowEligibilitySnapshot Snapshot) EvaluateWithSnapshot(IntPtr hwnd, EligibilityScope scope);

        /// <summary>
        /// 第一道筛：只判定不依赖进程名的硬规则（自身窗口 / 可见性 / cloaked / 样式 / owner /
        /// 物理可见性 / 类名黑名单）。快照的 <see cref="WindowEligibilitySnapshot.ProcessName"/>
        /// 可以为空 —— 全桌面枚举用它先淘汰绝大多数窗口，避免为每个窗口解析进程元数据
        /// （解析进程元数据比所有廉价 native 读取加起来还贵一个量级）。
        /// </summary>
        EligibilityResult EvaluateStructural(WindowEligibilitySnapshot snapshot);

        /// <summary>
        /// 对<b>已建好的快照</b>做完整判定（结构 + 身份）。两阶段枚举先经
        /// <see cref="EvaluateStructural"/> 筛掉大多数窗口，只为幸存窗口解析进程名后调用本方法；
        /// 单快照路径（Inspector 诊断报告）直接调用。scope 决定进程黑名单是否参与
        /// （<see cref="EligibilityScope.Discovery"/> 生效 / <see cref="EligibilityScope.Explicit"/> 忽略）。
        /// 快照须含 ProcessName（存在标题依赖规则时还须含 Title，见 <see cref="HasTitleDependentRules"/>）。
        /// </summary>
        EligibilityResult EvaluateSnapshot(WindowEligibilitySnapshot snapshot, EligibilityScope scope);

        /// <summary>更新进程黑名单（与系统默认黑名单合并后原子替换）。</summary>
        void UpdateBlacklist(IEnumerable<string> userBlacklist);

        /// <summary>原子替换用户窗口排除/放行规则（透传至 policy）。</summary>
        void UpdateRules(IReadOnlyList<WindowEligibilityRule>? rules);

        /// <summary>当前生效的用户规则（有序，透传至 policy）。</summary>
        IReadOnlyList<WindowEligibilityRule> Rules { get; }

        /// <summary>是否存在标题依赖规则（透传至 policy）。</summary>
        bool HasTitleDependentRules { get; }

        /// <summary>发现路径的进程黑名单判定（供枚举通道按进程名过滤）。</summary>
        bool IsDiscoveryBlacklisted(string processName);
    }
}
