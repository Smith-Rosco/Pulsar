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
            var (context, descriptor, _) = CreateScenario(pageIndex: 0, subSlotCount: 1);

            context.Strategy.ConfigureSubMenu(context.Context, descriptor);

            context.Context.CenterSlot.ActionStrategy.Should().BeOfType<BackActionStrategy>();
            context.Context.CenterSlot.Type.Should().Be(SlotType.Action);
            context.Context.CenterSlot.Label.Should().Be("Cascade Label");
        }

        [Fact]
        public void ConfigureSubMenu_ShouldMapChildrenToPluginActionStrategy()
        {
            var (context, descriptor, metadataRegistry) = CreateScenario(pageIndex: 0, subSlotCount: 2);
            metadataRegistry
                .Setup(registry => registry.GetActionMetadata(KnownPluginId, KnownAction))
                .Returns(new SlotActionMetadata { Name = KnownAction });

            context.Strategy.ConfigureSubMenu(context.Context, descriptor);

            context.Slots[0].ActionStrategy.Should().BeOfType<PluginActionStrategy>();
            context.Slots[0].Type.Should().Be(SlotType.Action);
            context.Slots[0].IsEnabled.Should().BeTrue();
            context.Slots[0].DataContext.Should().BeOfType<SubSlotDescriptor>();
            context.Slots[1].ActionStrategy.Should().BeOfType<PluginActionStrategy>();
        }

        [Fact]
        public void ConfigureSubMenu_ShouldAssignNoOpStrategy_ToEmptyPageSlots()
        {
            // Page 1 of a 1-child cascade: the whole page is empty → all fillers.
            var (context, descriptor, _) = CreateScenario(pageIndex: 1, subSlotCount: 1);

            context.Strategy.ConfigureSubMenu(context.Context, descriptor);

            context.Slots[0].ActionStrategy.Should().BeOfType<NoOpStrategy>();
            context.Slots[0].Type.Should().Be(SlotType.None);
            context.Slots[0].Label.Should().BeEmpty();
        }

        [Fact]
        public void ConfigureSubMenu_ShouldMarkUnknownChild_NotEnabled_WithNoOpStrategy()
        {
            var (context, descriptor, metadataRegistry) = CreateScenario(pageIndex: 0, subSlotCount: 1);
            metadataRegistry
                .Setup(registry => registry.GetActionMetadata(KnownPluginId, KnownAction))
                .Returns((SlotActionMetadata?)null);

            context.Strategy.ConfigureSubMenu(context.Context, descriptor);

            context.Slots[0].ActionStrategy.Should().BeOfType<NoOpStrategy>();
            context.Slots[0].IsEnabled.Should().BeFalse("an unknown plugin/action child must be marked not-enabled");
        }

        [Fact]
        public void ConfigureSubMenu_ShouldPageChildren_FromSubSlotCount()
        {
            // 10 children, 8 slots per page → page 1 (pageIndex 1) shows children 8-9
            // and leaves slots 3..7 as no-op fillers.
            var (context, descriptor, metadataRegistry) = CreateScenario(pageIndex: 1, subSlotCount: 10);
            metadataRegistry
                .Setup(registry => registry.GetActionMetadata(KnownPluginId, KnownAction))
                .Returns(new SlotActionMetadata { Name = KnownAction });

            context.Strategy.ConfigureSubMenu(context.Context, descriptor);

            context.Slots[0].ActionStrategy.Should().BeOfType<PluginActionStrategy>();
            context.Slots[1].ActionStrategy.Should().BeOfType<PluginActionStrategy>();
            context.Slots[2].ActionStrategy.Should().BeOfType<NoOpStrategy>();
            context.Slots[2].Type.Should().Be(SlotType.None);
            context.Slots[7].ActionStrategy.Should().BeOfType<NoOpStrategy>();
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
            int slotsPerPage = 8)
        {
            var metadataRegistry = new Mock<IPluginMetadataRegistry>();
            var pluginRegistry = new Mock<IPluginRegistry>();
            pluginRegistry
                .Setup(registry => registry.IsPluginEnabled(It.IsAny<string>()))
                .Returns(true);

            var strategy = new CascadeSubMenuStrategy(
                pluginRegistry.Object,
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
                PulsarContextFactory.CreateTestContext());

            return (new ScenarioData(strategy, context, slots), descriptor, metadataRegistry);
        }

        private sealed record ScenarioData(
            CascadeSubMenuStrategy Strategy,
            SubMenuContext Context,
            ObservableCollection<SlotViewModel> Slots);
    }
}
