using FluentAssertions;
using Pulsar.Services;
using Xunit;

namespace Pulsar.Tests.Services
{
    public class WindowPlacementServiceTests
    {
        [Fact]
        public void ToDip_ConvertsPhysicalPixelsToDeviceIndependentUnits()
        {
            var service = new WindowPlacementService();

            var point = service.ToDip(300, 150, 1.5, 2.0);

            point.X.Should().Be(200);
            point.Y.Should().Be(75);
        }
    }
}
