using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Pulsar.Services.Interfaces;
using Pulsar.Services.WindowSwitching;
using Pulsar.ViewModels.Dialogs;
using Xunit;

namespace Pulsar.Tests.ViewModels
{
    public class WindowInspectorViewModelTests
    {
        private static WindowEligibilityReport Report(IntPtr hwnd, string title, string process, string className)
            => new(hwnd, title, process, className, null, true, WindowEligibilityVerdict.Eligible);

        private static WindowInspectorViewModel CreateViewModel(
            Mock<IWindowService> windowService,
            List<WindowEligibilityRule>? persisted = null)
        {
            return new WindowInspectorViewModel(
                windowService.Object,
                new Mock<IConfigService>().Object,
                persistRules: rules =>
                {
                    persisted?.Clear();
                    persisted?.AddRange(rules);
                    return Task.CompletedTask;
                });
        }

        [Fact]
        public async Task InitializeAsync_ShouldPopulateRowsFromReport()
        {
            var windowService = new Mock<IWindowService>();
            windowService.Setup(s => s.GetEligibilityRules()).Returns(new List<WindowEligibilityRule>());
            windowService.Setup(s => s.GetWindowEligibilityReportAsync()).ReturnsAsync(new List<WindowEligibilityReport>
            {
                Report(new IntPtr(0x100), "Chrome Legacy Window", "chrome", "Chrome_WidgetWin_1"),
                Report(new IntPtr(0x200), "Notepad", "notepad", "NotepadClass")
            });

            var vm = CreateViewModel(windowService);
            await vm.InitializeAsync();

            vm.Rows.Should().HaveCount(2);
            vm.Rows[0].Title.Should().Be("Chrome Legacy Window");
            vm.Rows[0].HwndText.Should().Be("0x100");
            vm.Rows[0].IsExcluded.Should().BeFalse();
        }

        [Fact]
        public async Task Exclude_ShouldPushSpecificRule_AndPersist()
        {
            var windowService = new Mock<IWindowService>();
            windowService.Setup(s => s.GetEligibilityRules()).Returns(new List<WindowEligibilityRule>());
            windowService.Setup(s => s.GetWindowEligibilityReportAsync()).ReturnsAsync(new List<WindowEligibilityReport>
            {
                Report(new IntPtr(0x100), "Chrome Legacy Window", "chrome", "Chrome_WidgetWin_1")
            });

            var persisted = new List<WindowEligibilityRule>();
            var vm = CreateViewModel(windowService, persisted);
            await vm.InitializeAsync();

            await vm.ExcludeCommand.ExecuteAsync(vm.Rows[0]);

            windowService.Verify(s => s.UpdateEligibilityRules(It.IsAny<IReadOnlyList<WindowEligibilityRule>>()), Times.Once);
            persisted.Should().HaveCount(1);
            var rule = persisted[0];
            rule.Allow.Should().BeFalse();
            rule.ProcessName.Should().Be("chrome");
            rule.WindowClass.Should().Be("Chrome_WidgetWin_1");
            rule.TitlePattern.Should().Be("^Chrome\\ Legacy\\ Window$");
        }

        [Fact]
        public async Task Exclude_ShouldNotDuplicateAnExistingRule()
        {
            var existing = new WindowEligibilityRule(false, "chrome", "Chrome_WidgetWin_1", "^Chrome\\ Legacy\\ Window$");
            var windowService = new Mock<IWindowService>();
            windowService.Setup(s => s.GetEligibilityRules()).Returns(new List<WindowEligibilityRule> { existing });
            windowService.Setup(s => s.GetWindowEligibilityReportAsync()).ReturnsAsync(new List<WindowEligibilityReport>
            {
                Report(new IntPtr(0x100), "Chrome Legacy Window", "chrome", "Chrome_WidgetWin_1")
            });

            var persisted = new List<WindowEligibilityRule>();
            var vm = CreateViewModel(windowService, persisted);
            await vm.InitializeAsync();

            await vm.ExcludeCommand.ExecuteAsync(vm.Rows[0]);

            persisted.Should().HaveCount(1);
        }

        [Fact]
        public async Task Flash_ShouldCallFlashWindow()
        {
            var windowService = new Mock<IWindowService>();
            windowService.Setup(s => s.GetEligibilityRules()).Returns(new List<WindowEligibilityRule>());
            windowService.Setup(s => s.GetWindowEligibilityReportAsync()).ReturnsAsync(new List<WindowEligibilityReport>
            {
                Report(new IntPtr(0x100), "Chrome Legacy Window", "chrome", "Chrome_WidgetWin_1")
            });

            var vm = CreateViewModel(windowService);
            await vm.InitializeAsync();

            vm.FlashCommand.Execute(vm.Rows[0]);

            windowService.Verify(s => s.FlashWindow(new IntPtr(0x100)), Times.Once);
        }
    }
}
