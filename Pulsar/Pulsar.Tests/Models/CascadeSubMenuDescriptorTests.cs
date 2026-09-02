using System;
using System.Collections.Generic;
using FluentAssertions;
using Pulsar.Models;
using Xunit;

namespace Pulsar.Tests.Models
{
    /// <summary>
    /// Tests for <see cref="SubMenuLayoutStyle"/> and <see cref="CascadeSubMenuDescriptor"/>:
    /// the style enum drives which sub-layout form renders cascade children, and the
    /// descriptor defaults to <see cref="SubMenuLayoutStyle.Fan"/> unless explicitly set.
    /// </summary>
    public class CascadeSubMenuDescriptorTests
    {
        [Fact]
        public void Style_ShouldDefaultToFan_WhenNotSpecified()
        {
            var descriptor = new CascadeSubMenuDescriptor(new List<SubSlotDescriptor>());

            descriptor.LayoutStyle.Should().Be(SubMenuLayoutStyle.Fan);
        }

        [Theory]
        [InlineData(SubMenuLayoutStyle.Ring)]
        [InlineData(SubMenuLayoutStyle.Fan)]
        public void Style_ShouldRoundTrip_WhenExplicitlySpecified(SubMenuLayoutStyle style)
        {
            var descriptor = new CascadeSubMenuDescriptor(
                new List<SubSlotDescriptor> { CreateSubSlot() },
                style);

            descriptor.LayoutStyle.Should().Be(style);
        }

        [Fact]
        public void Descriptor_ShouldKeepCascadeStrategyId_AndExposeSlotCountHint()
        {
            var slots = new List<SubSlotDescriptor>
            {
                CreateSubSlot("com.pulsar.command", "sendkeys"),
                CreateSubSlot("com.pulsar.winswitcher", "switch")
            };

            var descriptor = new CascadeSubMenuDescriptor(slots, SubMenuLayoutStyle.Ring);

            descriptor.StrategyId.Should().Be("cascade");
            descriptor.StrategyId.Should().Be(CascadeSubMenuDescriptor.StrategyIdValue);
            descriptor.TotalSlotsHint.Should().Be(2);
        }

        [Fact]
        public void Descriptor_ShouldTreatNullSubSlots_AsEmptyList()
        {
            var descriptor = new CascadeSubMenuDescriptor(null!);

            descriptor.SubSlots.Should().NotBeNull();
            descriptor.SubSlots.Should().BeEmpty();
            descriptor.TotalSlotsHint.Should().Be(0);
        }

        private static SubSlotDescriptor CreateSubSlot(
            string pluginId = "com.pulsar.command",
            string action = "sendkeys") =>
            new(pluginId, action, null, $"{pluginId}.{action}", "E8F1", "#4ECDC4");
    }
}
