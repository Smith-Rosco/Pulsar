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
    /// <summary>
    /// Window Inspector 行为测试。[W2] 排除规则的读写改经 IDiscoveryExclusionPolicy
    /// 单一所有者（SetRulesAsync = 应用 + 持久化），判定报告与闪烁仍走 IWindowService。
    /// </summary>
    public class WindowInspectorViewModelTests
    {
        private static WindowEligibilityReport Report(IntPtr hwnd, string title, string process, string className)
            => new(hwnd, title, process, className, null, true, WindowEligibilityVerdict.Eligible);

        private static Mock<IDiscoveryExclusionPolicy> CreatePolicyMock(
            List<WindowEligibilityRule>? initialRules = null,
            List<IReadOnlyList<WindowEligibilityRule>>? setRulesCalls = null)
        {
            var policy = new Mock<IDiscoveryExclusionPolicy>();
            policy.SetupGet(p => p.Rules).Returns(initialRules ?? new List<WindowEligibilityRule>());
            policy.Setup(p => p.SetRulesAsync(It.IsAny<IReadOnlyList<WindowEligibilityRule>>()))
                .Callback<IReadOnlyList<WindowEligibilityRule>>(rules => setRulesCalls?.Add(rules))
                .Returns(Task.CompletedTask);
            return policy;
        }

        private static WindowInspectorViewModel CreateViewModel(
            Mock<IWindowService> windowService,
            Mock<IDiscoveryExclusionPolicy> policy)
            => new(windowService.Object, policy.Object);

        [Fact]
        public async Task InitializeAsync_ShouldPopulateRowsFromReport()
        {
            var windowService = new Mock<IWindowService>();
            windowService.Setup(s => s.GetWindowEligibilityReportAsync()).ReturnsAsync(new List<WindowEligibilityReport>
            {
                Report(new IntPtr(0x100), "Chrome Legacy Window", "chrome", "Chrome_WidgetWin_1"),
                Report(new IntPtr(0x200), "Notepad", "notepad", "NotepadClass")
            });

            var vm = CreateViewModel(windowService, CreatePolicyMock());
            await vm.InitializeAsync();

            vm.Rows.Should().HaveCount(2);
            vm.Rows[0].Title.Should().Be("Chrome Legacy Window");
            vm.Rows[0].HwndText.Should().Be("0x100");
            vm.Rows[0].IsExcluded.Should().BeFalse();
        }

        [Fact]
        public async Task Exclude_ShouldSetRulesThroughPolicy_AndPersist()
        {
            var windowService = new Mock<IWindowService>();
            windowService.Setup(s => s.GetWindowEligibilityReportAsync()).ReturnsAsync(new List<WindowEligibilityReport>
            {
                Report(new IntPtr(0x100), "Chrome Legacy Window", "chrome", "Chrome_WidgetWin_1")
            });

            var setRulesCalls = new List<IReadOnlyList<WindowEligibilityRule>>();
            var vm = CreateViewModel(windowService, CreatePolicyMock(setRulesCalls: setRulesCalls));
            await vm.InitializeAsync();

            await vm.ExcludeCommand.ExecuteAsync(vm.Rows[0]);

            setRulesCalls.Should().HaveCount(1);
            var rule = setRulesCalls[0].Single();
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
            windowService.Setup(s => s.GetWindowEligibilityReportAsync()).ReturnsAsync(new List<WindowEligibilityReport>
            {
                Report(new IntPtr(0x100), "Chrome Legacy Window", "chrome", "Chrome_WidgetWin_1")
            });

            var setRulesCalls = new List<IReadOnlyList<WindowEligibilityRule>>();
            var vm = CreateViewModel(windowService, CreatePolicyMock(
                initialRules: new List<WindowEligibilityRule> { existing },
                setRulesCalls: setRulesCalls));
            await vm.InitializeAsync();

            await vm.ExcludeCommand.ExecuteAsync(vm.Rows[0]);

            setRulesCalls.Should().HaveCount(1);
            setRulesCalls[0].Should().HaveCount(1);
        }

        [Fact]
        public async Task Flash_ShouldCallFlashWindow()
        {
            var windowService = new Mock<IWindowService>();
            windowService.Setup(s => s.GetWindowEligibilityReportAsync()).ReturnsAsync(new List<WindowEligibilityReport>
            {
                Report(new IntPtr(0x100), "Chrome Legacy Window", "chrome", "Chrome_WidgetWin_1")
            });

            var vm = CreateViewModel(windowService, CreatePolicyMock());
            await vm.InitializeAsync();

            vm.FlashCommand.Execute(vm.Rows[0]);

            windowService.Verify(s => s.FlashWindow(new IntPtr(0x100)), Times.Once);
        }
    }
}
