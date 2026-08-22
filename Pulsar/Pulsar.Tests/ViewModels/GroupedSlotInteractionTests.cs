using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using FluentAssertions;
using Moq;
using Pulsar.Core.Localization;
using Pulsar.Models;
using Pulsar.Native;
using Pulsar.Services;
using Pulsar.Services.Interfaces;
using Pulsar.ViewModels;
using Pulsar.ViewModels.Strategies;
using Xunit;

namespace Pulsar.Tests.ViewModels
{
    /// <summary>
    /// Interaction-policy tests for the grouped (process) slot. These now construct
    /// <see cref="MenuSession"/> directly with mocks — no WPF shell, no reflection.
    /// </summary>
    public class GroupedSlotInteractionTests
    {
        [Fact]
        public async Task ProcessGroupStrategy_ShouldUseGroupedRootDirectTrigger_ForModifierReleaseExecution()
        {
            var windows = CreateWindows();
            var windowService = new Mock<IWindowService>();
            WindowSelectionRequest? capturedRequest = null;

            windowService
                .Setup(service => service.GetPreviousWindow())
                .Returns(new IntPtr(101));

            windowService
                .Setup(service => service.SelectTargetWindow(It.IsAny<List<ProcessWindowInfo>>(), It.IsAny<WindowSelectionRequest?>()))
                .Callback<List<ProcessWindowInfo>, WindowSelectionRequest?>((_, request) => capturedRequest = request)
                .Returns(new WindowSelectionResult
                {
                    Request = new WindowSelectionRequest(),
                    SelectedWindow = windows[1],
                    DecisionReason = "test"
                });

            windowService
                .Setup(service => service.ActivateWindow(It.IsAny<ProcessWindowInfo>()))
                .Returns(true);

            var strategy = new ProcessGroupStrategy(windows, windowService.Object);
            var context = new Mock<IMenuSession>();
            context.SetupProperty(c => c.IsVisible, true);

            await strategy.ExecuteAsync(new SlotViewModel(1, 0, 0, 40), context.Object);

            capturedRequest.Should().NotBeNull();
            capturedRequest!.Intent.Should().Be(WindowSelectionIntent.GroupedRootDirectTrigger);
            capturedRequest.SkipMode.Should().Be(WindowSelectionSkipMode.None);
            capturedRequest.CurrentForegroundHandle.Should().Be(new IntPtr(101));
        }

        [Fact]
        public async Task HandleGlobalMouseClickAsync_ShouldEnterSubMenu_ForGroupedSlotLeftClick()
        {
            var windowService = new Mock<IWindowService>();
            var previewService = new Mock<IPreviewService>();
            var session = CreateSession(windowService, previewService);
            var windows = CreateWindows();

            windowService
                .Setup(service => service.SelectTargetWindow(It.IsAny<List<ProcessWindowInfo>>(), It.IsAny<WindowSelectionRequest?>()))
                .Returns(new WindowSelectionResult
                {
                    Request = new WindowSelectionRequest(),
                    SelectedWindow = windows[1],
                    DecisionReason = "test"
                });

            var slot = session.Slots.First(s => s.SlotIndex == 1);
            slot.Label = "testapp";
            slot.Type = SlotType.Process;
            slot.DataContext = windows;
            slot.ActionStrategy = new ProcessGroupStrategy(windows, windowService.Object);

            session.IsVisible = true;

            await session.HandleGlobalMouseClickAsync(GlobalMouseButton.Left, clickSlotIndex: 1, new Vector(150, 150));

            session.IsInSubMenu.Should().BeTrue();
            windowService.Verify(service => service.SelectTargetWindow(It.IsAny<List<ProcessWindowInfo>>(), It.IsAny<WindowSelectionRequest?>()), Times.Once);
            windowService.Verify(service => service.ActivateWindow(It.IsAny<ProcessWindowInfo>()), Times.Never);
        }

        [Fact]
        public async Task HandleGlobalMouseClickAsync_ShouldHideMenu_ForRootCenterLeftClick()
        {
            var windowService = new Mock<IWindowService>();
            var session = CreateSession(windowService, new Mock<IPreviewService>());

            session.IsVisible = true;
            await session.HandleGlobalMouseClickAsync(GlobalMouseButton.Left, clickSlotIndex: 0, new Vector(150, 150));

            session.IsVisible.Should().BeFalse();
        }

