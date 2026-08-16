using FluentAssertions;
using Pulsar.ViewModels;
using Xunit;

namespace Pulsar.Tests.ViewModels
{
    public class RadialMenuAnimationDurationTests
    {
        [Fact]
        public void GetSubMenuEnterDuration_ShouldRespectMinAndMaxBounds()
        {
            RadialMenuViewModel.GetSubMenuEnterDuration(0)
                .TotalMilliseconds.Should().BeApproximately(110, 1);

            RadialMenuViewModel.GetSubMenuEnterDuration(5000)
                .TotalMilliseconds.Should().BeApproximately(240, 1);
        }

        [Fact]
        public void GetSubMenuEnterDuration_FarClicksShouldTravelFaster()
        {
            var near = RadialMenuViewModel.GetSubMenuEnterDuration(80);
            var far = RadialMenuViewModel.GetSubMenuEnterDuration(600);

            double nearVelocity = 80 / near.TotalMilliseconds;
            double farVelocity = 600 / far.TotalMilliseconds;

            farVelocity.Should().BeGreaterThan(nearVelocity);
        }
    }
}
