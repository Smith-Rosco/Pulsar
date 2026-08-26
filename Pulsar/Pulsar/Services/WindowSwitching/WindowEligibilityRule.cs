using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Pulsar.Services.WindowSwitching
{
    /// <summary>
    /// 用户可配置的窗口身份排除/放行规则。
    /// 匹配字段：<see cref="WindowClass"/>（精确，忽略大小写）/ <see cref="TitlePattern"/>（正则，忽略大小写），
    /// <see cref="ProcessName"/> 作为可选限定条件。
    /// 语义：<see cref="Allow"/> 是绝对放行（覆盖之前任何 Exclude）；Exclude 为"暂定排除"，
    /// 会被之后的任何 Allow 救回。物理可见性由硬规则统一处理（见 <see cref="WindowEligibilityPolicy"/>），
    /// 不设 RectState 维度 —— 屏幕外/零尺寸窗口已由硬规则排除，规则无需重复表达。
    /// 纯逻辑（无 P/Invoke），供 policy 规则链与 Inspector 生成/展示共用。
    /// </summary>
    public sealed record WindowEligibilityRule(
        bool Allow,
        string? ProcessName,
        string? WindowClass,
        string? TitlePattern)
    {
        /// <summary>
        /// 规则必须至少匹配一个身份维度（Class / Title）。纯进程名排除请走 ExcludeProcesses
        /// （保持 discovery 作用域语义），避免绕过进程黑名单的作用域约定。
        /// </summary>
        public bool IsIdentityRule
            => !string.IsNullOrWhiteSpace(WindowClass) || !string.IsNullOrWhiteSpace(TitlePattern);

        /// <summary>
        /// 快照是否命中本规则。TitlePattern 依赖快照携带 Title —— 未携带（热路径未读标题）时
        /// 视为不命中，调用方通过 policy 的 <see cref="IWindowEligibilityPolicy.HasTitleDependentRules"/>
        /// 决定是否读取标题。
        /// </summary>
        public bool Matches(WindowEligibilitySnapshot snapshot)
        {
            if (!IsIdentityRule)
            {
                return false;
            }

            if (ProcessName != null
                && !string.Equals(snapshot.ProcessName, ProcessName, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (WindowClass != null
                && !string.Equals(snapshot.ClassName, WindowClass, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (TitlePattern != null
                && (string.IsNullOrEmpty(snapshot.Title)
                    || !Regex.IsMatch(snapshot.Title, TitlePattern, RegexOptions.IgnoreCase)))
            {
                return false;
            }

            return true;
        }
    }

    /// <summary>规则列表的 JSON 序列化/反序列化（ExcludeRules 设置项与 Inspector 共用）。</summary>
    public static class WindowEligibilityRuleSerializer
    {
        public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.General)
        {
            PropertyNameCaseInsensitive = true
        };

        /// <summary>
        /// 解析 JSON 数组文本；非合法 JSON 或解析失败返回 null（调用方回退为空规则集并告警）。
        /// 只保留 <see cref="WindowEligibilityRule.IsIdentityRule"/> 为 true 的规则。
        /// </summary>
        public static List<WindowEligibilityRule>? TryParse(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return new List<WindowEligibilityRule>();
            }

            try
            {
                var rules = JsonSerializer.Deserialize<List<WindowEligibilityRule>>(json, Options);
                return rules?
                    .Where(rule => rule.IsIdentityRule)
                    .ToList();
            }
            catch (JsonException)
            {
                return null;
            }
        }

        public static string Serialize(IEnumerable<WindowEligibilityRule> rules)
            => JsonSerializer.Serialize(rules, Options);
    }
}
