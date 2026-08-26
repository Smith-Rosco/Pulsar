using System.Collections.Generic;
using FluentAssertions;
using Pulsar.Services.WindowSwitching;
using Xunit;

namespace Pulsar.Tests.Services
{
    public class WindowEligibilityRuleTests
    {
        [Fact]
        public void Serialize_ThenParse_ShouldRoundTrip()
        {
            var rules = new List<WindowEligibilityRule>
            {
                new(false, "chrome", "Chrome_WidgetWin_1", "^Chrome Legacy Window$"),
                new(true, "wps", "KxWppQuickHelpBarContainer", null)
            };

            var json = WindowEligibilityRuleSerializer.Serialize(rules);
            var parsed = WindowEligibilityRuleSerializer.TryParse(json);

            parsed.Should().NotBeNull();
            var result = parsed!;
            result.Should().HaveCount(2);
            result[0].Allow.Should().BeFalse();
            result[0].ProcessName.Should().Be("chrome");
            result[0].WindowClass.Should().Be("Chrome_WidgetWin_1");
            result[0].TitlePattern.Should().Be("^Chrome Legacy Window$");
            result[1].Allow.Should().BeTrue();
        }

        [Fact]
        public void TryParse_InvalidJson_ShouldReturnNull()
        {
            WindowEligibilityRuleSerializer.TryParse("{ not json").Should().BeNull();
        }

        [Fact]
        public void TryParse_EmptyOrNull_ShouldReturnEmptyList()
        {
            WindowEligibilityRuleSerializer.TryParse(null).Should().BeEmpty();
            WindowEligibilityRuleSerializer.TryParse("").Should().BeEmpty();
            WindowEligibilityRuleSerializer.TryParse("   ").Should().BeEmpty();
        }

        [Fact]
        public void TryParse_ShouldDropNonIdentityRules()
        {
            var json = "[{\"Allow\":false,\"ProcessName\":\"notepad\"},{\"Allow\":false,\"WindowClass\":\"X\"}]";

            var parsed = WindowEligibilityRuleSerializer.TryParse(json);

            parsed.Should().NotBeNull();
            var result = parsed!;
            result.Should().HaveCount(1);
            result[0].WindowClass.Should().Be("X");
        }
    }
}
