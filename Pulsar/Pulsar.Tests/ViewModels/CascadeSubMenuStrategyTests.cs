using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Pulsar.Core.Plugin.Metadata;
using Pulsar.Models;
using Pulsar.Services.ActionFeedback;
using Pulsar.Services.Interfaces;
using Pulsar.Tests.TestHelpers;
using Pulsar.ViewModels;
using Pulsar.ViewModels.Strategies;
using Xunit;

namespace Pulsar.Tests.ViewModels
{
    /// <summary>
    /// Behavior of <see cref="CascadeSubMenuStrategy"/> (id <c>cascade</c>): center
    /// back-navigation + cascade label, child slots mapped to <see cref="PluginActionStrategy"/>,
    /// empty-page <see cref="NoOpStrategy"/> fillers, unknown plugin/action children marked
    /// not-enabled, and pagination driven by <see cref="CascadeSubMenuDescriptor.SubSlots"/> count.
    /// </summary>
    public class CascadeSubMenuStrategyTests
    {
        private const string KnownPluginId = "com.pulsar.command";
        private const string KnownAction = "sendkeys";

        [Fact]
        public void ConfigureSubMenu_ShouldSetCenterAsBackActionStrategy_WithCascadeLabel()
        {
            // [ADR-024 D7] Only Ring (replace-mode) stands the centre orb in for the
            // parent slot, so this scenario uses an effective Ring layout.
            var (context, descriptor, _) = CreateScenario(pageIndex: 0, subSlotCount: 1, effectiveLayoutStyle: SubMenuLayoutStyle.Ring);

            context.Strategy.ConfigureSubMenu(context.Context, descriptor);

            context.Context.CenterSlot.ActionStrategy.Should().BeOfType<BackActionStrategy>();
            context.Context.CenterSlot.Type.Should().Be(SlotType.Action);
            context.Context.CenterSlot.Label.Should().Be("Cascade Label");
        }

        [Fact]
        public void ConfigureSubMenu_Fan_ShouldNotTouchCenterSlot()
        {
            // [ADR-024 D8] Fan is overlay-mode: the parent slot (still in the root
            // collection) is the dismiss anchor, so the centre orb keeps its root
            // semantics untouched.
            var (context, descriptor, _) = CreateScenario(pageIndex: 0, subSlotCount: 1);

            var labelBefore = context.Context.CenterSlot.Label;
            context.Strategy.ConfigureSubMenu(context.Context, descriptor);

            context.Context.CenterSlot.ActionStrategy.Should().NotBeOfType<BackActionStrategy>();
            context.Context.CenterSlot.Label.Should().Be(labelBefore);
        }

        [Fact]
        public void ConfigureSubMenu_ShouldMapChildrenToPluginActionStrategy()
        {
            var (context, descriptor, metadataRegistry) = CreateScenario(pageIndex: 0, subSlotCount: 2);
            metadataRegistry
                .Setup(registry => registry.GetActionMetadata(KnownPluginId, KnownAction))
                .Returns(new SlotActionMetadata { Name = KnownAction });

            context.Strategy.ConfigureSubMenu(context.Context, descriptor);

            // [ADR-024 D4/D7] Children render into SubMenuSlots, not the root slots.
            context.SubMenuSlots[0].ActionStrategy.Should().BeOfType<PluginActionStrategy>();
            context.SubMenuSlots[0].Type.Should().Be(SlotType.Action);
            context.SubMenuSlots[0].IsEnabled.Should().BeTrue();
            context.SubMenuSlots[0].DataContext.Should().BeOfType<SubSlotDescriptor>();
            context.SubMenuSlots[1].ActionStrategy.Should().BeOfType<PluginActionStrategy>();
        }

        [Fact]
        public void ConfigureSubMenu_ShouldAssignNoOpStrategy_ToEmptyPageSlots()
        {
            // Page 1 of a 1-child cascade: the whole page is empty → all fillers.
            var (context, descriptor, _) = CreateScenario(pageIndex: 1, subSlotCount: 1);

            context.Strategy.ConfigureSubMenu(context.Context, descriptor);

            context.SubMenuSlots[0].ActionStrategy.Should().BeOfType<NoOpStrategy>();
            context.SubMenuSlots[0].Type.Should().Be(SlotType.None);
            context.SubMenuSlots[0].Label.Should().BeEmpty();
        }

