using System;
using Pulsar.Core.Localization;
using Pulsar.Core.Plugin;
using Pulsar.Models;

namespace Pulsar.Services.ActionFeedback
{
    public sealed class ActionFeedbackService : IActionFeedbackService
    {
        private readonly ILocalizationService _loc;

        public ActionFeedbackService(ILocalizationService loc)
        {
            _loc = loc;
        }

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
                    _loc["Feedback.ActionUnavailable"],
                    _loc["Feedback.ActionUnavailableBody"],
                    _loc["Feedback.ActionUnavailableHelp"],
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
                _loc["Feedback.ActionFailed"],
                _loc["Feedback.ActionFailedBody"],
                _loc["Feedback.ActionFailedHelp"],
                PulsarNotificationIcon.Error);
        }

        private ActionFeedback CreateSuccessFeedback(string pluginId, string action)
        {
            if (string.Equals(pluginId, "com.pulsar.winswitcher", StringComparison.OrdinalIgnoreCase))
            {
                string title = string.Equals(action, "launch", StringComparison.OrdinalIgnoreCase)
                    ? _loc["Feedback.AppLaunched"]
                    : _loc["Feedback.AppSwitched"];

                string message = string.Equals(action, "launch", StringComparison.OrdinalIgnoreCase)
                    ? _loc["Feedback.AppLaunchedBody"]
                    : _loc["Feedback.AppSwitchedBody"];

                return new ActionFeedback(ActionFeedbackKind.Success, title, message, null, PulsarNotificationIcon.Info);
            }

            if (string.Equals(pluginId, "com.pulsar.command", StringComparison.OrdinalIgnoreCase))
            {
                string title = string.Equals(action, "sendkeys", StringComparison.OrdinalIgnoreCase)
                    ? _loc["Feedback.InputSent"]
                    : _loc["Feedback.TargetOpened"];

                string message = string.Equals(action, "sendkeys", StringComparison.OrdinalIgnoreCase)
                    ? _loc["Feedback.InputSentBody"]
                    : _loc["Feedback.TargetOpenedBody"];

                return new ActionFeedback(ActionFeedbackKind.Success, title, message, null, PulsarNotificationIcon.Info);
            }

            if (string.Equals(pluginId, "com.pulsar.pki", StringComparison.OrdinalIgnoreCase))
            {
                return new ActionFeedback(
                    ActionFeedbackKind.Success,
                    _loc["Feedback.CredentialFilled"],
                    _loc["Feedback.CredentialFilledBody"],
                    null,
                    PulsarNotificationIcon.Info);
            }

            return new ActionFeedback(
                ActionFeedbackKind.Success,
                _loc["Feedback.ActionComplete"],
                _loc["Feedback.ActionCompleteBody"],
                null,
                PulsarNotificationIcon.Info);
        }

        private ActionFeedback CreateWinSwitcherFailure(string? message)
        {
            if (ContainsAny(message, "Missing required parameter", "Path must be absolute", "Unsupported file type", "Application not found", "File not found"))
            {
                return new ActionFeedback(
                    ActionFeedbackKind.ConfigurationError,
                    _loc["Feedback.FixSlotSetup"],
                    _loc["Feedback.FixSlotSetupBody"],
                    _loc["Feedback.FixSlotSetupHelp"],
                    PulsarNotificationIcon.Warning);
            }

            if (ContainsAny(message, "is not running and no launch path specified", "is not running"))
            {
                return new ActionFeedback(
                    ActionFeedbackKind.RecoverableFailure,
                    _loc["Feedback.AppNotAvailable"],
                    _loc["Feedback.AppNotAvailableBody"],
                    _loc["Feedback.AppNotAvailableHelp"],
                    PulsarNotificationIcon.Warning);
            }

            return new ActionFeedback(
                ActionFeedbackKind.RecoverableFailure,
                _loc["Feedback.SwitchFailed"],
                _loc["Feedback.SwitchFailedBody"],
                _loc["Feedback.SwitchFailedHelp"],
                PulsarNotificationIcon.Error);
        }

        private ActionFeedback CreateCommandFailure(string action, string? message)
        {
            if (ContainsAny(message, "Missing required parameter: path", "Missing required parameter: keys"))
            {
                return new ActionFeedback(
                    ActionFeedbackKind.ConfigurationError,
                    _loc["Feedback.FixCommandSlot"],
                    _loc["Feedback.FixCommandSlotBody"],
                    _loc["Feedback.FixCommandSlotHelp"],
                    PulsarNotificationIcon.Warning);
            }

            if (string.Equals(action, "sendkeys", StringComparison.OrdinalIgnoreCase))
            {
                return new ActionFeedback(
                    ActionFeedbackKind.RecoverableFailure,
                    _loc["Feedback.InputFailed"],
                    _loc["Feedback.InputFailedBody"],
                    _loc["Feedback.InputFailedHelp"],
                    PulsarNotificationIcon.Error);
            }

            return new ActionFeedback(
                ActionFeedbackKind.RecoverableFailure,
                _loc["Feedback.OpenFailed"],
                _loc["Feedback.OpenFailedBody"],
                _loc["Feedback.OpenFailedHelp"],
                PulsarNotificationIcon.Error);
        }

        private ActionFeedback CreatePkiFailure(string? message)
        {
            if (ContainsAny(message, "Missing required parameter: secretId", "Secret not found", "Secret data is empty", "Decryption failed"))
            {
                return new ActionFeedback(
                    ActionFeedbackKind.ConfigurationError,
                    _loc["Feedback.FixCredentialSlot"],
                    _loc["Feedback.FixCredentialSlotBody"],
                    _loc["Feedback.FixCredentialSlotHelp"],
                    PulsarNotificationIcon.Warning);
            }

            if (ContainsAny(message, "restore focus", "text entry", "key entry", "injection failed", "hide launcher"))
            {
                return new ActionFeedback(
                    ActionFeedbackKind.RecoverableFailure,
                    _loc["Feedback.CredentialFillFailed"],
                    _loc["Feedback.CredentialFillFailedBody"],
                    _loc["Feedback.CredentialFillFailedHelp"],
                    PulsarNotificationIcon.Error);
            }

            return new ActionFeedback(
                ActionFeedbackKind.RecoverableFailure,
                _loc["Feedback.CredentialFillFailed2"],
                _loc["Feedback.CredentialFillFailed2Body"],
                _loc["Feedback.CredentialFillFailed2Help"],
                PulsarNotificationIcon.Error);
        }

        private ActionFeedback CreateBookmarkletFailure(string? message)
        {
            if (ContainsAny(message,
                "Missing required parameter: scriptPath", "缺少必要参数: scriptPath",
                "Script path contains unsafe characters", "脚本路径包含不安全字符",
                "Script validation failed", "脚本验证失败",
                "Script content is empty", "脚本内容为空",
                "Script file not found", "找不到脚本文件"))
            {
                return new ActionFeedback(
                    ActionFeedbackKind.ConfigurationError,
                    _loc["Feedback.FixBookmarkletSlot"],
                    _loc["Feedback.FixBookmarkletSlotBody"],
                    _loc["Feedback.FixBookmarkletSlotHelp"],
                    PulsarNotificationIcon.Warning);
            }

            if (ContainsAny(message,
                "browser address bar", "浏览器地址栏",
                "No running browser detected", "未检测到运行中的浏览器",
                "Failed to focus browser",
                "Error executing bookmarklet script", "执行书签脚本时出错"))
            {
                return new ActionFeedback(
                    ActionFeedbackKind.RecoverableFailure,
                    _loc["Feedback.BookmarkletFailed"],
                    _loc["Feedback.BookmarkletFailedBody"],
                    _loc["Feedback.BookmarkletFailedHelp"],
                    PulsarNotificationIcon.Error);
            }

            return new ActionFeedback(
                ActionFeedbackKind.RecoverableFailure,
                _loc["Feedback.BookmarkletFailed2"],
                _loc["Feedback.BookmarkletFailed2Body"],
                _loc["Feedback.BookmarkletFailed2Help"],
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
