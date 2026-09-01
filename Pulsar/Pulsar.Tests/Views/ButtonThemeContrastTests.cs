using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using FluentAssertions;
using Xunit;

namespace Pulsar.Tests.Views
{
    /// <summary>
    /// Regression: Pulsar button templates must render readable text on their accent
    /// backgrounds. Previously the text presenter was a ContentPresenter; WPF's
    /// string->TextBlock generation freezes the foreground at first layout, so the
    /// template trigger's white foreground never reached the rendered text, leaving
    /// black text on the dark-blue accent background.
    ///
    /// These tests render on an isolated visual tree (no Window / Application /
    /// dispatcher pump) exactly like SlotWheelRingClippingTests, so they stay safe
    /// under xUnit's parallel execution (a shared Application.Current across STA
    /// threads hangs otherwise). Styles come from a constructed AddSlotContent, and
    /// the colour tokens come from WPF-UI's real ThemesDictionary (Light) plus
    /// Pulsar's Theme.Light.xaml — see <see cref="BuildLightTheme"/>.
    /// </summary>
    public class ButtonThemeContrastTests
    {
        [Fact]
        public void PulsarPrimaryButton_OnThemedTree_RendersWhiteTextOnAccent()
        {
            ButtonRenderResult? result = null;
            RunInSta(() => result = RenderButton("PulsarPrimaryButtonStyle"));

            result!.TextColor.Should().NotBeNull();
            result.TextColor!.Value.R.Should().BeGreaterThan(180,
                $"primary button text should be white on the accent background, got {result}");
            result.BackgroundColor.Should().NotBeNull();
            result.BackgroundColor!.Value.R.Should().BeLessThan(80,
                $"primary button background should be the dark accent color, got {result}");
        }

        [Fact]
        public void PulsarDangerButton_OnThemedTree_RendersWhiteTextOnRed()
        {
            ButtonRenderResult? result = null;
            RunInSta(() => result = RenderButton("PulsarDangerButtonStyle"));

            result!.TextColor.Should().NotBeNull();
            result.TextColor!.Value.R.Should().BeGreaterThan(180,
                $"danger button text should be white on the destructive background, got {result}");
        }

        [Fact]
        public void PulsarSecondaryButton_OnThemedTree_RendersDarkTextOnNeutral()
        {
            ButtonRenderResult? result = null;
            RunInSta(() => result = RenderButton("PulsarSecondaryButtonStyle"));

            result!.TextColor.Should().NotBeNull();
            result.TextColor!.Value.R.Should().BeLessThan(100,
                $"secondary button text should be dark on the neutral background, got {result}");
        }

        [Fact]
        public void SegmentedRadioButton_CheckedAfterFirstLayout_UpdatesTextColor()
        {
            Exception? failure = null;
            RunInSta(() =>
            {
                try
                {
                    var content = new Pulsar.Views.Dialogs.Contents.AddSlotContent();
                    var style = content.TryFindResource("SegmentedActionButtonStyle") as Style;
                    style.Should().NotBeNull();

                    var radio = new RadioButton
                    {
                        Content = "Switch Or Launch",
                        IsChecked = false,
                        Style = style
                    };

                    var uncheckedText = RenderIsolated(radio);
                    var uncheckedColor = ((SolidColorBrush)uncheckedText.Foreground).Color;
                    uncheckedColor.R.Should().BeLessThan(100, $"unchecked text should be dark, got {uncheckedColor}");

                    radio.IsChecked = true;
                    var checkedText = RenderIsolated(radio);
                    var checkedColor = ((SolidColorBrush)checkedText.Foreground).Color;
                    checkedColor.R.Should().BeGreaterThan(180,
                        $"segmented button text must turn white when checked after layout, got {checkedColor}");
                }
                catch (Exception ex)
                {
                    failure = ex;
                }
            });

            if (failure != null)
            {
                throw new Xunit.Sdk.XunitException(failure.ToString());
            }
        }

