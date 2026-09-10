// [Path]: Pulsar/Pulsar/Core/Debug/DebugSecretFillRedaction.cs

using System;

namespace Pulsar.Core.Debug
{
    /// <summary>
    /// Debug-mode SecretFill/secret redaction for capture output. While
    /// <see cref="IsActive"/> is set (a <c>--ui-debug</c> run), secret-bearing
    /// display metadata is masked so E2E screenshots and state events can never
    /// leak real credential labels or accounts. Production behavior is untouched
    /// (default <see cref="IsActive"/> = false).
    /// </summary>
    public static class DebugSecretFillRedaction
    {
        public static bool IsActive { get; set; }

        public static string RedactSecretDisplay(string label)
            => IsActive ? Mask(label) : label;

        public static string RedactAccount(string account)
            => IsActive ? Mask(account) : account;

        /// <summary>
        /// Keep only the leading character (so distinct secrets stay tellable in a
        /// capture) and mask the remainder with bullets, capped to a fixed width so
        /// the mask itself never leaks the plaintext length.
        /// </summary>
        private static string Mask(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return value;
            }

            if (value.Length == 1)
            {
                return "•";
            }

            return value[..1] + new string('•', Math.Min(value.Length - 1, 7));
        }
    }
}
