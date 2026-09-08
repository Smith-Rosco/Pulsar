using FluentAssertions;
using Moq;
using Pulsar.Core.Localization;
using Pulsar.Services.Interfaces;
using Xunit;

namespace Pulsar.Tests.Localization
{
    public class PluginLocalizationTests
    {
        private static ILocalizationService CreateLoc(Func<string, string> resolver)
        {
            var mock = new Mock<ILocalizationService>();
            mock.Setup(l => l[It.IsAny<string>()]).Returns((string k) => resolver(k));
            return mock.Object;
        }

        [Fact]
        public void ConventionLookup_WhenKeyExists_ReturnsLocalizedValue()
        {
            var loc = CreateLoc(k => k == "SlotAction.RunCalc" ? "运行计算器" : k);

            var result = PluginLocalization.ConventionLookup(loc, "SlotAction.", "Run Calc!");

            result.Should().Be("运行计算器");
        }

        [Fact]
        public void ConventionLookup_WhenKeyMissing_ReturnsOriginalValue()
        {
            var loc = CreateLoc(k => k);

            var result = PluginLocalization.ConventionLookup(loc, "SlotAction.", "Run Calc");

            result.Should().Be("Run Calc");
        }

        [Fact]
        public void ConventionLookup_WithKeySource_GeneratesKeyFromKeySource()
        {
            var loc = CreateLoc(k => k == "Plugin.Description.Calculator" ? "计算器插件" : k);

            var result = PluginLocalization.ConventionLookup(loc, "Plugin.Description.", "Runs the calculator", "Calculator");

            result.Should().Be("计算器插件");
        }

        [Fact]
        public void ConventionLookup_NullLoc_ReturnsOriginalValue()
        {
            var result = PluginLocalization.ConventionLookup(null, "SlotAction.", "Run Calc");

            result.Should().Be("Run Calc");
        }

        [Fact]
        public void ConventionLookup_EmptyOrNullValue_ReturnsEmpty()
        {
            var loc = CreateLoc(k => k);

            PluginLocalization.ConventionLookup(loc, "SlotAction.", "").Should().Be("");
            PluginLocalization.ConventionLookup(loc, "SlotAction.", null).Should().Be("");
        }

        [Fact]
        public void LocalizePluginName_DelegatesToConvention()
        {
            var loc = CreateLoc(k => k == "Plugin.Name.Calculator" ? "计算器" : k);

            var result = PluginLocalization.LocalizePluginName(loc, "Calculator");

            result.Should().Be("计算器");
        }
    }
}