        [Fact]
        public async Task HandleGlobalMouseClickAsync_ShouldHideMenu_ForRootRightClick()
        {
            var windowService = new Mock<IWindowService>();
            var session = CreateSession(windowService, new Mock<IPreviewService>());

            session.IsVisible = true;
            await session.HandleGlobalMouseClickAsync(GlobalMouseButton.Right, clickSlotIndex: -1, new Vector(150, 150));

            session.IsVisible.Should().BeFalse();
        }

        private static MenuSession CreateSession(Mock<IWindowService> windowService, Mock<IPreviewService> previewService)
        {
            var slotLayoutEngine = new Mock<ISlotLayoutEngine>();
            slotLayoutEngine
                .Setup(engine => engine.CalculateOptimalLayout(It.IsAny<int>()))
                .Returns(new LayoutParameters(250, 250, 120, 0, 8));
            slotLayoutEngine
                .Setup(engine => engine.GetSlotPosition(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<LayoutParameters>()))
                .Returns((0d, 0d));
            slotLayoutEngine
                .Setup(engine => engine.HitTest(It.IsAny<Vector>(), It.IsAny<LayoutParameters>()))
                .Returns(-1);

            var animationController = new Mock<IAnimationController>();
            animationController
                .Setup(controller => controller.AnimateLayoutAsync(It.IsAny<LayoutTarget>(), It.IsAny<AnimationOptions?>(), It.IsAny<System.Threading.CancellationToken>()))
                .Returns(Task.CompletedTask);

            var configService = new Mock<IConfigService>();
            configService
                .Setup(service => service.GetValidatedSlotsPerPage())
                .Returns(8);
            configService
                .Setup(service => service.GetSnapshot())
                .Returns(new ProfilesConfig());

            var hotkeyService = new Mock<IHotkeyService>();
            var trayService = new Mock<ITrayService>();
            var pagingController = new Mock<IPagingController>();
            var serviceProvider = new Mock<System.IServiceProvider>();

            var loc = new Mock<ILocalizationService>();
            loc.Setup(l => l["RadialMenu.Pulsar"]).Returns("Pulsar");
            loc.Setup(l => l["RadialMenu.Back"]).Returns("Back");
            loc.Setup(l => l["Notification.Cancel"]).Returns("Cancel");

            var session = new MenuSession(
                configService.Object,
                windowService.Object,
                Mock.Of<IPluginRegistry>(),
                hotkeyService.Object,
                trayService.Object,
                animationController.Object,
                slotLayoutEngine.Object,
                pagingController.Object,
                previewService.Object,
                serviceProvider.Object,
                loc.Object,
                new DirectUiDispatcher());

            session.Initialize();
            return session;
        }

        private static List<ProcessWindowInfo> CreateWindows()
        {
            return
            [
                new ProcessWindowInfo
                {
                    Handle = new IntPtr(101),
                    ProcessName = "testapp",
                    Title = "First Window",
                    FirstSeenTime = new DateTime(2026, 1, 1, 9, 0, 0),
                    RealActivationTime = new DateTime(2026, 1, 1, 10, 0, 0)
                },
                new ProcessWindowInfo
                {
                    Handle = new IntPtr(202),
                    ProcessName = "testapp",
                    Title = "Second Window",
                    FirstSeenTime = new DateTime(2026, 1, 1, 10, 0, 0),
                    RealActivationTime = new DateTime(2026, 1, 1, 11, 0, 0)
                }
            ];
        }

        /// <summary>Direct-call fake so MenuSession tests need no WPF Application.</summary>
        private sealed class DirectUiDispatcher : IUiDispatcher
        {
            public bool CheckAccess() => true;
            public void Invoke(Action action) => action();
            public Task InvokeAsync(Action action)
            {
                action();
                return Task.CompletedTask;
            }

            public Task BeginInvoke(Action action)
            {
                action();
                return Task.CompletedTask;
            }
        }
    }
}
