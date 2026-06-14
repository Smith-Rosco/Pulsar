using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Pulsar.Core.Focus;
using Pulsar.Core.Localization;
using Pulsar.Core.Plugin;
using Pulsar.Plugins.Extensions.Command;
using Pulsar.Services.Interfaces;
using Pulsar.Tests.TestHelpers;

namespace Pulsar.Tests.Plugins.Command
{
    public class CommandPluginTests
    {
        private readonly Mock<IKeySender> _keySenderMock = new();
        private readonly Mock<IProcessLauncher> _processLauncherMock = new();
        private readonly Mock<ILocalizationService> _locMock = new();
        private readonly Mock<IWindowService> _windowServiceMock = new();
        private readonly Mock<IFocusManager> _focusManagerMock = new();

        public CommandPluginTests()
        {
            _locMock.Setup(l => l[It.IsAny<string>()]).Returns((string key) => key);
            _locMock.Setup(l => l.GetString(It.IsAny<string>())).Returns((string key) => key);
            _focusManagerMock
                .Setup(f => f.ActivateWindowAsync(It.IsAny<IntPtr>(), It.IsAny<FocusActivationOptions?>()))
                .ReturnsAsync(new FocusActivationResult { Success = true, VerificationPassed = true });
        }

        private CommandPlugin CreatePlugin()
        {
            return new CommandPlugin(
                NullLogger<CommandPlugin>.Instance,
                _keySenderMock.Object,
                _processLauncherMock.Object,
                _locMock.Object,
                _windowServiceMock.Object,
                _focusManagerMock.Object);
        }

        [Fact]
        public async Task RunCommandAsync_WithPath_LaunchesProcess()
        {
            var plugin = CreatePlugin();
            var args = new Dictionary<string, string> { ["path"] = "notepad.exe" };
            var context = PulsarContextFactory.CreateTestContext();

            await plugin.ExecuteAsync("run", args, context);

            _processLauncherMock.Verify(
                l => l.Launch(It.Is<System.Diagnostics.ProcessStartInfo>(p => p.FileName == "notepad.exe")),
                Times.Once);
        }

        [Fact]
        public async Task RunCommandAsync_WithArguments_SetsArguments()
        {
            var plugin = CreatePlugin();
            var args = new Dictionary<string, string>
            {
                ["path"] = "notepad.exe",
                ["arguments"] = "readme.txt"
            };
            var context = PulsarContextFactory.CreateTestContext();

            await plugin.ExecuteAsync("run", args, context);

            _processLauncherMock.Verify(
                l => l.Launch(It.Is<System.Diagnostics.ProcessStartInfo>(
                    p => p.FileName == "notepad.exe" && p.Arguments == "readme.txt")),
                Times.Once);
        }

        [Fact]
        public async Task RunCommandAsync_WithWorkingDirectory_SetsWorkingDirectory()
        {
            var plugin = CreatePlugin();
            var args = new Dictionary<string, string>
            {
                ["path"] = "cmd.exe",
                ["workingDir"] = "C:\\Temp"
            };
            var context = PulsarContextFactory.CreateTestContext();

            await plugin.ExecuteAsync("run", args, context);

            _processLauncherMock.Verify(
                l => l.Launch(It.Is<System.Diagnostics.ProcessStartInfo>(
                    p => p.WorkingDirectory == "C:\\Temp")),
                Times.Once);
        }

        [Fact]
        public async Task RunCommandAsync_WithoutPath_ReturnsError_AndDoesNotLaunch()
        {
            var plugin = CreatePlugin();
            var args = new Dictionary<string, string>();
            var context = PulsarContextFactory.CreateTestContext();

            var result = await plugin.ExecuteAsync("run", args, context);

            result.Success.Should().BeFalse();
            _processLauncherMock.Verify(l => l.Launch(It.IsAny<System.Diagnostics.ProcessStartInfo>()), Times.Never);
        }

        [Fact]
        public async Task RunCommandAsync_LaunchThrows_ReturnsError()
        {
            _processLauncherMock
                .Setup(l => l.Launch(It.IsAny<System.Diagnostics.ProcessStartInfo>()))
                .Throws(new Win32Exception("Access denied"));

            _locMock.Setup(l => l["Plugin.Command.Error.ExecutionFailed"])
                .Returns("Execution failed: {0}");

            var plugin = CreatePlugin();
            var args = new Dictionary<string, string> { ["path"] = "restricted.exe" };
            var context = PulsarContextFactory.CreateTestContext();

            var result = await plugin.ExecuteAsync("run", args, context);

            result.Success.Should().BeFalse();
            result.Message.Should().Contain("Access denied");
        }

        [Fact]
        public async Task SendKeysAsync_WithKeys_CallsKeySenderExecute()
        {
            var plugin = CreatePlugin();
            var args = new Dictionary<string, string> { ["keys"] = "hello" };
            var handle = new IntPtr(0x12345);
            var context = PulsarContextFactory.CreateTestContext(targetWindowHandle: handle);

            var result = await plugin.ExecuteAsync("sendkeys", args, context);

            result.Success.Should().BeTrue();
            _keySenderMock.Verify(
                k => k.ExecuteAsync(It.IsAny<IReadOnlyList<KeyInstruction>>(), It.IsAny<CancellationToken>()),
                Times.Once);
            _windowServiceMock.Verify(w => w.HideMainWindow(), Times.Once);
            _focusManagerMock.Verify(
                f => f.ActivateWindowAsync(handle, It.IsAny<FocusActivationOptions?>()),
                Times.Once);
        }

        [Fact]
        public async Task SendKeysAsync_WithoutKeys_ReturnsError()
        {
            var plugin = CreatePlugin();
            var args = new Dictionary<string, string>();
            var context = PulsarContextFactory.CreateTestContext();

            var result = await plugin.ExecuteAsync("sendkeys", args, context);

            result.Success.Should().BeFalse();
            _keySenderMock.Verify(
                k => k.ExecuteAsync(It.IsAny<IReadOnlyList<KeyInstruction>>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Fact]
        public async Task SendKeysAsync_WithCancellationToken_StopsExecution()
        {
            var cts = new CancellationTokenSource();
            cts.Cancel();

            var plugin = CreatePlugin();
            var args = new Dictionary<string, string> { ["keys"] = "test" };
            var context = PulsarContextFactory.CreateTestContext();

            var result = await plugin.ExecuteAsync("sendkeys", args, context, cts.Token);

            result.Success.Should().BeFalse();
            result.Message.Should().Contain("Plugin.Command.Error.Cancelled");
        }

        [Fact]
        public async Task ExecuteAsync_UnknownAction_ReturnsError()
        {
            var plugin = CreatePlugin();
            var args = new Dictionary<string, string>();
            var context = PulsarContextFactory.CreateTestContext();

            var result = await plugin.ExecuteAsync("unknown", args, context);

            result.Success.Should().BeFalse();
        }
    }
}
