// [Path]: Pulsar/Pulsar.Tests/UI/XamlIconTokenValidityTests.cs
//
// Regression guard for the "settings page crashes on navigation due to invalid
// SymbolIcon token" bug family (e.g. Refresh24 / Download24 / DialPad24 typos).
// Scans every XAML file in Pulsar/Views and asserts that every icon string
// token is a valid Wpf.Ui SymbolRegular enum member. Catches the whole class
// of runtime XamlParseException crashes at test time, not at navigation time.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Wpf.Ui.Controls;
using Xunit;

namespace Pulsar.Tests.UI
{
    public sealed class XamlIconTokenValidityTests
    {
        // Token sources we treat as the same enum-converter path: any of these
        // will throw XamlParseException at the first invalid value encountered
        // when WPF parses the file. {ui:SymbolIcon X} markup on a property of
        // type SymbolRegular? is the dominant crash surface; CardExpander
        // Icon="X" / CardControl Icon="X" / <ui:SymbolIcon Symbol="X"/> are
        // also routed through the same SymbolRegular converter.
        private static readonly Regex[] TokenPatterns = new[]
        {
            new Regex(@"\{ui:SymbolIcon\s+(?<t>[A-Za-z][A-Za-z0-9]+)\}", RegexOptions.Compiled),
            new Regex(@"\bSymbol=""(?<t>[A-Za-z][A-Za-z0-9]+)""", RegexOptions.Compiled),
            new Regex(@"\bIcon=""\s*\{ui:SymbolIcon\s+(?<t>[A-Za-z][A-Za-z0-9]+)\}\s*""", RegexOptions.Compiled),
        };

        private static readonly Regex ExcludedFile = new(
            @"\\(bin|obj)\\", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        [Fact]
        public void All_xaml_icon_tokens_are_valid_SymbolRegular_members()
        {
            // Test assembly runs next to the Pulsar project; fall back to a
            // relative walk so the test is robust to test-runner CWD.
            var viewsDir = ResolveViewsDir();
            Assert.True(Directory.Exists(viewsDir),
                $"Views directory not found at '{viewsDir}'.");

            var xamlFiles = Directory.EnumerateFiles(viewsDir, "*.xaml", SearchOption.AllDirectories)
                .Where(p => !ExcludedFile.IsMatch(p))
                .ToList();

            Assert.NotEmpty(xamlFiles);

            var validMembers = new HashSet<string>(Enum.GetNames<SymbolRegular>());
            var invalid = new List<(string file, string token)>();

            foreach (var file in xamlFiles)
            {
                var text = File.ReadAllText(file);
                foreach (var pattern in TokenPatterns)
                {
                    foreach (Match m in pattern.Matches(text))
                    {
                        var token = m.Groups["t"].Value;
                        if (!validMembers.Contains(token))
                        {
                            invalid.Add((Path.GetRelativePath(viewsDir, file), token));
                        }
                    }
                }
            }

            Assert.True(invalid.Count == 0,
                "Invalid SymbolRegular icon tokens found in XAML (each will cause "
                + "a XamlParseException on page load):\n  - "
                + string.Join("\n  - ", invalid.Select(v => $"{v.file}: {v.token}")));
        }

        private static string ResolveViewsDir()
        {
            // Walk up from the test assembly's working dir until we find a
            // Pulsar/Pulsar/Views folder; this works whether the runner is the
            // solution root or the test project.
            var probe = new DirectoryInfo(AppContext.BaseDirectory);
            while (probe != null)
            {
                var candidate = Path.Combine(probe.FullName, "Pulsar", "Pulsar", "Views");
                if (Directory.Exists(candidate)) return candidate;
                candidate = Path.Combine(probe.FullName, "Views");
                if (Directory.Exists(candidate) && probe.Name == "Pulsar") return candidate;
                probe = probe.Parent;
            }
            return Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Pulsar", "Pulsar", "Views");
        }
    }
}
