using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Pulsar.Models;
using Pulsar.Services.Interfaces;
using Pulsar.ViewModels;
using Pulsar.ViewModels.Strategies;
using Xunit;

namespace Pulsar.Tests.ViewModels
{
    /// <summary>
    /// Strategy-host behavior for <see cref="RadialMenuSubMenuCoordinator"/>: routing by
    /// registered id, unknown-id fallback (log warning, never throw), and window-strategy
    /// selection for a <see cref="WindowSubMenuDescriptor"/>.
    /// </summary>
    public class SubMenuCoordinatorStrategyTests
    {
        [Fact]
        public void ConfigureSubMenu_ShouldRoute_ToRegisteredStrategy()
        {
            var fake = new RecordingSubMenuStrategy("fake-strategy");
            var coordinator = CreateCoordinator(fake);

            var result = coordinator.ConfigureSubMenu(
                new FakeDescriptor("fake-strategy"), 8, 0, CreateCenter(), CreateSlots());

            result.FallbackToRoot.Should().BeFalse();
            fake.Configured.Should().BeTrue();
        }

        [Fact]
        public void ConfigureSubMenu_ShouldFallBackToRoot_ForUnknownStrategy()
        {
            var logger = new Mock<ILogger>();
            var coordinator = new RadialMenuSubMenuCoordinator(
                Array.Empty<ISubMenuStrategy>(),
                logger.Object);

            var result = coordinator.ConfigureSubMenu(
                new FakeDescriptor("not-registered"), 8, 0, CreateCenter(), CreateSlots());

            result.FallbackToRoot.Should().BeTrue("an unregistered strategy must fall back to root, never throw");
            logger.Verify(l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
        }

        [Fact]
        public void ConfigureSubMenu_ShouldSelectWindowStrategy_ForWindowDescriptor()
        {
            var windows = new List<ProcessWindowInfo>
            {
                new ProcessWindowInfo { Handle = new IntPtr(100), ProcessName = "testapp", Title = "Window 0" },
                new ProcessWindowInfo { Handle = new IntPtr(101), ProcessName = "testapp", Title = "Window 1" }
            };
            var windowService = new Mock<IWindowService>();
            windowService
                .Setup(service => service.GetPreviousWindow())
                .Returns(new IntPtr(101));
            windowService
                .Setup(service => service.SelectTargetWindow(It.IsAny<List<ProcessWindowInfo>>(), It.IsAny<WindowSelectionRequest?>()))
                .Returns(new WindowSelectionResult { Request = new WindowSelectionRequest(), SelectedWindow = windows[1], DecisionReason = "test" });

            var coordinator = CreateCoordinator(new WindowSwitchSubMenuStrategy(windowService.Object));

            var result = coordinator.ConfigureSubMenu(
                new WindowSubMenuDescriptor("testapp", windows), 8, 0, CreateCenter(), CreateSlots());

            result.FallbackToRoot.Should().BeFalse();
            result.SelectedWindow.Should().BeSameAs(windows[1]);
            windowService.Verify(service => service.SelectTargetWindow(
                It.IsAny<List<ProcessWindowInfo>>(),
                It.Is<WindowSelectionRequest?>(r => r != null && r.Intent == WindowSelectionIntent.SubMenuDefault)), Times.Once);
        }

        [Fact]
        public void ConfigureSubMenu_ShouldLogAndFallback_ForNullDescriptor()
        {
            var logger = new Mock<ILogger>();
            var coordinator = new RadialMenuSubMenuCoordinator(
                Array.Empty<ISubMenuStrategy>(),
                logger.Object);

            var result = coordinator.ConfigureSubMenu(null!, 8, 0, CreateCenter(), CreateSlots());

            result.FallbackToRoot.Should().BeTrue();
            logger.Verify(l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
        }

        private static RadialMenuSubMenuCoordinator CreateCoordinator(params ISubMenuStrategy[] strategies)
        {
            return new RadialMenuSubMenuCoordinator(strategies, logger: null);
        }

        private static SlotViewModel CreateCenter() => new(0, 0, 0, 60);

        private static ObservableCollection<SlotViewModel> CreateSlots()
        {
            var slots = new ObservableCollection<SlotViewModel>();
            for (int i = 1; i <= 8; i++)
            {
                slots.Add(new SlotViewModel(i, 0, 0, 50));
            }

            return slots;
        }

        private sealed class FakeDescriptor : SubMenuDescriptor
        {
            private readonly string _strategyId;

            public FakeDescriptor(string strategyId)
            {
                _strategyId = strategyId;
            }

            public override string StrategyId => _strategyId;
        }

        private sealed class RecordingSubMenuStrategy : ISubMenuStrategy
        {
            public string StrategyId { get; }

            public bool Configured { get; private set; }

            public RecordingSubMenuStrategy(string strategyId)
            {
                StrategyId = strategyId;
            }

            public ProcessWindowInfo? ConfigureSubMenu(SubMenuContext context, SubMenuDescriptor descriptor)
            {
                Configured = true;
                return null;
            }
        }
    }
}
