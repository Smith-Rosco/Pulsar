using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using FluentAssertions;
using Moq;
using Pulsar.Models;
using Pulsar.Services.Interfaces;
using Pulsar.ViewModels;
using Pulsar.ViewModels.Strategies;
using Xunit;

namespace Pulsar.Tests.ViewModels
{
    /// <summary>
    /// Behavior-preservation tests for <see cref="WindowSwitchSubMenuStrategy"/> — the
    /// window-submenu configuration extracted from the old coordinator. Asserts the
    /// exact slot wiring the pre-change code produced.
    /// </summary>
    public class WindowSwitchSubMenuStrategyTests
    {
        [Fact]
        public void ConfigureSubMenu_ShouldSetCenterAsBackActionStrategy()
        {
            var (context, descriptor, _) = CreateScenario(2, pageIndex: 0);

            context.Strategy.ConfigureSubMenu(context.Context, descriptor);

            context.Context.CenterSlot.ActionStrategy.Should().BeOfType<BackActionStrategy>();
            context.Context.CenterSlot.Label.Should().Be("testapp");
            context.Context.CenterSlot.Type.Should().Be(SlotType.Action);
        }

        [Fact]
        public void ConfigureSubMenu_ShouldAssignWindowSwitchStrategy_ToWindowSlots()
        {
            var (context, descriptor, _) = CreateScenario(2, pageIndex: 0);

            context.Strategy.ConfigureSubMenu(context.Context, descriptor);

            context.Slots[0].ActionStrategy.Should().BeOfType<WindowSwitchStrategy>();
            context.Slots[0].Type.Should().Be(SlotType.Window);
            context.Slots[0].DataContext.Should().BeOfType<ProcessWindowInfo>();
        }

        [Fact]
        public void ConfigureSubMenu_ShouldAssignNoOpStrategy_ToEmptyPageSlots()
        {
            var (context, descriptor, _) = CreateScenario(windows: 1, pageIndex: 1);

            context.Strategy.ConfigureSubMenu(context.Context, descriptor);

            context.Slots[1].ActionStrategy.Should().BeOfType<NoOpStrategy>();
            context.Slots[1].Type.Should().Be(SlotType.None);
            context.Slots[1].Label.Should().BeEmpty();
        }

        [Fact]
        public void ConfigureSubMenu_ShouldApplyColorPalette_ForMultiWindowGroups()
        {
            var (context, descriptor, _) = CreateScenario(2, pageIndex: 0);

            context.Strategy.ConfigureSubMenu(context.Context, descriptor);

            context.Slots[0].CustomStrokeBrush.Should().NotBeNull("palette applies a stroke token to window slots");
        }

        [Fact]
        public void ConfigureSubMenu_ShouldNotApplyPalette_ForSingleWindow()
        {
            var (context, descriptor, _) = CreateScenario(1, pageIndex: 0);

            context.Strategy.ConfigureSubMenu(context.Context, descriptor);

            context.Slots[0].CustomStrokeBrush.Should().BeNull("a single window keeps the default palette");
        }

        [Fact]
        public void ConfigureSubMenu_ShouldRequestSubMenuDefaultSelection()
        {
            var (context, descriptor, windowService) = CreateScenario(2, pageIndex: 0);

            context.Strategy.ConfigureSubMenu(context.Context, descriptor);

            windowService.Verify(service => service.SelectTargetWindow(
                It.IsAny<List<ProcessWindowInfo>>(),
                It.Is<WindowSelectionRequest?>(r => r != null && r.Intent == WindowSelectionIntent.SubMenuDefault)),
                Times.Once);
        }

        [Fact]
        public void ConfigureSubMenu_ShouldReturnSelectedWindow_ForPreviewPriming()
        {
            var (context, descriptor, windowService) = CreateScenario(2, pageIndex: 0);
            var expected = descriptor.Windows[1];
            windowService
                .Setup(service => service.SelectTargetWindow(It.IsAny<List<ProcessWindowInfo>>(), It.IsAny<WindowSelectionRequest?>()))
                .Returns(new WindowSelectionResult { Request = new WindowSelectionRequest(), SelectedWindow = expected, DecisionReason = "test" });

            var selected = context.Strategy.ConfigureSubMenu(context.Context, descriptor);

            selected.Should().BeSameAs(expected);
        }

        [Fact]
        public void ConfigureSubMenu_ShouldRejectNonWindowDescriptor()
        {
            var (context, _, _) = CreateScenario(1, pageIndex: 0);

            var selected = context.Strategy.ConfigureSubMenu(
                context.Context,
                new CascadeSubMenuDescriptor(new List<SubSlotDescriptor>()));

            selected.Should().BeNull();
        }

        private static (ScenarioData, WindowSubMenuDescriptor, Mock<IWindowService>) CreateScenario(
            int windows,
            int pageIndex)
        {
            var windowService = new Mock<IWindowService>();
            windowService
                .Setup(service => service.GetPreviousWindow())
                .Returns(new IntPtr(101));
            windowService
                .Setup(service => service.SelectTargetWindow(It.IsAny<List<ProcessWindowInfo>>(), It.IsAny<WindowSelectionRequest?>()))
                .Returns(new WindowSelectionResult { Request = new WindowSelectionRequest(), SelectedWindow = null });

            var strategy = new WindowSwitchSubMenuStrategy(windowService.Object);
            var centerSlot = new SlotViewModel(0, 0, 0, 60);
            var slots = new ObservableCollection<SlotViewModel>();
            for (int i = 1; i <= 8; i++)
            {
                slots.Add(new SlotViewModel(i, 0, 0, 50));
            }

            var windowList = new List<ProcessWindowInfo>();
            for (int i = 0; i < windows; i++)
            {
                windowList.Add(new ProcessWindowInfo
                {
                    Handle = new IntPtr(100 + i),
                    ProcessName = "testapp",
                    Title = $"Window {i}",
                    FirstSeenTime = new DateTime(2026, 1, 1, 9, i, 0)
                });
            }

            var descriptor = new WindowSubMenuDescriptor("testapp", windowList);
            var context = new SubMenuContext(centerSlot, slots, slotsPerPage: 8, pageIndex);

            return (new ScenarioData(strategy, context, slots), descriptor, windowService);
        }

        private sealed record ScenarioData(
            WindowSwitchSubMenuStrategy Strategy,
            SubMenuContext Context,
            ObservableCollection<SlotViewModel> Slots);
    }
}
