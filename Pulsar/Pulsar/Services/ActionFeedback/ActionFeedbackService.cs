using System;
using Pulsar.Core.Plugin;
using Pulsar.Models;

namespace Pulsar.Services.ActionFeedback
{
    public sealed class ActionFeedbackService : IActionFeedbackService
    {
        public ActionFeedback Create(string pluginId, string action, PluginResult result)
        {
            if (result.Success)
            {
                return CreateSuccessFeedback(pluginId, action);
            }

            if (IsTemporaryUnavailable(result.Message))
            {
                return new ActionFeedback(
                    ActionFeedbackKind.TemporaryUnavailable,
                    "Action unavailable",
                    "This action is temporarily unavailable.",
                    "Wait a moment and try again.",
                    PulsarNotificationIcon.Warning);
            }

            if (string.Equals(pluginId, "com.pulsar.winswitcher", StringComparison.OrdinalIgnoreCase))
            {
                return CreateWinSwitcherFailure(result.Message);
            }

            if (string.Equals(pluginId, "com.pulsar.command", StringComparison.OrdinalIgnoreCase))
            {
                return CreateCommandFailure(action, result.Message);
            }

            if (string.Equals(pluginId, "com.pulsar.pki", StringComparison.OrdinalIgnoreCase))
            {
                return CreatePkiFailure(result.Message);
            }

            if (string.Equals(pluginId, "com.pulsar.bookmarklet", StringComparison.OrdinalIgnoreCase))
            {
                return CreateBookmarkletFailure(result.Message);
            }

            return new ActionFeedback(
                ActionFeedbackKind.RecoverableFailure,
                "Action failed",
                "The action did not complete.",
                "Try again or review the slot settings.",
                PulsarNotificationIcon.Error);
        }

        private static ActionFeedback CreateSuccessFeedback(string pluginId, string action)
        {
            if (string.Equals(pluginId, "com.pulsar.winswitcher", StringComparison.OrdinalIgnoreCase))
            {
                string title = string.Equals(action, "launch", StringComparison.OrdinalIgnoreCase)
                    ? "App launched"
                    : "App switched";

                string message = string.Equals(action, "launch", StringComparison.OrdinalIgnoreCase)
                    ? "The app was launched successfully."
                    : "The app was brought to the front successfully.";

                return new ActionFeedback(ActionFeedbackKind.Success, title, message, null, PulsarNotificationIcon.Info);
            }

            if (string.Equals(pluginId, "com.pulsar.command", StringComparison.OrdinalIgnoreCase))
            {
                string title = string.Equals(action, "sendkeys", StringComparison.OrdinalIgnoreCase)
                    ? "Input sent"
                    : "Target opened";

                string message = string.Equals(action, "sendkeys", StringComparison.OrdinalIgnoreCase)
                    ? "The keys or text were sent to the active app."
                    : "The target opened successfully.";

                return new ActionFeedback(ActionFeedbackKind.Success, title, message, null, PulsarNotificationIcon.Info);
            }

            if (string.Equals(pluginId, "com.pulsar.pki", StringComparison.OrdinalIgnoreCase))
            {
                return new ActionFeedback(
                    ActionFeedbackKind.Success,
                    "Credential filled",
                    "The saved credential was sent to the active app.",
                    null,
                    PulsarNotificationIcon.Info);
            }

            return new ActionFeedback(
                ActionFeedbackKind.Success,
                "Action complete",
                "The action completed successfully.",
                null,
                PulsarNotificationIcon.Info);
        }

        private static ActionFeedback CreateWinSwitcherFailure(string? message)
        {
            if (ContainsAny(message, "Missing required parameter", "Path must be absolute", "Unsupported file type", "Application not found", "File not found"))
            {
                return new ActionFeedback(
                    ActionFeedbackKind.ConfigurationError,
                    "Fix slot setup",
                    "This app slot needs updated settings before it can run.",
                    "Open the slot and verify the app name or launch path.",
                    PulsarNotificationIcon.Warning);
            }

            if (ContainsAny(message, "is not running and no launch path specified", "is not running"))
            {
                return new ActionFeedback(
                    ActionFeedbackKind.RecoverableFailure,
                    "App not available",
                    "Pulsar could not find a running app to switch to.",
                    "Open the app first or add a launch path to this slot.",
                    PulsarNotificationIcon.Warning);
            }

            return new ActionFeedback(
                ActionFeedbackKind.RecoverableFailure,
                "Switch failed",
                "Pulsar could not switch to that app.",
                "Try again after the app is ready.",
                PulsarNotificationIcon.Error);
        }

