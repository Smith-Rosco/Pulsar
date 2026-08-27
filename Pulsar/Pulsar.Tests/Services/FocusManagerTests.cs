using System;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Pulsar.Core.Focus;
using Pulsar.Services;
using Pulsar.Services.Interfaces;

namespace Pulsar.Tests.Services
{
    public class FocusManagerTests
    {
        [Fact]
        public async Task ActivateWindowAsync_ShouldFailWhenForegroundDoesNotMatchTarget()
        {
            var target = new IntPtr(42);
            var otherWindow = new IntPtr(84);
            var native = new Mock<IFocusNativeAdapter>();
            native.Setup(adapter => adapter.IsWindow(target)).Returns(true);
            native.Setup(adapter => adapter.IsIconic(target)).Returns(false);
            native.Setup(adapter => adapter.GetWindowThreadProcessId(target, out It.Ref<uint>.IsAny))
                .Returns((IntPtr _, out uint pid) =>
                {
                    pid = 1;
                    return 1u;
                });
            native.Setup(adapter => adapter.GetCurrentThreadId()).Returns(2);
            native.Setup(adapter => adapter.SendInputMouse()).Returns(1);
            native.Setup(adapter => adapter.SetForegroundWindowNative(target)).Returns(true);
            native.Setup(adapter => adapter.GetForegroundWindow()).Returns(otherWindow);
            var manager = new FocusManager(native.Object, Mock.Of<ILogger<FocusManager>>());

            var result = await manager.ActivateWindowAsync(target);

            result.Success.Should().BeFalse();
            result.FailureReason.Should().Be(FocusActivationFailureReason.VerificationFailed);
            result.ActualForegroundAfterActivation.Should().Be(otherWindow);
        }
    }
}
