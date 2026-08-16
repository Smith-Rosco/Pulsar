using System.Windows;
using FluentAssertions;
using Pulsar.Services;
using Xunit;

namespace Pulsar.Tests.Services
{
    public class MenuViewportServiceTests
    {
        private static readonly Rect WorkArea = new(0, 0, 3840, 2160);

        [Fact]
        public void ClampMenuCenter_ShouldFollowCursor_WhenThereIsEnoughMargin()
        {
            var center = MenuViewportService.ClampMenuCenter(WorkArea, new Point(1500, 1000), 300);

            center.Should().Be(new Point(1500, 1000));
        }

        [Fact]
        public void ClampMenuCenter_ShouldPushMenuInsideWorkArea_AtLeftTopEdge()
        {
            var center = MenuViewportService.ClampMenuCenter(WorkArea, new Point(5, 10), 300);

            center.X.Should().BeApproximately(300, 0.001);
            center.Y.Should().BeApproximately(300, 0.001);
        }

        [Fact]
        public void ClampMenuCenter_ShouldCenterMenu_WhenWorkAreaIsSmallerThanMenu()
        {
            var smallWorkArea = new Rect(100, 100, 400, 400);

            var center = MenuViewportService.ClampMenuCenter(smallWorkArea, new Point(100, 100), 300);

            center.X.Should().BeApproximately(300, 0.001);
            center.Y.Should().BeApproximately(300, 0.001);
        }

        [Fact]
        public void RequiresPointerWarp_ShouldOnlyTrigger_WhenCenterMoved()
        {
            MenuViewportService.RequiresPointerWarp(new Point(200, 200), new Point(200, 200))
                .Should().BeFalse();

            MenuViewportService.RequiresPointerWarp(new Point(300, 200), new Point(200, 200))
                .Should().BeTrue();
        }
    }
}
