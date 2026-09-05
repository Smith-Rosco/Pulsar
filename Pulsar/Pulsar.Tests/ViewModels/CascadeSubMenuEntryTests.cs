using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Pulsar.Core.Localization;
using Pulsar.Core.Plugin;
using Pulsar.Core.Plugin.Metadata;
using Pulsar.Models;
using Pulsar.Native;
using Pulsar.Services;
using Pulsar.Services.ActionFeedback;
using Pulsar.Services.Interfaces;
using Pulsar.Tests.TestHelpers;
using Pulsar.ViewModels;
using Pulsar.ViewModels.Strategies;
using Xunit;

namespace Pulsar.Tests.ViewModels
{
    /// <summary>
    /// Entry-point tests for the cascade drill-in path: left-clicking a root slot
    /// with sub-actions opens a <see cref="CascadeSubMenuDescriptor"/>, an empty slot
    /// executes its own action, and modifier-release still executes the slot action.
    /// </summary>
    public class CascadeSubMenuEntryTests
    {
        [Fact]
        public async Task LeftClick_CascadeSlot_ShouldOpenCascadeSubMenu()
        {
            var (session, _, pluginSlot) = CreateSession();
            pluginSlot.CascadeLayoutStyle = SubMenuLayoutStyle.Ring;
            pluginSlot.SubActions =
            [
                new SubSlotDescriptor("com.pulsar.command", "sendkeys", new Dictionary<string, string> { ["keys"] = "^c" }, "Copy", string.Empty, string.Empty)
            ];

            var slot = session.Slots.First(s => s.SlotIndex == 1);
            slot.DataContext = pluginSlot;
            slot.SubSlots.Clear();
            foreach (var sub in pluginSlot.SubActions)
            {
                slot.SubSlots.Add(sub);
            }
            slot.Label = "Clipboard";
            slot.IsEnabled = true;
            slot.ActionStrategy = new NoOpStrategy();

            session.IsVisible = true;
            await session.HandleGlobalMouseClickAsync(GlobalMouseButton.Left, clickSlotIndex: 1, new Vector(150, 150));

            session.IsInSubMenu.Should().BeTrue();
        }

        [Fact]
        public async Task LeftClick_EmptySlot_ShouldExecuteActionNotOpenCascade()
        {
            var (session, executor, pluginSlot) = CreateSession();
            pluginSlot.SubActions = null;

            var slot = session.Slots.First(s => s.SlotIndex == 1);
            slot.DataContext = pluginSlot;
            slot.Label = "Open Target";
            slot.IsEnabled = true;
            slot.ActionStrategy = new PluginActionStrategy(
                pluginSlot, executor.Object, null!,
                Mock.Of<ITrayService>(), Mock.Of<IActionFeedbackService>());

            session.IsVisible = true;
            await session.HandleGlobalMouseClickAsync(GlobalMouseButton.Left, clickSlotIndex: 1, new Vector(150, 150));

            executor.Verify(exec => exec.ExecuteAsync(
                pluginSlot.PluginId,
                pluginSlot.Action,
                pluginSlot.Args,
                It.IsAny<Pulsar.Core.Plugin.PulsarContext>(),
                It.IsAny<CancellationToken>()), Times.Once);
            session.IsInSubMenu.Should().BeFalse();
        }

        [Fact]
        public async Task ModifierRelease_CascadeSlot_ShouldExecuteSlotAction()
        {
            var (session, executor, pluginSlot) = CreateSession();
            pluginSlot.CascadeLayoutStyle = SubMenuLayoutStyle.Fan;
            pluginSlot.SubActions =
            [
                new SubSlotDescriptor("com.pulsar.command", "sendkeys", new Dictionary<string, string> { ["keys"] = "^v" }, "Paste", string.Empty, string.Empty)
            ];

            var slot = session.Slots.First(s => s.SlotIndex == 1);
            slot.DataContext = pluginSlot;
            slot.SubSlots.Clear();
            foreach (var sub in pluginSlot.SubActions)
            {
                slot.SubSlots.Add(sub);
            }
            slot.Label = "Clipboard";
            slot.IsEnabled = true;
            slot.ActionStrategy = new PluginActionStrategy(
                pluginSlot, executor.Object, null!,
                Mock.Of<ITrayService>(), Mock.Of<IActionFeedbackService>());

            session.IsVisible = true;
            session.UpdateActiveSlot(1);
            await session.HandleModifierRelease(
                new QuickSwitchPolicy { MaxDuration = TimeSpan.Zero, CenterZoneRadius = 0 },
                isLoading: false);

            executor.Verify(exec => exec.ExecuteAsync(
                pluginSlot.PluginId,
                pluginSlot.Action,
                pluginSlot.Args,
                It.IsAny<Pulsar.Core.Plugin.PulsarContext>(),
                It.IsAny<CancellationToken>()), Times.Once);
            session.IsInSubMenu.Should().BeFalse();
        }

