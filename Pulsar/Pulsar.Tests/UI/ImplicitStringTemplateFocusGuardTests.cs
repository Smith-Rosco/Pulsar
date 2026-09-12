// [Path]: Pulsar/Pulsar.Tests/UI/ImplicitStringTemplateFocusGuardTests.cs
//
// Static guard for a whole defect class found on 2026-09-12:
//
//   An IMPLICIT (key-less) DataTemplate targeting System.String renders EVERY
//   string shown through a ContentPresenter app-wide — TabItem headers, Expander
//   headers, menu items, tooltips, ... — not just the dialog messages it was
//   written for. If that template's root element is focusable, it steals
//   keyboard focus at click time and breaks WPF's focus-based selection logic:
//   TabItem.OnMouseLeftButtonDown → SetFocus() → OnPreviewGotKeyboardFocus →
//   IsSelected never runs, so the settings "内置/外部" tab headers appeared
//   completely unclickable (real-mouse clicks included; diagnosed via E2E
//   click loop + focus-event tracing, see journal 2026-09-12 and
//   artifacts/e2e-plugins-tabs).
//
//   Fix: Focusable="False" on the template root. Mouse-wheel scrolling does not
//   need keyboard focus, so dialog message bodies keep working.
//
// Like SettingsLayoutGuardTests this is a source scan on purpose: the defect
// lives in template resolution, which a headless test host cannot render.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Xunit;

namespace Pulsar.Tests.UI
{
    public sealed class ImplicitStringTemplateFocusGuardTests
    {
        private static readonly Regex ExcludedPath = new(
            @"\\(bin|obj)\\", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly string SourceRoot = Path.GetFullPath(
            Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "Pulsar"));

        /// <summary>Strips <!-- --> comments so guard-relevant words in comments cannot mask the scan.</summary>
        private static string StripComments(string xaml)
        {
            return Regex.Replace(xaml, @"<!--.*?-->", string.Empty, RegexOptions.Singleline);
        }

        /// <summary>
        /// Implicit (x:Key-less) DataTemplate targeting System.String. Matches both
        /// the "sys" and "system" xmlns aliases.
        /// </summary>
        private static readonly Regex ImplicitStringTemplate = new(
            @"<DataTemplate\s+(?![^>]*x:Key=)[^>]*DataType=""\{x:Type (?:sys|system):String\}""[^>]*>(?<body>.*?)</DataTemplate>",
            RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.IgnoreCase);

        /// <summary>First element opening tag inside a template body.</summary>
        private static readonly Regex FirstElementTag = new(
            @"<([A-Za-z][\w:.]*)((?:\s[^<>]*)?/?>)",
            RegexOptions.Compiled);

        [Fact]
        public void Implicit_string_template_roots_must_not_be_focusable()
        {
            Assert.True(Directory.Exists(SourceRoot),
                $"Source root not found: {SourceRoot} — the scan would silently pass on nothing.");

            var violations = new List<string>();
            var templateCount = 0;

            foreach (var file in Directory.EnumerateFiles(SourceRoot, "*.xaml", SearchOption.AllDirectories))
            {
                if (ExcludedPath.IsMatch(file))
                {
                    continue;
                }

                var stripped = StripComments(File.ReadAllText(file));
                foreach (Match m in ImplicitStringTemplate.Matches(stripped))
                {
                    templateCount++;
                    var body = m.Groups["body"].Value;
                    var rootTag = FirstElementTag.Match(body);

                    if (!rootTag.Success)
                    {
                        violations.Add($"{file}: implicit String DataTemplate has no element body.");
                        continue;
                    }

                    var isNonFocusable = rootTag.Value.Contains("Focusable", StringComparison.OrdinalIgnoreCase)
                        && rootTag.Value.Contains("\"False\"", StringComparison.OrdinalIgnoreCase);

                    if (!isNonFocusable)
                    {
                        violations.Add(
                            $"{file}: implicit String DataTemplate root <{rootTag.Groups[1].Value}> must set Focusable=\"False\". " +
                            "The app-wide string template renders EVERY ContentPresenter string " +
                            "(TabItem/Expander/menu headers included); a focusable root steals keyboard focus " +
                            "and breaks focus-based selection (TabItem headers become unclickable).");
                    }
                }
            }

            // Fail-fast: an empty match set means the scan silently stopped seeing the
            // template (renamed alias, moved file) and the guard became a no-op.
            Assert.True(templateCount > 0,
                "No implicit DataTemplate DataType=String found anywhere — the guard is blind. " +
                "If the dialog string template was removed or made explicit on purpose, update this guard.");

            Assert.True(violations.Count == 0,
                "Implicit String DataTemplate focus violations:\n" + new StringBuilder().AppendJoin("\n", violations));
        }
    }
}
