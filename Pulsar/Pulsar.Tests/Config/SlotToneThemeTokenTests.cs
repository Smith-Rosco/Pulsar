using System;
using System.Globalization;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using FluentAssertions;
using Pulsar.Core.Converters;
using Pulsar.Models;

namespace Pulsar.Tests.Config
{
    public class SlotToneThemeTokenTests
    {
        [Fact]
        public void ThemeDictionaries_ShouldExposeSlotToneBrushKeys_ForBothSupportedThemes()
        {
            RunInSta(() =>
            {
                string[] themeSources =
                {
                    "/Pulsar;component/Themes/Theme.Light.xaml",
                    "/Pulsar;component/Themes/Theme.Dark.xaml"
                };

                string[] expectedKeys =
                {
                    "SlotTypeBrushDefault",
                    "SlotTypeBrushSecret",
                    "SlotTypeBrushApp",
                    "SlotTypeBrushCommand",
                    "SlotTypeBrushScript",
                    "SlotTypeBrushVba",
                    "SlotHealthBrushReady",
                    "SlotHealthBrushWarning",
                    "SlotHealthBrushError"
                };

                foreach (string themeSource in themeSources)
                {
                    var dictionary = (ResourceDictionary)Application.LoadComponent(new Uri(themeSource, UriKind.Relative));

                    foreach (string expectedKey in expectedKeys)
                    {
                        dictionary.Contains(expectedKey).Should().BeTrue($"{themeSource} should define {expectedKey}");
                        dictionary[expectedKey].Should().BeAssignableTo<Brush>();
                    }
                }
            });
        }

        [Fact]
        public void SlotBrushConverter_ShouldResolveBrushFromHostResourceScope()
        {
            RunInSta(() =>
            {
                var expectedBrush = new SolidColorBrush(Colors.Gold);
                var host = new Page();
                var child = new TextBlock();
                host.Resources["SlotTypeBrushSecret"] = expectedBrush;
                host.Content = child;

                var converter = new SlotBrushConverter();

                var result = converter.Convert(
                    new object[] { "SlotTypeBrushSecret", child },
                    typeof(Brush),
                    "Type",
                    CultureInfo.InvariantCulture);

                result.Should().BeSameAs(expectedBrush);
            });
        }

        [Fact]
        public void SlotBrushConverter_ShouldFallbackToHostSemanticDefault_WhenToneKeyMissing()
        {
            RunInSta(() =>
            {
                var fallbackBrush = new SolidColorBrush(Colors.SteelBlue);
                var host = new Page();
                var child = new TextBlock();
                host.Resources["SlotTypeBrushDefault"] = fallbackBrush;
                host.Content = child;

                var converter = new SlotBrushConverter();

                var result = converter.Convert(
                    new object[] { "SlotTypeBrushDoesNotExist", child },
                    typeof(Brush),
                    "Type",
                    CultureInfo.InvariantCulture);

                result.Should().BeSameAs(fallbackBrush);
            });
        }

        [Fact]
        public void SlotBrushConverter_ShouldReturnVisibleFallback_WhenHostTokensAreUnavailable()
        {
            RunInSta(() =>
            {
                var converter = new SlotBrushConverter();

                var result = converter.Convert(
                    new object[] { "MissingHealthKey", new TextBlock() },
                    typeof(Brush),
                    "Health",
                    CultureInfo.InvariantCulture);

                result.Should().BeOfType<SolidColorBrush>();
                ((SolidColorBrush)result).Color.A.Should().Be(255);
                ((SolidColorBrush)result).Color.Should().NotBe(Colors.Transparent);
            });
        }

        [Fact]
        public void SlotPresentation_ShouldPreserveExistingToneKeyContract()
        {
            SlotPresentation.ResolveTypeToneKey("com.pulsar.pki").Should().Be("SlotTypeBrushSecret");
            SlotPresentation.ResolveTypeToneKey("com.pulsar.command").Should().Be("SlotTypeBrushCommand");
            SlotPresentation.ResolveTypeToneKey("unknown.plugin").Should().Be("SlotTypeBrushDefault");

            SlotPresentation.ResolveHealthToneKey(ValidationSeverity.Warning).Should().Be("SlotHealthBrushWarning");
            SlotPresentation.ResolveHealthToneKey(ValidationSeverity.Error).Should().Be("SlotHealthBrushError");
            SlotPresentation.ResolveHealthToneKey(ValidationSeverity.None).Should().Be("SlotHealthBrushReady");
        }

        private static void RunInSta(Action action)
        {
            Exception? capturedException = null;
            using var completed = new ManualResetEventSlim(false);

            var thread = new Thread(() =>
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    capturedException = ex;
                }
                finally
                {
                    completed.Set();
                }
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            completed.Wait();
            thread.Join();

            if (capturedException != null)
            {
                ExceptionDispatchInfo.Capture(capturedException).Throw();
            }
        }
    }
}
