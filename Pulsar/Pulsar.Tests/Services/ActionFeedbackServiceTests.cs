using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Pulsar.Core.Localization;
using Pulsar.Core.Plugin;
using Pulsar.Services.ActionFeedback;

namespace Pulsar.Tests.Services
{
    public class ActionFeedbackServiceTests
    {
        private readonly LocalizationService _loc = new(
            new Mock<ILogger<LocalizationService>>().Object);
        private ActionFeedbackService _service => new(_loc);

        [Fact]
        public void Create_ShouldReturnConfigurationError_ForWinSwitcherConfigProblems()
        {
            var result = PluginResult.Error("Missing required parameter: app");

            var feedback = _service.Create("com.pulsar.winswitcher", "switch", result);

            feedback.Kind.Should().Be(ActionFeedbackKind.ConfigurationError);
            feedback.Title.Should().Be("Fix slot setup");
            feedback.ToNotificationMessage().Should().Contain("Open the slot and verify the app name or launch path.");
        }

        [Fact]
        public void Create_ShouldReturnRecoverableFailure_ForWinSwitcherRuntimeProblems()
        {
            var result = PluginResult.Error("Process 'chrome' is not running");

            var feedback = _service.Create("com.pulsar.winswitcher", "switch", result);

            feedback.Kind.Should().Be(ActionFeedbackKind.RecoverableFailure);
            feedback.Title.Should().Be("App not available");
        }

        [Fact]
        public void Create_ShouldReturnConsistentConfigurationErrorTitle_AcrossSupportedPlugins()
        {
            var commandFeedback = _service.Create(
                "com.pulsar.command",
                "run",
                PluginResult.Error("Missing required parameter: path"));

            var pkiFeedback = _service.Create(
                "com.pulsar.pki",
                "fill",
                PluginResult.Error("Missing required parameter: secretId"));

            commandFeedback.Kind.Should().Be(ActionFeedbackKind.ConfigurationError);
            pkiFeedback.Kind.Should().Be(ActionFeedbackKind.ConfigurationError);
            commandFeedback.Title.Should().Be("Fix slot setup");
            pkiFeedback.Title.Should().Be("Fix credential slot");
            commandFeedback.Kind.Should().Be(pkiFeedback.Kind);
        }

        [Fact]
        public void Create_ShouldReturnTemporaryUnavailable_ForDisabledPluginOutcomes()
        {
            var feedback = _service.Create(
                "com.pulsar.command",
                "run",
                PluginResult.Error("Plugin disabled for safety. Try again in 42s."));

            feedback.Kind.Should().Be(ActionFeedbackKind.TemporaryUnavailable);
            feedback.Title.Should().Be("Action unavailable");
        }

        [Fact]
        public void Create_ShouldRedactPkiFailureMessage_FromRawSensitiveDetails()
        {
            var rawMessage = "Secret not found: 01234567-89ab-cdef-0123-456789abcdef";

            var feedback = _service.Create("com.pulsar.pki", "fill", PluginResult.Error(rawMessage));

            feedback.Kind.Should().Be(ActionFeedbackKind.ConfigurationError);
            feedback.Message.Should().NotContain("01234567-89ab-cdef-0123-456789abcdef");
            feedback.ToNotificationMessage().Should().NotContain("01234567-89ab-cdef-0123-456789abcdef");
            feedback.Message.Should().NotContain("Secret not found:");
        }

        [Fact]
        public void Create_ShouldReturnRetryGuidance_ForBookmarkletBrowserReadinessFailures()
        {
            var feedback = _service.Create(
                "com.pulsar.bookmarklet",
                "run",
                PluginResult.Error("浏览器地址栏暂时未准备好接受书签脚本。请等待页面或浏览器完成加载后重试。"));

            feedback.Kind.Should().Be(ActionFeedbackKind.RecoverableFailure);
            feedback.Title.Should().Be("Bookmarklet failed");
            feedback.ToNotificationMessage().Should().Contain("finish loading");
        }
    }
}
