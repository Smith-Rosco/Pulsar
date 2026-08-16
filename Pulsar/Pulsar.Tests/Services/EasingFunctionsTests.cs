using FluentAssertions;
using Pulsar.Services.Interfaces;
using Xunit;

namespace Pulsar.Tests.Services
{
    public class EasingFunctionsTests
    {
        [Theory]
        [InlineData(0.0, 0.0)]
        [InlineData(1.0, 1.0)]
        public void EaseOutBack_ShouldPinEndpoints(double input, double expected)
        {
            EasingFunctions.EaseOutBack(input).Should().BeApproximately(expected, 0.0001);
        }

        [Fact]
        public void EaseOutBack_ShouldOvershootBeforeSettling()
        {
            // The overshoot is what makes the submenu bloom feel alive.
            EasingFunctions.EaseOutBack(0.7).Should().BeGreaterThan(1.0);
        }

        [Theory]
        [InlineData(0.0, 0.0)]
        [InlineData(1.0, 1.0)]
        public void EaseInCubic_ShouldPinEndpoints(double input, double expected)
        {
            EasingFunctions.EaseInCubic(input).Should().BeApproximately(expected, 0.0001);
        }
    }
}
