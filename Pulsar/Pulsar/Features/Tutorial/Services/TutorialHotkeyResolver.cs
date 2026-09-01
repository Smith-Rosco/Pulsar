using System;
using System.Collections.Generic;
using System.Linq;
using Pulsar.Helpers;
using Pulsar.Models;

namespace Pulsar.Features.Tutorial.Services
{
    /// <summary>
    /// 教程文案热键解析器：把步骤文案里的 {SwitchHotkey} / {CommandHotkey} 占位符
    /// 替换为用户当前配置的实际热键（如 "Ctrl+Q"）。未配置或为空时回退到默认值
    /// （Ctrl+Q / Ctrl+Shift+Q）。纯逻辑，便于单元测试。
    /// </summary>
    public static class TutorialHotkeyResolver
    {
        public const string SwitchToken = "{SwitchHotkey}";
        public const string CommandToken = "{CommandHotkey}";

        private const string DefaultSwitch = "Ctrl+Q";
        private const string DefaultCommand = "Ctrl+Shift+Q";

        private static readonly IReadOnlyDictionary<string, string> ModifierAbbreviations =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Control"] = "Ctrl",
                ["Shift"] = "Shift",
                ["Alt"] = "Alt",
                ["Windows"] = "Win",
            };

        /// <summary>
        /// 将 <paramref name="text"/> 中的占位符替换为配置热键。
        /// 不持有热键配置时（hotkeys 为 null）仍替换为默认值，保持文案可读。
        /// </summary>
        public static string Resolve(string? text, IReadOnlyDictionary<string, HotkeyConfig>? hotkeys)
        {
            if (string.IsNullOrEmpty(text)) return text ?? string.Empty;

            string switchText = FormatHotkeyOrDefault(hotkeys, HotkeyActionIds.ShowSwitcher, DefaultSwitch);
            string commandText = FormatHotkeyOrDefault(hotkeys, HotkeyActionIds.ShowGrid, DefaultCommand);

            return text
                .Replace(SwitchToken, switchText, StringComparison.OrdinalIgnoreCase)
                .Replace(CommandToken, commandText, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 取指定动作的热键并格式化为 "Ctrl+Q" 样式；缺失/空时返回默认值。
        /// </summary>
        public static string FormatHotkeyOrDefault(
            IReadOnlyDictionary<string, HotkeyConfig>? hotkeys,
            string actionId,
            string fallback)
        {
            if (hotkeys != null
                && hotkeys.TryGetValue(actionId, out var config)
                && config != null
                && !config.IsEmpty)
            {
                string formatted = FormatHotkey(config);
                if (!string.IsNullOrEmpty(formatted))
                {
                    return formatted;
                }
            }

            return fallback;
        }

        /// <summary>
        /// 将 HotkeyConfig 格式化为 "Ctrl+Shift+Q" 样式（修饰符缩写：Control→Ctrl, Windows→Win）。
        /// </summary>
        public static string FormatHotkey(HotkeyConfig config)
        {
            if (config == null || config.IsEmpty) return string.Empty;

            var modifiers = string.IsNullOrWhiteSpace(config.Modifiers)
                ? Array.Empty<string>()
                : config.Modifiers
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(m => ModifierAbbreviations.TryGetValue(m, out var abbr) ? abbr : m);

            var all = modifiers.Append(config.Key);
            return string.Join("+", all);
        }
    }
}
