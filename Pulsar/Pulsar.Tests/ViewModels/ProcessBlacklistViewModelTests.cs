using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Pulsar.Helpers;
using Pulsar.Models;
using Pulsar.Services.Interfaces;
using Pulsar.ViewModels.Dialogs;

namespace Pulsar.Tests.ViewModels
{
    public class ProcessBlacklistViewModelTests
    {
        [Fact]
        public async Task Constructor_ShouldBuildListFromRegistryAndLightweightRunningState()
        {
            var windowService = new Mock<IWindowService>(MockBehavior.Strict);
            var processRegistryService = new Mock<IProcessRegistryService>(MockBehavior.Strict);

            processRegistryService
                .Setup(service => service.GetAllProcessesAsync())
                .ReturnsAsync(new List<ProcessRegistryEntry>
                {
                    new() { ProcessName = "chrome", DisplayName = "Google Chrome", LastSeen = new DateTime(2026, 4, 1), IsBlacklisted = false },
                    new() { ProcessName = "notepad", DisplayName = "Notepad", LastSeen = new DateTime(2026, 4, 2), IsBlacklisted = false }
                });

            windowService
                .Setup(service => service.GetRunningProcessesAsync())
                .ReturnsAsync(new List<RunningProcessInfo>
                {
                    new() { ProcessName = "notepad", ExePath = string.Empty }
                });

            processRegistryService
                .Setup(service => service.GetIconAsync(It.IsAny<string>()))
                .ReturnsAsync((System.Windows.Media.ImageSource?)null);

            windowService
                .Setup(service => service.GetActiveWindowsAsync())
                .Throws(new InvalidOperationException("Full discovery should not be used by the blacklist dialog."));

            var viewModel = new ProcessBlacklistViewModel(windowService.Object, processRegistryService.Object, "chrome");

            await WaitForAsync(() => viewModel.Processes.Count == 2 && viewModel.Processes.All(item => item.HasResolvedIcon));

            viewModel.IsLoading.Should().BeFalse();
            viewModel.Processes.Select(item => item.ProcessName).Should().Equal("chrome", "notepad");
            viewModel.Processes[0].IsBlacklisted.Should().BeTrue();
            viewModel.Processes[0].IsRunning.Should().BeFalse();
            viewModel.Processes[1].IsRunning.Should().BeTrue();

            windowService.Verify(service => service.GetRunningProcessesAsync(), Times.Once);
            windowService.Verify(service => service.GetActiveWindowsAsync(), Times.Never);
        }

        [Fact]
        public async Task Constructor_ShouldExposeRowsBeforeDeferredIconsComplete()
        {
            var windowService = new Mock<IWindowService>(MockBehavior.Strict);
            var processRegistryService = new Mock<IProcessRegistryService>(MockBehavior.Strict);
            var iconGate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            processRegistryService
                .Setup(service => service.GetAllProcessesAsync())
                .ReturnsAsync(new List<ProcessRegistryEntry>
                {
                    new() { ProcessName = "chrome", DisplayName = "Google Chrome", LastSeen = new DateTime(2026, 4, 1) }
                });

            windowService
                .Setup(service => service.GetRunningProcessesAsync())
                .ReturnsAsync(new List<RunningProcessInfo>());

            processRegistryService
                .Setup(service => service.GetIconAsync("chrome"))
                .Returns(async () =>
                {
                    await iconGate.Task;
                    return (System.Windows.Media.ImageSource?)null;
                });

            var viewModel = new ProcessBlacklistViewModel(windowService.Object, processRegistryService.Object, string.Empty);

            await WaitForAsync(() => viewModel.Processes.Count == 1);

            viewModel.IsLoading.Should().BeFalse();
            viewModel.Processes[0].ProcessName.Should().Be("chrome");
            viewModel.Processes[0].Icon.Should().NotBeNull();
            viewModel.Processes[0].HasResolvedIcon.Should().BeFalse();

            iconGate.SetResult(true);

            await WaitForAsync(() => viewModel.Processes[0].HasResolvedIcon);
        }

        [Fact]
        public async Task Constructor_ShouldShowRunningProcesses_WhenRegistryIsEmpty()
        {
            var windowService = new Mock<IWindowService>(MockBehavior.Strict);
            var processRegistryService = new Mock<IProcessRegistryService>(MockBehavior.Strict);

            processRegistryService
                .Setup(service => service.GetAllProcessesAsync())
                .ReturnsAsync(new List<ProcessRegistryEntry>());

            windowService
                .Setup(service => service.GetRunningProcessesAsync())
                .ReturnsAsync(new List<RunningProcessInfo>
                {
                    new() { ProcessName = "notepad", ExePath = string.Empty },
                    new() { ProcessName = "chrome", ExePath = string.Empty }
                });

            processRegistryService
                .Setup(service => service.GetIconAsync(It.IsAny<string>()))
                .ReturnsAsync((System.Windows.Media.ImageSource?)null);

            var viewModel = new ProcessBlacklistViewModel(windowService.Object, processRegistryService.Object, string.Empty);

            await WaitForAsync(() => viewModel.Processes.Count == 2 && viewModel.Processes.All(item => item.HasResolvedIcon));

            viewModel.Processes.Select(item => item.ProcessName)
                .Should().BeEquivalentTo(new[] { "notepad", "chrome" });
            viewModel.Processes.Should().OnlyContain(item => item.IsRunning);
        }

        [Fact]
        public async Task Constructor_ShouldUseRunningExecutablePathForDeferredIcons_WhenRegistryIsEmpty()
        {
            var windowService = new Mock<IWindowService>(MockBehavior.Strict);
            var processRegistryService = new Mock<IProcessRegistryService>(MockBehavior.Strict);

            processRegistryService
                .Setup(service => service.GetAllProcessesAsync())
                .ReturnsAsync(new List<ProcessRegistryEntry>());

            windowService
                .Setup(service => service.GetRunningProcessesAsync())
                .ReturnsAsync(new List<RunningProcessInfo>
                {
                    new() { ProcessName = "powershell", ExePath = Environment.ProcessPath ?? string.Empty }
                });

            processRegistryService
                .Setup(service => service.GetIconAsync("powershell"))
                .ReturnsAsync((System.Windows.Media.ImageSource?)null);

            var viewModel = new ProcessBlacklistViewModel(windowService.Object, processRegistryService.Object, string.Empty);

            await WaitForAsync(() => viewModel.Processes.Count == 1 && viewModel.Processes[0].HasResolvedIcon);

            viewModel.Processes[0].ExecutablePath.Should().Be(Environment.ProcessPath);
            viewModel.Processes[0].Icon.Should().NotBeNull();
        }

        private static async Task WaitForAsync(Func<bool> condition, int timeoutMs = 3000)
        {
            var started = DateTime.UtcNow;
            while (!condition())
            {
                if ((DateTime.UtcNow - started).TotalMilliseconds > timeoutMs)
                {
                    throw new TimeoutException("Condition was not met in time.");
                }

                await Task.Delay(25);
            }
        }
    }
}
