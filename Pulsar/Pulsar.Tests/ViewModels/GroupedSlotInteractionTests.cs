using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reflection;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Pulsar.Models;
using Pulsar.Native;
using Pulsar.Services.Interfaces;
using Pulsar.ViewModels;
using Pulsar.ViewModels.Strategies;

namespace Pulsar.Tests.ViewModels
{
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
            var context = CreateContext(windowService.Object, Mock.Of<IPreviewService>());
            context.IsVisible = true;

            await strategy.ExecuteAsync(new SlotViewModel(1, 0, 0, 40), context);

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
            var coordinator = new RadialMenuInputCoordinator(windowService.Object, logger: null);
            var context = CreateContext(windowService.Object, previewService.Object);
            var windows = CreateWindows();

            windowService
                .Setup(service => service.SelectTargetWindow(It.IsAny<List<ProcessWindowInfo>>(), It.IsAny<WindowSelectionRequest?>()))
                .Returns(new WindowSelectionResult
                {
                    Request = new WindowSelectionRequest(),
                    SelectedWindow = windows[1],
                    DecisionReason = "test"
                });

            var slot = new SlotViewModel(1, 0, 0, 40)
            {
                Label = "testapp",
                Type = SlotType.Process,
                DataContext = windows,
                ActionStrategy = new ProcessGroupStrategy(windows, windowService.Object)
            };

            context.Slots.Add(slot);

            await coordinator.HandleGlobalMouseClickAsync(
                GlobalMouseButton.Left,
                isVisible: true,
                activeSlotIndex: 1,
                menuState: MenuState.Root,
                centerSlot: context.CenterSlot,
                slots: context.Slots,
                context: context,
                restoreRootMenu: context.RestoreRootMenu,
                triggerRootBounceAnimation: () => { },
                hideMenu: () => context.IsVisible = false);

            context.IsInSubMenu.Should().BeTrue();
            windowService.Verify(service => service.SelectTargetWindow(It.IsAny<List<ProcessWindowInfo>>(), It.IsAny<WindowSelectionRequest?>()), Times.Once);
            windowService.Verify(service => service.ActivateWindow(It.IsAny<ProcessWindowInfo>()), Times.Never);
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

        private static RadialMenuViewModel CreateContext(IWindowService windowService, IPreviewService previewService)
        {
            var context = (RadialMenuViewModel)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(RadialMenuViewModel));
            var mouseTrackingService = new Mock<IMouseTrackingService>();
            var pagingController = new Mock<IPagingController>();
            var animationController = new Mock<IAnimationController>();
            var slotLayoutEngine = new Mock<ISlotLayoutEngine>();

            slotLayoutEngine
                .Setup(engine => engine.CalculateOptimalLayout(It.IsAny<int>()))
                .Returns(new LayoutParameters(250, 250, 120, 0, 8));

            animationController
                .Setup(controller => controller.AnimateLayoutAsync(It.IsAny<LayoutTarget>(), It.IsAny<AnimationOptions?>(), It.IsAny<System.Threading.CancellationToken>()))
                .Returns(Task.CompletedTask);

            var hotkeyService = new Mock<IHotkeyService>();
            SetField(context, "_hotkeyService", hotkeyService.Object);
            SetField(context, "_mouseTrackingService", mouseTrackingService.Object);
            SetField(context, "_pagingController", pagingController.Object);
            SetField(context, "_animationController", animationController.Object);
            SetField(context, "_slotLayoutEngine", slotLayoutEngine.Object);
            SetField(context, "_previewService", previewService);
            SetField(context, "_visualStateCoordinator", new RadialMenuVisualStateCoordinator(previewService, null));
            SetField(context, "_subMenuCoordinator", new RadialMenuSubMenuCoordinator(windowService, null, null, null));
            SetField(context, "_layoutCoordinator", new RadialMenuLayoutCoordinator(slotLayoutEngine.Object, animationController.Object, null));
            SetField(context, "<Slots>k__BackingField", new ObservableCollection<SlotViewModel>());
            SetField(context, "<CenterSlot>k__BackingField", new SlotViewModel(0, 0, 0, 50));
            SetField(context, "_slotsPerPage", 8);
            SetField(context, "_currentCenterSize", 50d);
            SetField(context, "_currentSlotSize", 50d);
            SetField(context, "_currentRadius", 120d);
            SetField(context, "_isVisible", true);
            SetField(context, "_loc", new Pulsar.Core.Localization.LocalizationService(new Mock<Microsoft.Extensions.Logging.ILogger<Pulsar.Core.Localization.LocalizationService>>().Object));

            return context;
        }

        private static void SetField(object target, string fieldName, object? value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            field.Should().NotBeNull($"field {fieldName} must exist for test setup");
            field!.SetValue(target, value);
        }
    }
}