        private static ButtonRenderResult RenderButton(string styleKey)
        {
            var content = new Pulsar.Views.Dialogs.Contents.AddSlotContent();
            var style = content.TryFindResource(styleKey) as Style;
            style.Should().NotBeNull($"{styleKey} must be resolvable from AddSlotContent's merged resources");

            var button = new Wpf.Ui.Controls.Button { Content = "Test Action" };
            button.Style = style!;

            var root = new ContentControl { Width = 200, Height = 60 };
            root.Resources.MergedDictionaries.Add(BuildLightTheme());
            root.Content = button;

            root.Measure(new Size(200, 60));
            root.Arrange(new Rect(0, 0, 200, 60));
            root.UpdateLayout();

            var text = FindDescendants<TextBlock>(button).FirstOrDefault();
            var background = FindDescendants<Border>(button)
                .Select(b => b.Background as SolidColorBrush)
                .Where(b => b != null)
                .FirstOrDefault();

            return new ButtonRenderResult
            {
                TextColor = (text?.Foreground as SolidColorBrush)?.Color,
                BackgroundColor = background?.Color
            };
        }

        /// <summary>
        /// Renders a control in an isolated visual tree (no Window / Application) so
        /// tests do not interfere with each other's Application.Current state. The
        /// segmented style's foreground triggers use literal colors, so no theme
        /// dictionary is required for the color assertions.
        /// </summary>
        private static TextBlock RenderIsolated(FrameworkElement element)
        {
            var root = new ContentControl
            {
                Content = element,
                Width = 200,
                Height = 60
            };

            root.Measure(new Size(200, 60));
            root.Arrange(new Rect(0, 0, 200, 60));
            root.UpdateLayout();

            var text = FindDescendants<TextBlock>(element).FirstOrDefault();
            text.Should().NotBeNull("the button content should materialize during layout");
            return text!;
        }

        /// <summary>
        /// Supplies the Light theme's colour tokens.
        ///
        /// This loads WPF-UI's real <c>ThemesDictionary</c> rather than hand-copying
        /// key/value pairs. That matters: the button templates reference Fluent tokens
        /// (AccentFillColorDefaultBrush, AccentTextFillColorPrimaryBrush, ...), and a
        /// hand-rolled dictionary silently drifts out of sync — DynamicResource lookups
        /// fail *quietly*, so a stale copy would degrade the button to its fallback
        /// colours rather than fail loudly. Building it here also keeps the test
        /// independent of any Application instance.
        ///
        /// WPF-UI deliberately omits the <c>Accent*</c> keys from <c>ThemesDictionary</c>:
        /// at runtime <see cref="Pulsar.Services.ThemeService.ApplyAccent"/> injects them
        /// through <c>ApplicationAccentColorManager</c> so they track the user's Windows
        /// accent. Tests have no Application instance, so the Accent* keys would be
        /// missing here and resolve to null (silent DynamicResource failure). We seed the
        /// documented Fluent Light defaults — the same values ThemeService falls back to
        /// when the system accent is unavailable.
        ///
        /// Pulsar's own Theme.Light.xaml is added on top because a few states
        /// (e.g. Theme.Destructive.Hover) have no Fluent equivalent.
        /// </summary>
        private static ResourceDictionary BuildLightTheme()
        {
            var merged = new ResourceDictionary();

            // WPF-UI's Light token set — the same dictionary ThemeService injects at runtime.
            merged.MergedDictionaries.Add(new Wpf.Ui.Markup.ThemesDictionary
            {
                Theme = Wpf.Ui.Appearance.ApplicationTheme.Light
            });

            // Runtime-only accent keys (see summary above for why we seed them).
            merged["AccentFillColorDefaultBrush"] = new SolidColorBrush(Color.FromRgb(0x00, 0x67, 0xC0));
            merged["AccentFillColorSecondaryBrush"] = new SolidColorBrush(Color.FromRgb(0x19, 0x7C, 0xCB));
            merged["AccentTextFillColorPrimaryBrush"] = new SolidColorBrush(Colors.White);

            // Pulsar's own tokens layer on top (Pulsar keys must win).
            merged.MergedDictionaries.Add(new ResourceDictionary
            {
                Source = new Uri("pack://application:,,,/Pulsar;component/Themes/Theme.Light.xaml", UriKind.Absolute)
            });

            return merged;
        }

        private static IEnumerable<T> FindDescendants<T>(DependencyObject root)
            where T : DependencyObject
        {
            int count = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);
                if (child is T match)
                {
                    yield return match;
                }

                foreach (var descendant in FindDescendants<T>(child))
                {
                    yield return descendant;
                }
            }
        }

        private static void RunInSta(Action action) => StaTestRunner.RunInSta(action);

        private sealed class ButtonRenderResult
        {
            public Color? TextColor { get; init; }
            public Color? BackgroundColor { get; init; }

            public override string ToString()
                => $"Text={TextColor}, Background={BackgroundColor}";
        }
    }
}
