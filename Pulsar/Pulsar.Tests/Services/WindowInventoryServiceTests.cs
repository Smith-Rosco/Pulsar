using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Pulsar.Models;
using Pulsar.Services.Interfaces;
using Pulsar.Services.WindowSwitching;
using Xunit;

namespace Pulsar.Tests.Services
{
    /// <summary>
    /// <see cref="WindowInventoryService"/> 的专属测试（C2 收口后补网）。
    /// 枚举走真实 native EnumWindows，但判定全部走 mocked 前门 —— 只钉
    /// <b>两阶段协议</b>（结构筛早退、身份筛只在幸存窗口上发生）与入口行为；
    /// 需要解析真实进程元数据的身份/标题阶段无法在单测中确定性构造，由
    /// 前门词汇测试（<see cref="WindowEligibilityEvaluatorTests"/>）+ 真机回归覆盖。
    /// </summary>
    public class WindowInventoryServiceTests
    {
        private static Mock<IWindowEligibilityEvaluator> CreateExcludingEvaluator()
        {
            var evaluator = new Mock<IWindowEligibilityEvaluator>();
            evaluator
                .Setup(e => e.EvaluateStructural(It.IsAny<WindowEligibilitySnapshot>()))
                .Returns(new EligibilityResult(false, WindowEligibilityVerdict.ExcludedHidden));
            return evaluator;
        }

        [Fact]
        public async Task GetActiveWindowsAsync_WhenStructuralFilterExcludesEverything_ShouldReturnEmptyAndSkipIdentityPhase()
        {
            var evaluator = CreateExcludingEvaluator();
            var inventory = new WindowInventoryService(evaluator.Object);
            var snapshotWindow = new Mock<Func<IntPtr, WindowTrackingSnapshot>>();
            var extractIcon = new Mock<Func<string, System.Windows.Media.ImageSource?>>();

            var windows = await inventory.GetActiveWindowsAsync(
                snapshotWindow.Object,
                extractIcon.Object,
                processRegistryService: null);

            windows.Should().BeEmpty();
            // 两阶段协议：结构筛淘汰后不得进入身份阶段，也不得读取标题/图标/跟踪快照。
            evaluator.Verify(e => e.EvaluateSnapshot(It.IsAny<WindowEligibilitySnapshot>(), It.IsAny<EligibilityScope>()), Times.Never);
            snapshotWindow.VerifyNoOtherCalls();
            extractIcon.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task GetRunningProcessesAsync_WhenStructuralFilterExcludesEverything_ShouldReturnEmptyAndSkipIdentityPhase()
        {
            var evaluator = CreateExcludingEvaluator();
            var inventory = new WindowInventoryService(evaluator.Object);

            var processes = await inventory.GetRunningProcessesAsync();

            processes.Should().BeEmpty();
            evaluator.Verify(e => e.EvaluateSnapshot(It.IsAny<WindowEligibilitySnapshot>(), It.IsAny<EligibilityScope>()), Times.Never);
        }

        [Fact]
        public async Task GetRunningProcessNamesAsync_WhenStructuralFilterExcludesEverything_ShouldReturnEmpty()
        {
            var evaluator = CreateExcludingEvaluator();
            var inventory = new WindowInventoryService(evaluator.Object);

            var names = await inventory.GetRunningProcessNamesAsync();

            names.Should().BeEmpty();
        }

        [Fact]
        public async Task GetProcessWindowsAsync_UnknownProcessId_ShouldReturnEmpty()
        {
            var evaluator = CreateExcludingEvaluator();
            var inventory = new WindowInventoryService(evaluator.Object);

            var windows = await inventory.GetProcessWindowsAsync(
                targetProcessId: int.MaxValue,
                snapshotWindow: _ => new WindowTrackingSnapshot(),
                extractIcon: _ => null);

            windows.Should().BeEmpty();
        }
    }
}
