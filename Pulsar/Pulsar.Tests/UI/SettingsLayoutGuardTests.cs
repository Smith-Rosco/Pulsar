// [Path]: Pulsar/Pulsar.Tests/UI/SettingsLayoutGuardTests.cs
//
// Static guards for two settings-page defect classes found on 2026-09-08:
//
//   1. Text colour hardcoded to White/Black on a themed fill. Pulsar is bilingual
//      AND dual-theme: an accent fill is dark-blue in Light but light-cyan in Dark,
//      so literal white text disappears in Dark. The only correct token for text
//      sitting ON an accent/critical fill is TextOnAccentFillColorPrimaryBrush,
//      which flips with the fill's brightness.
//      See Docs/lessons/WPF_FLUENT_ACCENT_TOKENS_UNRESOLVED.md.
//
//   2. A settings page's ScrollViewer content sized to its own content instead of
//      the viewport (`HorizontalAlignment="Left"` + `MaxWidth`). The panel then
//      shrinks to the widest child, so cards never fill the view AND expanding any
//      card re-widens every card on the page.
//
// These are source scans rather than rendered-layout assertions on purpose: the
// test host has no desktop session, so Window.Show() yields ActualWidth == 0 and
// WPF-UI's VisualState pane animations never run (verified — see journal
// 2026-09-08). A static scan still locks the whole defect class at commit time.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace Pulsar.Tests.UI
{
    public sealed class SettingsLayoutGuardTests
    {
        private static readonly Regex ExcludedPath = new(
            @"\\(bin|obj)\\", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>Attribute form: <c>Foreground="White"</c>.</summary>
        private static readonly Regex ForegroundAttribute = new(
            @"Foreground\s*=\s*""(?:White|Black|#FFFFFFFF|#FF000000|#FFF|#000)""",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>Setter form: <c>&lt;Setter Property="Foreground" Value="White"/&gt;</c>.</summary>
        private static readonly Regex ForegroundSetter = new(
            @"<Setter\s+Property=""Foreground""\s+Value=""(?:White|Black|#FFFFFFFF|#FF000000|#FFF|#000)""",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>
        /// Any container start tag — the two offending attributes are matched against the
        /// captured attribute list, so declaration order does not matter (the original
        /// defects wrote <c>MaxWidth</c> before <c>HorizontalAlignment</c>).
        /// </summary>
        private static readonly Regex AnyContainerTag = new(
            @"<(?:StackPanel|Grid|Border|ItemsControl)\b(?<attrs>[^>]*)>",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex LeftAligned = new(
            @"HorizontalAlignment\s*=\s*""Left""", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex WidthCapped = new(
            @"MaxWidth\s*=\s*""[^""]+""", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>
        /// Accent tokens that do not exist in WPF-UI 4.3.0's theme dictionaries at all
        /// (verified via TryFindResource — see Docs/lessons/WPF_FLUENT_ACCENT_TOKENS_UNRESOLVED.md
        /// v1.2.0). A DynamicResource on a missing key fails silently, so a fill painted with
        /// one of these simply never appears. Use AccentFillColorDefaultBrush (fill) or
        /// AccentTextFillColorPrimaryBrush (accent-coloured text) instead — both are injected
        /// at runtime by ThemeService.
        /// </summary>
        private static readonly Regex DeadAccentToken = new(
            @"SystemFillColorAccentBrush|SystemFillColorAccentBackground\d", RegexOptions.Compiled);

        [Fact]
        public void No_xaml_foreground_is_hardcoded_to_white_or_black()
        {
            var root = ResolvePulsarRoot();
            var violations = new List<string>();

            foreach (var file in EnumerateXaml(root))
            {
                var text = File.ReadAllText(file);
                foreach (Match m in ForegroundAttribute.Matches(text))
                {
                    violations.Add($"{Path.GetRelativePath(root, file)}: {m.Value}");
                }
                foreach (Match m in ForegroundSetter.Matches(text))
                {
                    violations.Add($"{Path.GetRelativePath(root, file)}: {m.Value}");
                }
            }

            Assert.True(violations.Count == 0,
                "Hardcoded White/Black foregrounds found in XAML. They are invisible against "
                + "the Dark-theme accent/critical fills — use the theme-aware token "
                + "{DynamicResource TextOnAccentFillColorPrimaryBrush} for text on a coloured "
                + "fill, or TextFillColorPrimary/Secondary/TertiaryBrush otherwise:\n  - "
                + string.Join("\n  - ", violations));
        }

        [Fact]
        public void No_xaml_references_accent_tokens_that_do_not_resolve()
        {
            var root = ResolvePulsarRoot();
            var violations = new List<string>();

            foreach (var file in EnumerateXaml(root))
            {
                var text = StripComments(File.ReadAllText(file));
                foreach (Match m in DeadAccentToken.Matches(text))
                {
                    violations.Add($"{Path.GetRelativePath(root, file)}: {m.Value}");
                }
            }

            Assert.True(violations.Count == 0,
                "XAML references accent colour tokens that WPF-UI 4.3.0 does not define. "
                + "DynamicResource on a missing key fails silently, so fills/text painted with "
                + "them never appear (the analytics rank badges and bars were invisible for this "
                + "reason). Use AccentFillColorDefaultBrush / AccentTextFillColorPrimaryBrush:\n  - "
                + string.Join("\n  - ", violations));
        }

        [Fact]
        public void Settings_pages_do_not_size_scroll_content_to_content()
        {
            var root = ResolvePulsarRoot();
            var pagesDir = Path.Combine(root, "Views", "Pages");
            Assert.True(Directory.Exists(pagesDir), $"Pages directory not found at '{pagesDir}'.");

            var violations = Directory
                .EnumerateFiles(pagesDir, "*.xaml", SearchOption.TopDirectoryOnly)
                .Where(p => !ExcludedPath.IsMatch(p))
                .SelectMany(file => AnyContainerTag.Matches(File.ReadAllText(file))
                    .Cast<Match>()
                    .Where(m => LeftAligned.IsMatch(m.Groups["attrs"].Value) && WidthCapped.IsMatch(m.Groups["attrs"].Value))
                    .Select(m => $"{Path.GetRelativePath(root, file)}: {Trim(m.Value)}"))
                .ToList();

            Assert.True(violations.Count == 0,
                "A settings page caps its width with MaxWidth while pinning "
                + "HorizontalAlignment=\"Left\". The panel then measures to its content instead "
                + "of the viewport, so cards never fill the view and expanding one card re-widens "
                + "them all. Use HorizontalAlignment=\"Stretch\" (keep MaxWidth as a reading-width "
                + "cap) or bind Width to the ScrollViewer's ViewportWidth:\n  - "
                + string.Join("\n  - ", violations));
        }

        private static string Trim(string snippet)
        {
            var flat = snippet.Replace("\r", " ").Replace("\n", " ");
            return flat.Length <= 140 ? flat : flat.Substring(0, 140) + "…";
        }

        /// <summary>XAML comments legitimately mention token names (they document the fix),
        /// so comments must not count as usages.</summary>
        private static string StripComments(string xaml)
            => Regex.Replace(xaml, @"<!--.*?-->", string.Empty, RegexOptions.Singleline);

        private static IEnumerable<string> EnumerateXaml(string root)
            => Directory.EnumerateFiles(root, "*.xaml", SearchOption.AllDirectories)
                .Where(p => !ExcludedPath.IsMatch(p));

        private static string ResolvePulsarRoot()
        {
            var probe = new DirectoryInfo(AppContext.BaseDirectory);
            while (probe != null)
            {
                var candidate = Path.Combine(probe.FullName, "Pulsar", "Pulsar");
                if (Directory.Exists(Path.Combine(candidate, "Views"))) return candidate;
                if (Directory.Exists(Path.Combine(probe.FullName, "Views")) && probe.Name == "Pulsar") return probe.FullName;
                probe = probe.Parent;
            }
            return Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Pulsar", "Pulsar");
        }
    }
}
