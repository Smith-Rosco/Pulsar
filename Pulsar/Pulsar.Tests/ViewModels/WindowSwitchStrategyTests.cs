using System;
using System.Collections.ObjectModel;
using System.Reflection;
using FluentAssertions;
using Moq;
using Pulsar.Models;
using Pulsar.Services.Interfaces;
using Pulsar.ViewModels;
using Pulsar.ViewModels.Strategies;
using Pulsar.Services;
using Pulsar.Native;

namespace Pulsar.Tests.ViewModels
{
    public class WindowSwitchStrategyTests
    {
        [Fact]
        public async Task ExecuteAsync_ShouldHideMenuBeforeAttemptingActivation()
        {
            var windowService = new Mock<IWindowService>();
            RadialMenuViewModel? observedContext = null;

            windowService
                .Setup(service => service.ActivateWindow(It.IsAny<ProcessWindowInfo>()))
                .Callback(() => observedContext!.IsVisible.Should().BeFalse())
                .Returns(true);

            var strategy = new WindowSwitchStrategy(CreateWindow(), windowService.Object);
            var context = CreateContext();
            context.IsVisible = true;
            observedContext = context;

            await strategy.ExecuteAsync(new SlotViewModel(1, 0, 0, 40), context);

            context.IsVisible.Should().BeFalse();
            windowService.Verify(service => service.ActivateWindow(It.IsAny<ProcessWindowInfo>()), Times.Once);
        }

        private static ProcessWindowInfo CreateWindow()
        {
            return new ProcessWindowInfo
            {
                Handle = new IntPtr(42),
                ProcessName = "testapp",
                Title = "Test Window"
            };
        }

        private static RadialMenuViewModel CreateContext()
        {
            var context = (RadialMenuViewModel)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(RadialMenuViewModel));
            var mouseTrackingService = new Mock<IMouseTrackingService>();
            var hotkeyService = new Mock<IHotkeyService>();

            SetField(context, "_mouseTrackingService", mouseTrackingService.Object);
            SetField(context, "_hotkeyService", hotkeyService.Object);
            SetField(context, "<Slots>k__BackingField", new ObservableCollection<SlotViewModel>());
            SetField(context, "_isVisible", true);

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