        private static ActionFeedback CreateCommandFailure(string action, string? message)
        {
            if (ContainsAny(message, "Missing required parameter: path", "Missing required parameter: keys"))
            {
                return new ActionFeedback(
                    ActionFeedbackKind.ConfigurationError,
                    "Fix slot setup",
                    "This command slot is missing required settings.",
                    "Open the slot and fill in the missing value.",
                    PulsarNotificationIcon.Warning);
            }

            if (string.Equals(action, "sendkeys", StringComparison.OrdinalIgnoreCase))
            {
                return new ActionFeedback(
                    ActionFeedbackKind.RecoverableFailure,
                    "Input failed",
                    "Pulsar could not send the keys or text to the active app.",
                    "Make sure the target app is focused, then try again.",
                    PulsarNotificationIcon.Error);
            }

            return new ActionFeedback(
                ActionFeedbackKind.RecoverableFailure,
                "Open failed",
                "Pulsar could not open the configured target.",
                "Check that the target exists and try again.",
                PulsarNotificationIcon.Error);
        }

        private static ActionFeedback CreatePkiFailure(string? message)
        {
            if (ContainsAny(message, "Missing required parameter: secretId", "Secret not found", "Secret data is empty", "Decryption failed"))
            {
                return new ActionFeedback(
                    ActionFeedbackKind.ConfigurationError,
                    "Fix credential slot",
                    "This credential action needs an updated saved secret before it can run.",
                    "Open the slot and choose a valid saved credential.",
                    PulsarNotificationIcon.Warning);
            }

            if (ContainsAny(message, "restore focus", "text entry", "key entry", "injection failed", "hide launcher"))
            {
                return new ActionFeedback(
                    ActionFeedbackKind.RecoverableFailure,
                    "Credential fill failed",
                    "Pulsar could not send the saved credential to the active app.",
                    "Focus the target field and try again.",
                    PulsarNotificationIcon.Error);
            }

            return new ActionFeedback(
                ActionFeedbackKind.RecoverableFailure,
                "Credential fill failed",
                "Pulsar could not complete the credential action.",
                "Try again after the target app is ready.",
                PulsarNotificationIcon.Error);
        }

        private static ActionFeedback CreateBookmarkletFailure(string? message)
        {
            if (ContainsAny(message, "缺少必要参数: scriptPath", "找不到脚本文件", "脚本验证失败", "脚本内容为空", "脚本路径包含不安全字符"))
            {
                return new ActionFeedback(
                    ActionFeedbackKind.ConfigurationError,
                    "Fix bookmarklet slot",
                    "This bookmarklet slot needs a valid script file before it can run.",
                    "Open the slot and verify the configured script path.",
                    PulsarNotificationIcon.Warning);
            }

            if (ContainsAny(message, "浏览器地址栏", "browser address bar", "未检测到运行中的浏览器", "Failed to focus browser"))
            {
                return new ActionFeedback(
                    ActionFeedbackKind.RecoverableFailure,
                    "Bookmarklet failed",
                    "Pulsar could not inject the bookmarklet into the browser.",
                    "Wait for the page or browser address bar to finish loading, then try again.",
                    PulsarNotificationIcon.Error);
            }

            return new ActionFeedback(
                ActionFeedbackKind.RecoverableFailure,
                "Bookmarklet failed",
                "Pulsar could not complete the bookmarklet action.",
                "Make sure the browser is ready, then try again.",
                PulsarNotificationIcon.Error);
        }

        private static bool IsTemporaryUnavailable(string? message)
        {
            return ContainsAny(message, "disabled for safety", "Plugin is disabled", "temporarily disabled");
        }

        private static bool ContainsAny(string? value, params string[] needles)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            foreach (string needle in needles)
            {
                if (value.Contains(needle, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
