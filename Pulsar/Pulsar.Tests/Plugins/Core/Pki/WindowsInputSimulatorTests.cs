using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Pulsar.Plugins.Core.Pki.Services.Input;

namespace Pulsar.Tests.Plugins.Core.Pki
{
    public class WindowsInputSimulatorTests
    {
        [Fact]
        public async Task SimulateTextForceSendKeysAsync_ShouldEscapeBeforeSending()
        {
            var uiaWriter = new Mock<IUiaTextWriter>();
            var sendKeysWriter = new Mock<ISendKeysWriter>();
            sendKeysWriter.Setup(x => x.EscapeForSendKeys("p}ass")).Returns("p{}}ass");

            var simulator = new WindowsInputSimulator(
                uiaWriter.Object,
                sendKeysWriter.Object,
                NullLogger<WindowsInputSimulator>.Instance);

            await simulator.SimulateTextForceSendKeysAsync("p}ass");

            sendKeysWriter.Verify(x => x.EscapeForSendKeys("p}ass"), Times.Once);
            sendKeysWriter.Verify(x => x.SendWait("p{}}ass"), Times.Once);
        }

        [Fact]
        public async Task TrySimulateTextUiaAsync_ShouldReturnUnderlyingWriterResult()
        {
            var uiaWriter = new Mock<IUiaTextWriter>();
            var sendKeysWriter = new Mock<ISendKeysWriter>();
            uiaWriter.Setup(x => x.TrySetText("secret")).Returns(true);

            var simulator = new WindowsInputSimulator(
                uiaWriter.Object,
                sendKeysWriter.Object,
                NullLogger<WindowsInputSimulator>.Instance);

            var result = await simulator.TrySimulateTextUiaAsync("secret");

            result.Should().BeTrue();
            uiaWriter.Verify(x => x.TrySetText("secret"), Times.Once);
            sendKeysWriter.Verify(x => x.SendWait(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task SimulateKeyAsync_ShouldSendRawKey()
        {
            var uiaWriter = new Mock<IUiaTextWriter>();
            var sendKeysWriter = new Mock<ISendKeysWriter>();

            var simulator = new WindowsInputSimulator(
                uiaWriter.Object,
                sendKeysWriter.Object,
                NullLogger<WindowsInputSimulator>.Instance);

            await simulator.SimulateKeyAsync("{TAB}");

            sendKeysWriter.Verify(x => x.SendWait("{TAB}"), Times.Once);
        }
    }
}
