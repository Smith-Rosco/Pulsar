using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Pulsar.Plugins.Core.Pki.Contracts;
using Pulsar.Plugins.Core.Pki.Models.Execution;
using Pulsar.Plugins.Core.Pki.Services;
using Pulsar.Plugins.Core.Pki.Services.Input;
using Pulsar.Services.Interfaces;

namespace Pulsar.Tests.Plugins.Core.Pki
{
    public class SendKeysInjectionExecutorTests
    {
        [Fact]
        public async Task ExecuteAsync_ShouldReturnFocusRestoreFailure_WhenRestorerReturnsFalse()
        {
            var windowService = new Mock<IWindowService>();
            var focusRestorer = new Mock<IFocusRestorer>();
            var sendKeysWriter = new Mock<ISendKeysWriter>();
            focusRestorer.Setup(x => x.RestoreFocusAsync(It.IsAny<IntPtr>())).ReturnsAsync(false);

            var executor = new SendKeysInjectionExecutor(
                windowService.Object,
                focusRestorer.Object,
                sendKeysWriter.Object,
                NullLogger<SendKeysInjectionExecutor>.Instance);

            var result = await executor.ExecuteAsync(new InjectionPlan(Guid.NewGuid(), new List<InjectionStep>
            {
                new(InjectionStepType.HideLauncher),
                new(InjectionStepType.RestoreFocus, null, 0, new IntPtr(123))
            }));

            result.Success.Should().BeFalse();
            result.Stage.Should().Be(PkiExecutionStage.FocusRestore);
        }

        [Fact]
        public async Task ExecuteAsync_ShouldReturnInjectionFailure_WhenTextSendThrows()
        {
            var windowService = new Mock<IWindowService>();
            var focusRestorer = new Mock<IFocusRestorer>();
            var sendKeysWriter = new Mock<ISendKeysWriter>();
            focusRestorer.Setup(x => x.RestoreFocusAsync(It.IsAny<IntPtr>())).ReturnsAsync(true);
            sendKeysWriter.Setup(x => x.EscapeForSendKeys("secret")).Returns("secret");
            sendKeysWriter.Setup(x => x.SendWait("secret")).Throws(new InvalidOperationException("boom"));

            var executor = new SendKeysInjectionExecutor(
                windowService.Object,
                focusRestorer.Object,
                sendKeysWriter.Object,
                NullLogger<SendKeysInjectionExecutor>.Instance);

            var result = await executor.ExecuteAsync(new InjectionPlan(Guid.NewGuid(), new List<InjectionStep>
            {
                new(InjectionStepType.HideLauncher),
                new(InjectionStepType.RestoreFocus, null, 0, new IntPtr(123)),
                new(InjectionStepType.SendText, "secret")
            }));

            result.Success.Should().BeFalse();
            result.Stage.Should().Be(PkiExecutionStage.Injection);
        }
    }
}
