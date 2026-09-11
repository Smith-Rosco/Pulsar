using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Resources;
using System.Windows;
using System.Windows.Controls;
using FluentAssertions;
using Pulsar.Models;
using Xunit;

namespace Pulsar.Tests.Dialogs
{
    /// <summary>
    /// Guards the two message-dialog defects diagnosed on 2026-09-11:
    /// 1. literal <c>\n</c> sequences authored inside resx <c>&lt;value&gt;</c> elements —
    ///    XML does not interpret C# escapes, so WPF rendered the two characters verbatim;
    /// 2. string-content dialogs clipped by fixed-size windows with no scroll container —
    ///    the <c>sys:String</c> template must own a ScrollViewer and message dialogs must
    ///    grow vertically within a cap (DialogSizeConstraints.Message).
    /// </summary>
    public class DialogMessageOverflowGuardTests
    {
        private const string LiteralBackslashN = "\\n"; // two chars: '\' + 'n'

        private static IEnumerable<(string Culture, string Value)> AllResxValues()
        {
            var manager = new ResourceManager(
                "Pulsar.Resources.Strings",
                typeof(Pulsar.Models.ProfilesConfig).Assembly);

            foreach (var culture in new[] { "en", "zh-CN" })
            {
                using var set = manager.GetResourceSet(CultureInfo.GetCultureInfo(culture), true, true)!;
                foreach (System.Collections.DictionaryEntry entry in set)
                {
                    yield return (culture, (string)entry.Value!);
                }
            }
        }

        [Fact]
        public void ResxValues_ShouldNotContainLiteralBackslashN()
        {
            var offenders = AllResxValues()
                .Where(v => v.Value.Contains(LiteralBackslashN, StringComparison.Ordinal))
                .Select(v => $"{v.Culture}: {v.Value[..Math.Min(60, v.Value.Length)]}")
                .ToList();

            offenders.Should().BeEmpty(
                "resx <value> is XML text — C# escapes are NOT interpreted, WPF renders '\\n' verbatim. " +
                "Use &#10; (or a real newline inside xml:space=\"preserve\") instead.");
        }

        [Fact]
        public void CleanCacheBody_ShouldParseXmlNewlineEntityAsRealNewline()
        {
            var manager = new ResourceManager(
                "Pulsar.Resources.Strings",
                typeof(Pulsar.Models.ProfilesConfig).Assembly);

            foreach (var culture in new[] { "en", "zh-CN" })
            {
                var value = manager.GetString("Notification.CleanCacheBody", CultureInfo.GetCultureInfo(culture));
                value.Should().NotBeNull($"key must exist in {culture} resources");
                value!.Should().Contain("\n", "&#10; must resolve to a real line break at render time");
            }
        }

        [Fact]
        public void MessagePreset_ShouldGrowHeightWithinCap()
        {
            var m = DialogSizeConstraints.Message;

            m.AutoHeight.Should().BeTrue("message dialogs must fit their content instead of clipping it");
            m.Width.Should().Be(420);
            m.MinHeight.Should().Be(180);
            m.MaxHeight.Should().Be(480, "growth must stop somewhere; beyond the cap the string template's ScrollViewer takes over");
            m.AllowResize.Should().BeFalse();
            m.ShowMaximizeButton.Should().BeFalse();
        }

        [Fact]
        public void StringContentTemplate_ShouldWrapInScrollViewer()
        {
            StaTestRunner.RunInSta(() =>
            {
                // LoadContent resolves StaticResource against Application.Resources only;
                // the token normally comes from Styles/Tokens.xaml merged at App level.
                var app = System.Windows.Application.Current ?? new System.Windows.Application();
                app.Resources["Pulsar.Type.Subtitle"] = 16d;

                var templates = (ResourceDictionary)Application.LoadComponent(
                    new Uri("Pulsar;component/Themes/DialogTemplates.xaml", UriKind.Relative));

                var template = templates[new DataTemplateKey(typeof(string))] as DataTemplate;
                template.Should().NotBeNull("sys:String is the message-dialog content type");

                var root = template!.LoadContent();
                var scrollViewer = root as ScrollViewer;
                scrollViewer.Should().NotBeNull("long messages must scroll instead of clipping at the window edge");

                var textBlock = scrollViewer!.Content as TextBlock;
                textBlock.Should().NotBeNull();
                textBlock!.TextWrapping.Should().Be(TextWrapping.Wrap);
            });
        }
    }
}
