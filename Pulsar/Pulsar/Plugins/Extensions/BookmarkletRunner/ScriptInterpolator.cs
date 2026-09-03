using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace Pulsar.Plugins.Extensions.BookmarkletRunner
{
    /// <summary>
    /// Interpolates <c>{{name}}</c> placeholders in bookmarklet scripts with values
    /// taken from the slot's parameter arguments. <c>{{{{</c> escapes to a literal
    /// <c>{{</c> so real JavaScript braces are never mistaken for placeholders.
    /// </summary>
    public static class ScriptInterpolator
    {
        private static readonly Regex PlaceholderRegex =
            new(@"\{\{([^{}]+)\}\}", RegexOptions.Compiled);

        /// <summary>
        /// Replaces every <c>{{name}}</c> placeholder with the value of the matching
        /// argument. Missing placeholders are reported (and left untouched in the
        /// output) so the runner can fail with a user-meaningful message.
        /// </summary>
        public static InterpolationResult Interpolate(
            string content,
            IReadOnlyDictionary<string, string> args)
        {
            var missing = new List<string>();
            if (string.IsNullOrEmpty(content))
            {
                return new InterpolationResult(string.Empty, missing);
            }

            // Protect the escape sequence first so "{{{{" never matches the pattern.
            const string escapeSentinel = "\u0001\u0002";
            var escaped = content.Replace("{{{{", escapeSentinel);

            var sb = new StringBuilder();
            var lastIndex = 0;

            foreach (Match match in PlaceholderRegex.Matches(escaped))
            {
                sb.Append(escaped, lastIndex, match.Index - lastIndex);

                var name = match.Groups[1].Value.Trim();
                if (TryGetValue(args, name, out var value))
                {
                    sb.Append(value);
                }
                else
                {
                    missing.Add(name);
                    // Keep the original placeholder in the payload for visibility.
                    sb.Append(match.Value);
                }

                lastIndex = match.Index + match.Length;
            }

            sb.Append(escaped, lastIndex, escaped.Length - lastIndex);
            sb.Replace(escapeSentinel, "{{");

            return new InterpolationResult(sb.ToString(), missing);
        }

        private static bool TryGetValue(IReadOnlyDictionary<string, string> args, string name, out string value)
        {
            if (args.TryGetValue(name, out value!))
            {
                return true;
            }

            // Fall back to case-insensitive matching for friendlier slots.
            foreach (var pair in args)
            {
                if (string.Equals(pair.Key, name, StringComparison.OrdinalIgnoreCase))
                {
                    value = pair.Value ?? string.Empty;
                    return true;
                }
            }

            value = string.Empty;
            return false;
        }
    }

    public sealed class InterpolationResult
    {
        public InterpolationResult(string content, IReadOnlyList<string> missingPlaceholders)
        {
            Content = content;
            MissingPlaceholders = missingPlaceholders;
        }

        public string Content { get; }

        public IReadOnlyList<string> MissingPlaceholders { get; }
    }
}
