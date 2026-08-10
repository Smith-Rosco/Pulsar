using System.Windows;
using System.Windows.Documents;
using FluentAssertions;

namespace Pulsar.Tests.ViewModels
{
    public class WheelBindingContractTests
    {
        /// <summary>
        /// Regression guard for the settings-page crash:
        /// "cannot TwoWay/OneWayToSource bind to read-only PageDisplayText".
        /// Run.Text defaults to TwoWay, so bindings targeting read-only VM properties
        /// MUST specify Mode=OneWay (as done in SlotWheelEditor.xaml).
        /// </summary>
        [Fact]
        public void RunText_DefaultBinding_IsTwoWay()
        {
            var metadata = (FrameworkPropertyMetadata)Run.TextProperty.GetMetadata(typeof(Run));

            metadata.BindsTwoWayByDefault.Should().BeTrue();
        }
    }
}
