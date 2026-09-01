using System;
using System.Windows.Markup;
using FluentAssertions;
using Pulsar.Views.Controls;
using Xunit;

namespace Pulsar.Tests.Views.Controls
{
    public class EmptyStateSmokeTests
    {
        [Fact]
        public void InitializeComponent_ParsesActionButton_WithoutBasedOnBindingError()
        {
            // Regression: Style.BasedOn / Style.Setter.Value are NOT dependency
            // properties, so a {Binding} there throws XamlParseException at control
            // construction (surfaced as a hard crash when a page using EmptyState is
            // first navigated to). Must construct without resource errors.
            XamlParseException? resourceError = null;
            StaTestRunner.RunInSta(() =>
            {
                try
                {
                    _ = new EmptyState();
                }
                catch (XamlParseException ex)
                {
                    resourceError = ex;
                }
                catch (Exception ex) when (ex is NullReferenceException or InvalidCastException)
                {
                    // InitializeComponent succeeded (resources resolved); the ctor then
                    // touches App.Current.Services, which requires a running Pulsar.App.
                }
            });

            resourceError.Should().BeNull(
                "EmptyState must resolve Tokens.xaml/ButtonStyles.xaml and the action button style without XamlParseException");
        }
    }
}
