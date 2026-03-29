using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Pulsar.Plugins.Core.Pki.Services.Input;
using Xunit;

namespace Pulsar.Tests.Plugins.Core.Pki
{
    public class WindowsSendKeysWriterTests
    {
        private readonly WindowsSendKeysWriter _writer;

        public WindowsSendKeysWriterTests()
        {
            _writer = new WindowsSendKeysWriter(NullLogger<WindowsSendKeysWriter>.Instance);
        }

        [Theory]
        [InlineData("test", "test")]
        [InlineData("t{e}st", "t{{}e{}}st")]
        [InlineData("t[e]st", "t{[}e{]}st")]
        [InlineData("t+e^s%t~", "t{+}e{^}s{%}t{~}")]
        [InlineData("(test)", "{(}test{)}")]
        [InlineData("}}{{", "{}}{}}{{}{{}")]
        [InlineData("test ", "test ")]
        [InlineData("++", "{+}{+}")]
        [InlineData("^^", "{^}{^}")]
        [InlineData("%%", "{%}{%}")]
        [InlineData("~~", "{~}{~}")]
        [InlineData("((", "{(}{(}")]
        [InlineData("))", "{)}{)}")]
        [InlineData("[[", "{[}{[}")]
        [InlineData("]]", "{]}{]}")]
        [InlineData("{{", "{{}{{}")]
        [InlineData("}}", "{}}{}}")]
        [InlineData("", "")]
        public void EscapeForSendKeys_ShouldEscapeSpecialCharactersCorrectly(string? input, string? expected)
        {
            // Act
            string result = _writer.EscapeForSendKeys(input);

            // Assert
            result.Should().Be(expected);
        }

        [Fact]
        public void EscapeForSendKeys_ShouldReturnEmptyString_WhenInputIsNull()
        {
            string result = _writer.EscapeForSendKeys(null);

            result.Should().BeEmpty();
        }
    }
}
