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
        [InlineData("test")]
        [InlineData("t{e}st")]
        [InlineData("t[e]st")]
        [InlineData("t+e^s%t~")]
        [InlineData("(test)")]
        [InlineData("}}{{")]
        [InlineData("test ")]
        [InlineData("++")]
        [InlineData("^^")]
        [InlineData("%%")]
        [InlineData("~~")]
        [InlineData("((")]
        [InlineData("))")]
        [InlineData("[[")]
        [InlineData("]]")]
        [InlineData("{{")]
        [InlineData("}}")]
        [InlineData("")]
        public void EscapeForSendKeys_ShouldReturnInputUnchanged(string? input)
        {
            string result = _writer.EscapeForSendKeys(input);

            result.Should().Be(input ?? string.Empty);
        }

        [Fact]
        public void EscapeForSendKeys_ShouldReturnEmptyString_WhenInputIsNull()
        {
            string result = _writer.EscapeForSendKeys(null);

            result.Should().BeEmpty();
        }
    }
}
