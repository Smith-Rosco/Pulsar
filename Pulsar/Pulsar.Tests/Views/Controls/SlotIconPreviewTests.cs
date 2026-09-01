using System;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows.Controls;
using System.Windows.Data;
using FluentAssertions;
using Pulsar.Models;
using Pulsar.Views.Controls;
using Xunit;

namespace Pulsar.Tests.Views.Controls
{
    public class SlotIconPreviewTests
    {
        [Fact]
        public void IconKeyChange_ShouldUpdateSelectorAndOrbPreviewImmediately()
        {
            RunInSta(() =>
            {
                var slot = new PluginSlot { IconKey = "E72E" };
                var selector = new IconSelector();
                var orb = new SlotOrb();
                BindingOperations.SetBinding(selector, IconSelector.IconKeyProperty, new Binding(nameof(PluginSlot.IconKey)) { Source = slot });
                BindingOperations.SetBinding(orb, SlotOrb.IconKeyProperty, new Binding(nameof(PluginSlot.IconKey)) { Source = slot });

                slot.IconKey = "E8A7";

                selector.IconKey.Should().Be("E8A7");
                orb.IconKey.Should().Be("E8A7");
                orb.RenderGlyph.Should().NotBeEmpty();
            });
        }

        private static void RunInSta(Action action) => StaTestRunner.RunInSta(action);
    }
}
