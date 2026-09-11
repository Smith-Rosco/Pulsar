using System.Windows;
using FluentAssertions;
using Pulsar.Services;
using Pulsar.Services.Interfaces;
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
        public void ClampMenuCenter_ShouldHonorConfiguredMargin()
        {
            // A user-set margin moves the corrected center exactly that far from the edge.
            var center = MenuViewportService.ClampMenuCenter(WorkArea, new Point(5, 10), 120);

            center.X.Should().BeApproximately(120, 0.001);
            center.Y.Should().BeApproximately(120, 0.001);
        }

        [Fact]
        public void ClampMenuCenter_ShouldAllowCenterOnEdge_WhenMarginIsZero()
        {
            var center = MenuViewportService.ClampMenuCenter(WorkArea, new Point(-50, -50), 0);

            center.X.Should().BeApproximately(0, 0.001);
            center.Y.Should().BeApproximately(0, 0.001);
        }

        [Fact]
        public void ClampMenuCenter_ShouldFloorNegativeMargin_InsteadOfInvertingRange()
        {
            // Config could hold anything; a negative margin must not let the center leave
            // the work area (Math.Clamp would otherwise silently invert its range).
            var center = MenuViewportService.ClampMenuCenter(WorkArea, new Point(-50, -50), -40);

            center.X.Should().BeApproximately(0, 0.001);
            center.Y.Should().BeApproximately(0, 0.001);
        }

        [Fact]
        public void ResolveMenuCenter_ShouldKeepMenuExtentInsideWorkArea_WhenNoPolicyGiven()
        {
            // null = "no user preference" → legacy behaviour (wheel extent as the margin).
            var center = MenuViewportService.ResolveMenuCenter(WorkArea, new Point(5, 10), 300, null);

            center.X.Should().BeApproximately(300, 0.001);
            center.Y.Should().BeApproximately(300, 0.001);
        }

        [Fact]
        public void ResolveMenuCenter_ShouldUseConfiguredMargin_WhenEdgeClampEnabled()
        {
            var options = new MenuEdgeClampOptions(Enabled: true, MarginDip: 210);

            var center = MenuViewportService.ResolveMenuCenter(WorkArea, new Point(5, 10), 260, options);

            center.X.Should().BeApproximately(210, 0.001);
            center.Y.Should().BeApproximately(210, 0.001);
        }

        [Fact]
        public void ResolveMenuCenter_ShouldReturnCursorUnchanged_WhenEdgeClampDisabled()
        {
            var cursor = new Point(3, 5);
            var options = new MenuEdgeClampOptions(Enabled: false, MarginDip: 260);

            var center = MenuViewportService.ResolveMenuCenter(WorkArea, cursor, 260, options);

            center.Should().Be(cursor);
            // No displacement → no pointer warp, so the menu stops "jumping" to the edge.
            MenuViewportService.RequiresPointerWarp(center, cursor).Should().BeFalse();
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
