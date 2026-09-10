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
//   3. A dialog-era content control reused as a settings tab page (via
//      SettingsSlotEditorPage) still pinning its fixed dialog size with
//      MinWidth/MinHeight. The tab's content area is (~700px) narrower than the
//      retired modal dialog (760px), and the content ScrollViewer disables
//      horizontal scrolling, so the right edge was silently clipped.
//      See Docs/lessons/WPF_SETTINGS_PANEL_WIDTH_CONTENT_DRIVEN.md.
//
//   4. Visibility declared TWICE for one element: a local Visibility binding on
//      the tag AND a Visibility Setter/DataTrigger in the element's own Style.
//      WPF resolves a local value above a Style trigger, so the trigger is dead
//      forever — both branches then render at once. On a secret slot parameter
//      this stacked the TextBox placeholder ("Select a saved password") on top
//      of the picker Border's DisplayValue ("No secret selected") in the same
//      grid column (found 2026-09-10). Consolidate every condition into
//      Style.Triggers and keep the local attribute off the tag.
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
        /// The content controls a settings <c>Page</c> instantiates directly in its code-behind
        /// (<c>new Dialogs.Contents.X()</c>). Those run inside the tab's content area, not inside
        /// a modal dialog, so they must stay host-sized. Resolved from the code-behind rather than
        /// hard-listed, so a new reuse site is guarded without editing this test.
        /// </summary>
        private static readonly Regex HostedDialogContent = new(
            @"new\s+Dialogs\.Contents\.(?<type>\w+)\s*\(\s*\)", RegexOptions.Compiled);

        /// <summary>Root-element fixed size: dialog-era <c>MinWidth</c>/<c>MinHeight</c>.
        /// The Blend design-time twins (<c>d:DesignWidth</c>/<c>d:DesignHeight</c>) must not match.</summary>
        private static readonly Regex FixedMinSize = new(
            @"(?<![\w:])Min(?:Width|Height)\s*=\s*""[^""]+""", RegexOptions.Compiled);

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

        /// <summary>
        /// An element start tag that carries a LOCAL <c>Visibility</c> attribute (usually a
        /// binding). Captured with the tag body so the Style block that follows can be inspected
        /// for a competing Visibility declaration.
        /// </summary>
        private static readonly Regex LocalVisibilityTag = new(
            @"<(?<tag>[A-Za-z_][\w:.]*)\b(?<body>[^>]*?)\bVisibility\s*=\s*""(?<value>[^""]*)""",
            RegexOptions.Compiled | RegexOptions.Singleline);

        /// <summary>
        /// A Visibility assignment inside a Style (<c>&lt;Setter Property="Visibility"</c> or
        /// <c>&lt;Trigger</c>/<c>&lt;DataTrigger</c> whose setter targets Visibility). Matched
        /// loosely against the element's own Style block.
        /// </summary>
        private static readonly Regex StyleVisibilitySetter = new(
            @"<Setter\s+Property=""Visibility""", RegexOptions.Compiled | RegexOptions.IgnoreCase);

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

        [Fact]
        public void Tab_hosted_dialog_contents_do_not_pin_a_fixed_dialog_size()
        {
            var root = ResolvePulsarRoot();
            var pagesDir = Path.Combine(root, "Views", "Pages");
            var contentsDir = Path.Combine(root, "Views", "Dialogs", "Contents");

            var hosted = Directory
                .EnumerateFiles(pagesDir, "*.xaml.cs", SearchOption.TopDirectoryOnly)
                .Where(p => !ExcludedPath.IsMatch(p))
                .SelectMany(file => HostedDialogContent.Matches(File.ReadAllText(file))
                    .Cast<Match>()
                    .Select(m => m.Groups["type"].Value))
                .Distinct()
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToList();

            // Guard the guard: a rename in the page's code-behind must not silently
            // turn this scan into a no-op.
            Assert.True(hosted.Count > 0,
                "No dialog content control is hosted by a settings page code-behind — the "
                + "'new Dialogs.Contents.X()' scan matched nothing, so this guard proved nothing.");

            var violations = new List<string>();
            foreach (var name in hosted)
            {
                var xamlPath = Path.Combine(contentsDir, name + ".xaml");
                Assert.True(File.Exists(xamlPath),
                    $"{name} is hosted by a settings page but {Path.GetRelativePath(root, xamlPath)} does not exist.");

                // Comments document this very rule, so they must not count as usages.
                var rootTag = RootElementTag(StripComments(File.ReadAllText(xamlPath)));
                foreach (Match m in FixedMinSize.Matches(rootTag))
                {
                    violations.Add($"{Path.GetRelativePath(root, xamlPath)}: {m.Value}");
                }
            }

            Assert.True(violations.Count == 0,
                "A dialog content control hosted by a settings tab still pins its retired modal "
                + "size with MinWidth/MinHeight. The tab's content area is narrower than the old "
                + "dialog and its ScrollViewer disables horizontal scrolling, so the right edge "
                + "(status badge, icon selector) is clipped. Let the host size the content; keep "
                + "only d:DesignWidth/d:DesignHeight for the designer:\n  - "
                + string.Join("\n  - ", violations));
        }

        [Fact]
        public void No_element_declares_visibility_both_locally_and_in_its_own_style()
        {
            var root = ResolvePulsarRoot();
            var violations = new List<string>();

            foreach (var file in EnumerateXaml(root))
            {
                // Comments in these very templates explain the trap, so they must not count.
                var text = StripComments(File.ReadAllText(file));
                foreach (Match m in LocalVisibilityTag.Matches(text))
                {
                    // A local Visibility on the tag already outranks any Style trigger, so a
                    // competing setter inside this element's own <Style> is unreachable. Locate
                    // the element's opening tag, then its matching </Type.Style> (or the next
                    // </Type> as a lower bound) and look for a Visibility Setter in that window.
                    var tag = m.Groups["tag"].Value;
                    var openEnd = m.Index + m.Length;
                    var styleStart = text.IndexOf($"<{tag}.Style", openEnd, StringComparison.Ordinal);
                    if (styleStart < 0) continue;

                    var styleEnd = text.IndexOf($"</{tag}.Style>", styleStart, StringComparison.Ordinal);
                    if (styleEnd < 0) styleEnd = text.IndexOf($"</{tag}", styleStart, StringComparison.Ordinal);
                    if (styleEnd < 0) continue;

                    var styleBlock = text.Substring(styleStart, styleEnd - styleStart);
                    if (StyleVisibilitySetter.IsMatch(styleBlock))
                    {
                        var rel = Path.GetRelativePath(root, file);
                        var line = LineOf(text, m.Index);
                        violations.Add($"{rel}:{line}: <{tag}> has local Visibility=\"{Trim(m.Groups["value"].Value, 60)}\" "
                            + "and a Visibility Setter in its own Style");
                    }
                }
            }

            Assert.True(violations.Count == 0,
                "An element declares Visibility twice — once as a local attribute on the tag and "
                + "again inside its own Style. WPF resolves the local value above the Style "
                + "trigger, so the trigger never fires and both branches render simultaneously "
                + "(this stacked the secret-field TextBox placeholder on top of the picker "
                + "Border's DisplayValue). Move EVERY condition into Style.Triggers and delete the "
                + "local Visibility attribute from the tag:\n  - "
                + string.Join("\n  - ", violations));
        }

        /// <summary>1-based line number of a character offset, for human-readable failures.</summary>
        private static int LineOf(string text, int offset)
        {
            var line = 1;
            for (var i = 0; i < offset && i < text.Length; i++)
            {
                if (text[i] == '\n') line++;
            }
            return line;
        }

        /// <summary>The control's own start tag — inner containers may legitimately carry
        /// <c>MinWidth</c> (e.g. a 90px label column), only the root must not.</summary>
        private static string RootElementTag(string xaml)
        {
            var start = xaml.IndexOf('<');
            while (start >= 0)
            {
                var end = xaml.IndexOf('>', start);
                if (end < 0) return xaml;
                var tag = xaml.Substring(start, end - start + 1);
                if (tag.StartsWith("<UserControl", StringComparison.OrdinalIgnoreCase)) return tag;
                start = xaml.IndexOf('<', end);
            }
            return xaml;
        }

        private static string Trim(string snippet)
        {
            var flat = snippet.Replace("\r", " ").Replace("\n", " ");
            return flat.Length <= 140 ? flat : flat.Substring(0, 140) + "…";
        }

        /// <summary>Collapse a short attribute value for a failure line, capped at
        /// <paramref name="max"/> characters.</summary>
        private static string Trim(string snippet, int max)
        {
            var flat = snippet.Replace("\r", " ").Replace("\n", " ");
            return flat.Length <= max ? flat : flat.Substring(0, max) + "…";
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