        [Fact]
        public void ConfigureSubMenu_ShouldMarkUnknownChild_NotEnabled_WithNoOpStrategy()
        {
            var (context, descriptor, metadataRegistry) = CreateScenario(pageIndex: 0, subSlotCount: 1);
            metadataRegistry
                .Setup(registry => registry.GetActionMetadata(KnownPluginId, KnownAction))
                .Returns((SlotActionMetadata?)null);

            context.Strategy.ConfigureSubMenu(context.Context, descriptor);

            context.SubMenuSlots[0].ActionStrategy.Should().BeOfType<NoOpStrategy>();
            context.SubMenuSlots[0].IsEnabled.Should().BeFalse("an unknown plugin/action child must be marked not-enabled");
        }

        [Fact]
        public void ConfigureSubMenu_ShouldPageChildren_FromSubSlotCount()
        {
            // 10 children, 8 slots per page → page 1 (pageIndex 1) shows children 8-9
            // and leaves the rest of the supplied collection as no-op fillers. The
            // strategy fills exactly the SubMenuSlots collection it is handed.
            var (context, descriptor, metadataRegistry) = CreateScenario(pageIndex: 1, subSlotCount: 10);
            metadataRegistry
                .Setup(registry => registry.GetActionMetadata(KnownPluginId, KnownAction))
                .Returns(new SlotActionMetadata { Name = KnownAction });

            context.Strategy.ConfigureSubMenu(context.Context, descriptor);

            context.SubMenuSlots[0].ActionStrategy.Should().BeOfType<PluginActionStrategy>();
            context.SubMenuSlots[1].ActionStrategy.Should().BeOfType<PluginActionStrategy>();
            context.SubMenuSlots[2].ActionStrategy.Should().BeOfType<NoOpStrategy>();
            context.SubMenuSlots[2].Type.Should().Be(SlotType.None);
            context.SubMenuSlots[7].ActionStrategy.Should().BeOfType<NoOpStrategy>();
        }

        [Fact]
        public void ConfigureSubMenu_ShouldRejectNonCascadeDescriptor()
        {
            var (context, _, _) = CreateScenario(pageIndex: 0, subSlotCount: 1);

            var selected = context.Strategy.ConfigureSubMenu(
                context.Context,
                new WindowSubMenuDescriptor("testapp", new List<ProcessWindowInfo>()));

            selected.Should().BeNull();
            context.Context.CenterSlot.ActionStrategy.Should().NotBeOfType<BackActionStrategy>();
        }

        private static (ScenarioData, CascadeSubMenuDescriptor, Mock<IPluginMetadataRegistry>) CreateScenario(
            int pageIndex,
            int subSlotCount,
            int slotsPerPage = 8,
            SubMenuLayoutStyle effectiveLayoutStyle = SubMenuLayoutStyle.Fan)
        {
            var metadataRegistry = new Mock<IPluginMetadataRegistry>();

            var strategy = new CascadeSubMenuStrategy(
                new Mock<IPluginExecutor>().Object,
                metadataRegistry.Object,
                Mock.Of<ITrayService>(),
                Mock.Of<IActionFeedbackService>(),
                logger: Mock.Of<ILogger<CascadeSubMenuStrategy>>());

            var centerSlot = new SlotViewModel(0, 0, 0, 60);
            var slots = new ObservableCollection<SlotViewModel>();
            for (int i = 1; i <= slotsPerPage; i++)
            {
                slots.Add(new SlotViewModel(i, 0, 0, 50));
            }

            // [ADR-024 D4/D7] The session sizes the dedicated child collection to the
            // current page's child count and assigns SlotIndex 1..N; mirror that here.
            // Pooled VMs are reused across pages, hence the larger backing list.
            var subMenuSlots = new ObservableCollection<SlotViewModel>();
            for (int i = 1; i <= slotsPerPage; i++)
            {
                subMenuSlots.Add(new SlotViewModel(i, 0, 0, 50));
            }

            var subSlots = new List<SubSlotDescriptor>();
            for (int i = 0; i < subSlotCount; i++)
            {
                subSlots.Add(new SubSlotDescriptor(
                    KnownPluginId,
                    KnownAction,
                    new Dictionary<string, string> { ["keys"] = $"test{i}" },
                    $"Child {i}",
                    "E756",
                    "#32CD32"));
            }

            var descriptor = new CascadeSubMenuDescriptor(subSlots, SubMenuLayoutStyle.Fan, "Cascade Label");
            var context = new SubMenuContext(
                centerSlot,
                slots,
                slotsPerPage,
                pageIndex,
                PulsarContextFactory.CreateTestContext(),
                subMenuSlots,
                effectiveLayoutStyle);

            return (new ScenarioData(strategy, context, subMenuSlots), descriptor, metadataRegistry);
        }

        private sealed record ScenarioData(
            CascadeSubMenuStrategy Strategy,
            SubMenuContext Context,
            ObservableCollection<SlotViewModel> SubMenuSlots);
    }
}
