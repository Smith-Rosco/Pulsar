using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Pulsar.Core.Focus;
using Pulsar.Plugins.Core.Pki.Models.Execution;
using Pulsar.Plugins.Core.Pki.Services;
using Pulsar.Plugins.Core.Pki.Services.Input;
using Pulsar.Services.Interfaces;

namespace Pulsar.Tests.Plugins.Core.Pki
{
    public class SendKeysInjectionExecutorTests
    {
        [Fact]
        public async Task ExecuteAsync_ShouldReturnInjectionFailure_OnTimeout()
        {
            SendKeysInjectionExecutor.ExecutionTimeout = TimeSpan.FromMilliseconds(50);

            try
            {
                var windowService = new Mock<IWindowService>();
                var focusManager = new Mock<IFocusManager>();
                var sendKeysWriter = new Mock<ISendKeysWriter>();
                focusManager.Setup(x => x.ActivateWindowAsync(It.IsAny<IntPtr>(), It.IsAny<FocusActivationOptions>()))
                    .ReturnsAsync(new FocusActivationResult { Success = true, VerificationPassed = true });

                var executor = new SendKeysInjectionExecutor(
                    windowService.Object,
                    focusManager.Object,
                    sendKeysWriter.Object,
                    NullLogger<SendKeysInjectionExecutor>.Instance);

                var result = await executor.ExecuteAsync(new InjectionPlan(Guid.NewGuid(), new List<InjectionStep>
                {
                    new(InjectionStepType.HideLauncher),
                    new(InjectionStepType.RestoreFocus, null, 0, new IntPtr(123)),
                    new(InjectionStepType.Delay, null, 5000)
                }));

                result.Success.Should().BeFalse();
                result.Stage.Should().Be(PkiExecutionStage.Injection);
                result.Message.Should().Contain("timed out");
            }
            finally
            {
                SendKeysInjectionExecutor.ExecutionTimeout = TimeSpan.FromSeconds(15);
            }
        }
        [Fact]
        public async Task ExecuteAsync_ShouldReturnFocusRestoreFailure_WhenFocusManagerReturnsFailed()
        {
            var windowService = new Mock<IWindowService>();
            var focusManager = new Mock<IFocusManager>();
            var sendKeysWriter = new Mock<ISendKeysWriter>();
            focusManager.Setup(x => x.ActivateWindowAsync(It.IsAny<IntPtr>(), It.IsAny<FocusActivationOptions>()))
                .ReturnsAsync(new FocusActivationResult { Success = false, VerificationPassed = false });

            var executor = new SendKeysInjectionExecutor(
                windowService.Object,
                focusManager.Object,
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
        public async Task ExecuteAsync_ShouldReturnFocusRestoreFailure_WhenVerificationFails()
        {
            var windowService = new Mock<IWindowService>();
            var focusManager = new Mock<IFocusManager>();
            var sendKeysWriter = new Mock<ISendKeysWriter>();
            focusManager.Setup(x => x.ActivateWindowAsync(It.IsAny<IntPtr>(), It.IsAny<FocusActivationOptions>()))
                .ReturnsAsync(new FocusActivationResult { Success = true, VerificationPassed = false });

            var executor = new SendKeysInjectionExecutor(
                windowService.Object,
                focusManager.Object,
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
            var focusManager = new Mock<IFocusManager>();
            var sendKeysWriter = new Mock<ISendKeysWriter>();
            focusManager.Setup(x => x.ActivateWindowAsync(It.IsAny<IntPtr>(), It.IsAny<FocusActivationOptions>()))
                .ReturnsAsync(new FocusActivationResult { Success = true, VerificationPassed = true });
            sendKeysWriter.Setup(x => x.SendWait("secret")).Throws(new InvalidOperationException("boom"));

            var executor = new SendKeysInjectionExecutor(
                windowService.Object,
                focusManager.Object,
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

        [Fact]
        public async Task ExecuteAsync_ShouldCallSendKeyCombination_ForSendKeyStep()
        {
            var windowService = new Mock<IWindowService>();
            var focusManager = new Mock<IFocusManager>();
            var sendKeysWriter = new Mock<ISendKeysWriter>();
            focusManager.Setup(x => x.ActivateWindowAsync(It.IsAny<IntPtr>(), It.IsAny<FocusActivationOptions>()))
                .ReturnsAsync(new FocusActivationResult { Success = true, VerificationPassed = true });

            var executor = new SendKeysInjectionExecutor(
                windowService.Object,
                focusManager.Object,
                sendKeysWriter.Object,
                NullLogger<SendKeysInjectionExecutor>.Instance);

            await executor.ExecuteAsync(new InjectionPlan(Guid.NewGuid(), new List<InjectionStep>
            {
                new(InjectionStepType.HideLauncher),
                new(InjectionStepType.RestoreFocus, null, 0, new IntPtr(123)),
                new(InjectionStepType.SendKey, "{TAB}")
            }));

            sendKeysWriter.Verify(x => x.SendKeyCombination("{TAB}"), Times.Once);
            sendKeysWriter.Verify(x => x.SendWait(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task ExecuteAsync_ShouldReturnSuccess_WhenAllStepsComplete()
        {
            var windowService = new Mock<IWindowService>();
            var focusManager = new Mock<IFocusManager>();
            var sendKeysWriter = new Mock<ISendKeysWriter>();
            focusManager.Setup(x => x.ActivateWindowAsync(It.IsAny<IntPtr>(), It.IsAny<FocusActivationOptions>()))
                .ReturnsAsync(new FocusActivationResult { Success = true, VerificationPassed = true });

            var executor = new SendKeysInjectionExecutor(
                windowService.Object,
                focusManager.Object,
                sendKeysWriter.Object,
                NullLogger<SendKeysInjectionExecutor>.Instance);

            var result = await executor.ExecuteAsync(new InjectionPlan(Guid.NewGuid(), new List<InjectionStep>
            {
                new(InjectionStepType.HideLauncher),
                new(InjectionStepType.RestoreFocus, null, 0, new IntPtr(123)),
                new(InjectionStepType.SendText, "value")
            }));

            result.Success.Should().BeTrue();
            result.Stage.Should().Be(PkiExecutionStage.Completed);
        }
    }
}
