using System.Windows;
using FluentAssertions;
using Pulsar.Services;
using Pulsar.Services.Interfaces;

namespace Pulsar.Tests.Services
{
    public class SlotLayoutEngineTests
    {
        private static readonly LayoutParameters EightSlotLayout = new(250, 250, 90, 54, 8);

        private readonly SlotLayoutEngine _engine = new();

        [Fact]
        public void HitTest_ShouldReturnCenter_WhenPointIsInsideDeadZone()
        {
            var result = _engine.HitTest(new Vector(250, 250), EightSlotLayout);

            result.Should().Be(0);
        }

        [Theory]
        [InlineData(250, 120, 1)]
        [InlineData(380, 250, 3)]
        [InlineData(250, 380, 5)]
        [InlineData(120, 250, 7)]
        public void HitTest_ShouldReturnExpectedSlot_ForPrimaryDirections(double x, double y, int expectedSlot)
        {
            var result = _engine.HitTest(new Vector(x, y), EightSlotLayout);

            result.Should().Be(expectedSlot);
        }

        [Theory]
        [InlineData(-23, 8)]
        [InlineData(-22, 1)]
        [InlineData(22, 1)]
        [InlineData(23, 2)]
        public void HitTest_ShouldAssignBoundaryPoints_ToNearestSector(int angleFromTopDegrees, int expectedSlot)
        {
            var point = CreatePointFromTopAngle(angleFromTopDegrees, 120);

            var result = _engine.HitTest(point, EightSlotLayout);

            result.Should().Be(expectedSlot);
        }

        [Fact]
        public void CalculateOptimalLayout_ShouldReturnExpectedDeadZone_ForEightSlots()
        {
            var layout = _engine.CalculateOptimalLayout(8);

            layout.CenterX.Should().Be(250);
            layout.CenterY.Should().Be(250);
            layout.Radius.Should().Be(90);
            layout.DeadZoneRadius.Should().Be(54);
            layout.TotalSlots.Should().Be(8);
        }

        private static Vector CreatePointFromTopAngle(double angleFromTopDegrees, double radius)
        {
            double angleRad = (angleFromTopDegrees - 90) * Math.PI / 180.0;
            double x = EightSlotLayout.CenterX + radius * Math.Cos(angleRad);
            double y = EightSlotLayout.CenterY + radius * Math.Sin(angleRad);
            return new Vector(x, y);
        }
    }
}
