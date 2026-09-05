// [Path]: Pulsar.Tests/ViewModels/CascadeSubMenuLayoutRuntimeTests.cs

using System;
using System.Collections.Generic;
using System.Linq;
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
    /// Runtime layout regression tests for the cascade drill-in path (Fan QA change,
    /// task 3.1). Uses the REAL slot layout engine and REAL sub-menu layout engine so
    /// the asserted coordinates are the ones the UI actually renders — the existing
    /// entry tests only assert IsInSubMenu and mock the engine away.
    /// Found by manual QA (2026-09-05): child slots were reported invisible after
    /// entering a cascade; these tests pin down whether the runtime X/Y ever leaves
    /// the root ring at all.
    /// </summary>
    public class CascadeSubMenuLayoutRuntimeTests
    {
        private const double CanvasCenter = 250.0;

        [Fact]
        public async Task EnterCascade_Fan2_ChildSlotsShouldLeaveRootRing()
        {
            var (session, _) = CreateSessionWithRealEngines();
            session.IsVisible = true;
            await EnterCascadeWithSubActions(session, SubMenuLayoutStyle.Fan, childCount: 2);

            session.IsInSubMenu.Should().BeTrue();

            // Parent slot 1 center: root ring position for slot 1 with 8 slots.
            // Root slot 1 is at -90° (top) with radius 90 → center (250, 160).
            // Sub-ring radius = 0.6 × 90 = 54; Fan 2 children sit at ±30° about the
            // parent direction.
            double direction = -Math.PI / 2.0;
            double subRadius = 54.0;

            var child0 = SlotCenter(session, 1);
            var child1 = SlotCenter(session, 2);

            double expected0X = CanvasCenter + subRadius * Math.Cos(direction - Math.PI / 6.0);
            double expected0Y = CanvasCenter + subRadius * Math.Sin(direction - Math.PI / 6.0);
            double expected1X = CanvasCenter + subRadius * Math.Cos(direction + Math.PI / 6.0);
            double expected1Y = CanvasCenter + subRadius * Math.Sin(direction + Math.PI / 6.0);

            child0.cx.Should().BeApproximately(expected0X, 0.75, "child 0 (upper wing) must sit on the sub-ring, not on the root ring");
            child0.cy.Should().BeApproximately(expected0Y, 0.75);
            child1.cx.Should().BeApproximately(expected1X, 0.75, "child 1 (lower wing) must sit on the sub-ring, not on the root ring");
            child1.cy.Should().BeApproximately(expected1Y, 0.75);

            // The two children must NOT still be at their root ring spots.
            Distance(child0.cx, child0.cy, CanvasCenter, CanvasCenter).Should().BeLessThan(90.0 - 25.0, "sub-ring (54) is well inside the root ring (90)");
            Distance(child1.cx, child1.cy, CanvasCenter, CanvasCenter).Should().BeLessThan(65.0);
        }

        [Fact]
        public async Task EnterCascade_Ring5_ChildSlotsShouldDistributeOnSubRing()
        {
            var (session, _) = CreateSessionWithRealEngines();
            session.IsVisible = true;
            await EnterCascadeWithSubActions(session, SubMenuLayoutStyle.Fan, childCount: 5);

            session.IsInSubMenu.Should().BeTrue();

            // 5 children > FanMaxSlots(3) → Ring fallback on the same sub-ring (54).
            for (int i = 1; i <= 5; i++)
            {
                var (cx, cy) = SlotCenter(session, i);
                double dist = Distance(cx, cy, CanvasCenter, CanvasCenter);
                dist.Should().BeApproximately(54.0, 1.0,
                    $"child slot {i} must sit on the 54-radius sub-ring (was reported invisible in manual QA)");
            }
        }

        [Fact]
        public async Task EnterCascade_FillerSlots_ShouldRemainOnRootRing()
        {
            var (session, _) = CreateSessionWithRealEngines();
            session.IsVisible = true;
            await EnterCascadeWithSubActions(session, SubMenuLayoutStyle.Fan, childCount: 2);

            // Slots 3..8 are fillers; they must stay on the root ring (radius 90),
            // otherwise the wheel shape changes for no reason.
            for (int i = 3; i <= 8; i++)
            {
                var (cx, cy) = SlotCenter(session, i);
                double dist = Distance(cx, cy, CanvasCenter, CanvasCenter);
                dist.Should().BeApproximately(90.0, 1.0, $"filler slot {i} must stay on the root ring");
            }
        }

        private static async Task EnterCascadeWithSubActions(
            MenuSession session, SubMenuLayoutStyle style, int childCount)
        {
            var pluginSlot = new PluginSlot
            {
                Slot = 1,
                PluginId = "com.pulsar.command",
                Action = "sendkeys",
                Label = "QA Parent",
                Args = new Dictionary<string, string>(),
                CascadeLayoutStyle = style,
                SubActions = Enumerable.Range(0, childCount)
                    .Select(i => new SubSlotDescriptor(
                        "com.pulsar.command", "sendkeys",
                        new Dictionary<string, string> { ["keys"] = $"QA-{childCount}{(char)('A' + i)}" },
                        $"QA{childCount}-{(char)('A' + i)}", string.Empty, string.Empty))
                    .ToList()
            };

            var slot = session.Slots.First(s => s.SlotIndex == 1);
            slot.DataContext = pluginSlot;
            slot.SubSlots.Clear();
            foreach (var sub in pluginSlot.SubActions)
            {
                slot.SubSlots.Add(sub);
            }
            slot.Label = pluginSlot.Label;
            slot.IsEnabled = true;
            slot.ActionStrategy = new NoOpStrategy();

            await session.HandleGlobalMouseClickAsync(
                GlobalMouseButton.Left, clickSlotIndex: 1, new Vector(250, 160));
        }

        private static (double cx, double cy) SlotCenter(MenuSession session, int slotIndex)
        {
            var slot = session.Slots.First(s => s.SlotIndex == slotIndex);
            return (slot.X + slot.Size / 2.0, slot.Y + slot.Size / 2.0);
        }

        private static double Distance(double x1, double y1, double x2, double y2)
        {
            double dx = x1 - x2;
            double dy = y1 - y2;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        private static (MenuSession, PluginSlot) CreateSessionWithRealEngines()
        {
            var pluginRegistry = new Mock<IPluginRegistry>();
            pluginRegistry
                .Setup(registry => registry.IsPluginEnabled(It.IsAny<string>()))
                .Returns(true);

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

            var windowService = new Mock<IWindowService>();
            var previewService = new Mock<IPreviewService>();

            // REAL engines — the coordinates under test must be production geometry.
            var slotLayoutEngine = new SlotLayoutEngine();

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

            var pageProviderFactory = new Mock<IPageProviderFactory>();
            pageProviderFactory.Setup(f => f.CreateCommandPage(It.IsAny<List<PluginSlot>>(), It.IsAny<PulsarContext>()))
                .Returns((List<PluginSlot> slots, PulsarContext ctx) =>
                    new CommandPageProvider(slots, Mock.Of<IPluginRegistry>(), ctx, Mock.Of<ITrayService>(), configService.Object,
                        Mock.Of<IPluginExecutor>(), Mock.Of<IActionFeedbackService>(), null, null, null, null));

            var loc = new Mock<ILocalizationService>();
            loc.Setup(l => l["RadialMenu.Pulsar"]).Returns("Pulsar");
            loc.Setup(l => l["RadialMenu.Back"]).Returns("Back");
            loc.Setup(l => l["Notification.Cancel"]).Returns("Cancel");
            loc.Setup(l => l["RadialMenu.SubMenuPageFormat"]).Returns("{0} ({1}/{2})");

            var session = new MenuSession(
                configService.Object,
                windowService.Object,
                Mock.Of<IWindowInventoryCoordinator>(),
                new Mock<IHotkeyService>().Object,
                new Mock<ITrayService>().Object,
                animationController.Object,
                slotLayoutEngine,
                new Mock<IPagingController>().Object,
                previewService.Object,
                pageProviderFactory.Object,
                loc.Object,
                new DirectUiDispatcher(),
                subMenuStrategies: subMenuStrategies);

            session.Initialize();

            return (session, null!);
        }
    }
}
