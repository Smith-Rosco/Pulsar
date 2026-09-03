using System.Collections.Generic;
using FluentAssertions;
using Pulsar.Plugins.Extensions.BookmarkletRunner;

namespace Pulsar.Tests.Plugins.Extensions.BookmarkletRunner
{
    public class ScriptInterpolatorTests
    {
        [Fact]
        public void Interpolate_ReplacesPlaceholderWithArgumentValue()
        {
            var args = new Dictionary<string, string> { ["user"] = "alice" };

            var result = ScriptInterpolator.Interpolate("alert('{{user}}');", args);

            result.Content.Should().Be("alert('alice');");
            result.MissingPlaceholders.Should().BeEmpty();
        }

        [Fact]
        public void Interpolate_ReplacesMultiplePlaceholders()
        {
            var args = new Dictionary<string, string>
            {
                ["user"] = "bob",
                ["site"] = "intranet"
            };

            var result = ScriptInterpolator.Interpolate("var u='{{user}}'; var s='{{site}}';", args);

            result.Content.Should().Be("var u='bob'; var s='intranet';");
        }

        [Fact]
        public void Interpolate_LeavesMissingPlaceholder_AndReportsIt()
        {
            var args = new Dictionary<string, string> { ["known"] = "x" };

            var result = ScriptInterpolator.Interpolate("alert('{{unknown}}');", args);

            result.MissingPlaceholders.Should().Contain("unknown");
            result.Content.Should().Contain("{{unknown}}");
        }

        [Fact]
        public void Interpolate_EscapedDoubleBraces_BecomeLiteralBrace()
        {
            var args = new Dictionary<string, string> { ["user"] = "alice" };

            var result = ScriptInterpolator.Interpolate("var s = '{{{{'; alert('{{user}}');", args);

            result.Content.Should().Be("var s = '{{'; alert('alice');");
            result.MissingPlaceholders.Should().BeEmpty();
        }

        [Fact]
        public void Interpolate_EmptyContent_ReturnsEmpty()
        {
            var result = ScriptInterpolator.Interpolate(string.Empty, new Dictionary<string, string>());

            result.Content.Should().BeEmpty();
            result.MissingPlaceholders.Should().BeEmpty();
        }

        [Fact]
        public void Interpolate_MissingValue_MatchesCaseInsensitively()
        {
            var args = new Dictionary<string, string> { ["User"] = "carol" };

            var result = ScriptInterpolator.Interpolate("alert('{{user}}');", args);

            result.Content.Should().Be("alert('carol');");
            result.MissingPlaceholders.Should().BeEmpty();
        }
    }
}
