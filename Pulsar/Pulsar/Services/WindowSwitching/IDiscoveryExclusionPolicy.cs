using System.Collections.Generic;
using System.Threading.Tasks;

namespace Pulsar.Services.WindowSwitching
{
    /// <summary>
    /// Discovery Exclusion Policy 的单一所有者：进程黑名单（ExcludeProcesses）、窗口身份
    /// 排除/放行规则（ExcludeRules）与切换诊断开关（EnableSwitchDiagnostics）三个配置键
    /// 的解析、运行时应用与持久化只有一份实现。所有写者（插件激活路径、设置 UI、
    /// Window Inspector）都经由本接口写入，Eligibility Evaluator 的应用点也只此一处。
    /// <para>
    /// 本策略独立于 WinSwitcher 插件生命周期：插件卸载不清理策略状态（用户排除偏好
    /// 比插件活得更久）。见架构审查 2026-09-10 候选 W2。
    /// </para>
    /// </summary>
    public interface IDiscoveryExclusionPolicy
    {
        /// <summary>当前生效的用户排除规则（有序，透传至 evaluator）。</summary>
        IReadOnlyList<WindowEligibilityRule> Rules { get; }

        /// <summary>切换诊断日志开关（默认关闭）。WindowService 读取此状态决定是否记录诊断。</summary>
        bool DiagnosticsEnabled { get; }

        /// <summary>
        /// 从插件配置字典解析并应用排除策略。只应用字典中出现的键，缺失的键不触碰现状；
        /// 不持久化。启动引导、内核插件激活与设置保存链路共用。
        /// </summary>
        void ApplyFromConfig(IReadOnlyDictionary<string, object> config);

        /// <summary>
        /// 原子替换用户排除规则：先应用到 evaluator（本次会话立即生效），再持久化到
        /// WinSwitcher 插件配置的 ExcludeRules 键（ConfigEditSession 单写者通道）。
        /// 持久化失败时规则仍然生效，异常向上抛给调用方呈现。
        /// </summary>
        Task SetRulesAsync(IReadOnlyList<WindowEligibilityRule> rules);

        /// <summary>
        /// 启动引导：从 Profiles.json 快照读取 WinSwitcher 插件配置并应用，
        /// 在内核激活插件之前调用，保证 evaluator 从一开始就持有正确的排除状态。
        /// </summary>
        void InitializeFromConfig();
    }
}