        private static (MenuSession, Mock<IPluginExecutor>, PluginSlot) CreateSession()
        {
            var pluginRegistry = new Mock<IPluginRegistry>();
            pluginRegistry
                .Setup(registry => registry.IsPluginEnabled(It.IsAny<string>()))
                .Returns(true);

            // 执行面 seam：PluginActionStrategy 现在只依赖 IPluginExecutor。
            var executor = new Mock<IPluginExecutor>();
            executor
                .Setup(exec => exec.ExecuteAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, string>>(),
                    It.IsAny<Pulsar.Core.Plugin.PulsarContext>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Pulsar.Core.Plugin.PluginResult.Ok());

            var metadataRegistry = new Mock<IPluginMetadataRegistry>();
            metadataRegistry
                .Setup(registry => registry.GetActionMetadata(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(new SlotActionMetadata { Name = "sendkeys" });
            metadataRegistry
                .Setup(registry => registry.GetAllMetadata())
                .Returns(new List<PluginMetadata>());

            var windowService = new Mock<IWindowService>();
            var previewService = new Mock<IPreviewService>();

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
            slotLayoutEngine
                .Setup(engine => engine.CalculateOptimalRadius(It.IsAny<int>(), It.IsAny<double>(), It.IsAny<double>()))
                .Returns(120);

            var animationController = new Mock<IAnimationController>();
            animationController
                .Setup(controller => controller.AnimateLayoutAsync(It.IsAny<LayoutTarget>(), It.IsAny<AnimationOptions?>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var configService = new Mock<IConfigService>();
            configService
                .Setup(service => service.GetValidatedSlotsPerPage())
                .Returns(8);
            configService
                .Setup(service => service.GetSnapshot())
                .Returns(new ProfilesConfig());

            // [Architecture review 2026-09-05, candidate 1] Submenu strategies are now
            // an explicit ctor param (was resolved from IServiceProvider).
            var subMenuStrategies = new ISubMenuStrategy[]
            {
                new WindowSwitchSubMenuStrategy(windowService.Object),
                new CascadeSubMenuStrategy(
                    executor.Object,
                    metadataRegistry.Object,
                    Mock.Of<ITrayService>(),
                    Mock.Of<IActionFeedbackService>(),
                    logger: Mock.Of<ILogger<CascadeSubMenuStrategy>>())
            };

            // Factory returns real CommandPageProvider with null optional deps.
            var pageProviderFactory = new Mock<IPageProviderFactory>();
            pageProviderFactory.Setup(f => f.CreateCommandPage(It.IsAny<List<PluginSlot>>(), It.IsAny<PulsarContext>()))
                .Returns((List<PluginSlot> slots, PulsarContext ctx) =>
                    new CommandPageProvider(slots, Mock.Of<IPluginRegistry>(), ctx, Mock.Of<ITrayService>(), configService.Object,
                        Mock.Of<IPluginExecutor>(), Mock.Of<IActionFeedbackService>(), null, null, null, null));

            var loc = new Mock<ILocalizationService>();
            loc.Setup(l => l["RadialMenu.Pulsar"]).Returns("Pulsar");
            loc.Setup(l => l["RadialMenu.Back"]).Returns("Back");
            loc.Setup(l => l["Notification.Cancel"]).Returns("Cancel");

            var session = new MenuSession(
                configService.Object,
                windowService.Object,
                Mock.Of<IWindowInventoryCoordinator>(),
                new Mock<IHotkeyService>().Object,
                new Mock<ITrayService>().Object,
                animationController.Object,
                slotLayoutEngine.Object,
                new Mock<IPagingController>().Object,
                previewService.Object,
                pageProviderFactory.Object,
                loc.Object,
                new DirectUiDispatcher(),
                subMenuStrategies: subMenuStrategies);

            session.Initialize();

            var pluginSlot = new PluginSlot
            {
                Slot = 1,
                PluginId = "com.pulsar.command",
                Action = "sendkeys",
                Label = "Clipboard",
                Args = new Dictionary<string, string>()
            };

            return (session, executor, pluginSlot);
        }

        /// <summary>Direct-call fake so MenuSession tests need no WPF Application.</summary>
    }
}
