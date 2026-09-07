using System;
using FluentAssertions;
using Pulsar.Native;
using Pulsar.Services.WindowSwitching;
using Xunit;

namespace Pulsar.Tests.Services
{
    /// <summary>
    /// 前门 <see cref="WindowEligibilityEvaluator"/> 的两阶段词汇（EvaluateStructural /
    /// EvaluateSnapshot）—— C2 收口的判定协议契约测试。快照直接构造（快照即第二个适配器），
    /// 不触碰 native。
    /// </summary>
    public class WindowEligibilityEvaluatorTests
    {
        private static readonly PulsarNative.RECT VirtualScreen = new()
        {
            Left = 0,
            Top = 0,
            Right = 1920,
            Bottom = 1080
        };

        private static WindowEligibilityEvaluator CreateEvaluator(uint ownPid = 999)
            => new(new WindowEligibilityPolicy(ownPid));

        private static WindowEligibilitySnapshot EligibleSnapshot(
            uint pid = 1,
            string processName = "notepad",
            string className = "NotepadClass",
            string title = "")
            => new()
            {
                Hwnd = new IntPtr(42),
                Pid = pid,
                ProcessName = processName,
                ClassName = className,
                Title = title,
                IsVisible = true,
                IsCloaked = false,
                ExStyle = 0,
                Style = 0,
                OwnerHwnd = IntPtr.Zero,
                IsIconic = false,
                Rect = new PulsarNative.RECT { Left = 100, Top = 100, Right = 800, Bottom = 600 },
                VirtualScreenRect = VirtualScreen
            };

        // ===== EvaluateStructural：第一道筛 =====

        [Fact]
        public void EvaluateStructural_OwnPid_ShouldExcludeSelf()
        {
            var evaluator = CreateEvaluator(ownPid: 999);

            var result = evaluator.EvaluateStructural(EligibleSnapshot(pid: 999));

            result.Included.Should().BeFalse();
            result.Verdict.Should().Be(WindowEligibilityVerdict.ExcludedSelf);
        }

        [Fact]
        public void EvaluateStructural_HiddenWindow_ShouldExclude()
        {
            var evaluator = CreateEvaluator();

            var result = evaluator.EvaluateStructural(EligibleSnapshot() with { IsVisible = false });

            result.Included.Should().BeFalse();
            result.Verdict.Should().Be(WindowEligibilityVerdict.ExcludedHidden);
        }

        [Fact]
        public void EvaluateStructural_ShouldIgnoreProcessName()
        {
            var evaluator = CreateEvaluator();
            evaluator.UpdateBlacklist(new[] { "banned.exe" });

            // 结构筛不依赖进程名：黑名单进程只要结构合法，第一道筛必须放行，
            // 由第二道筛（EvaluateSnapshot）按 scope 决定是否排除。
            var result = evaluator.EvaluateStructural(EligibleSnapshot(processName: "banned.exe"));

            result.Included.Should().BeTrue();
            result.Verdict.Should().Be(WindowEligibilityVerdict.Eligible);
        }

        // ===== EvaluateSnapshot：对已建好的快照做完整判定 =====

        [Fact]
        public void EvaluateSnapshot_Discovery_ShouldApplyBlacklistFromEvaluatorState()
        {
            var evaluator = CreateEvaluator();
            evaluator.UpdateBlacklist(new[] { "banned.exe" });

            var result = evaluator.EvaluateSnapshot(
                EligibleSnapshot(processName: "banned.exe"),
                EligibilityScope.Discovery);

            result.Included.Should().BeFalse();
            result.Verdict.Should().Be(WindowEligibilityVerdict.ExcludedBlacklistedProcess);
        }

        [Fact]
        public void EvaluateSnapshot_Explicit_ShouldIgnoreBlacklist()
        {
            var evaluator = CreateEvaluator();
            evaluator.UpdateBlacklist(new[] { "banned.exe" });

            var result = evaluator.EvaluateSnapshot(
                EligibleSnapshot(processName: "banned.exe"),
                EligibilityScope.Explicit);

            result.Included.Should().BeTrue();
            result.Verdict.Should().Be(WindowEligibilityVerdict.Eligible);
        }

        [Fact]
        public void EvaluateSnapshot_ShouldReturnStructuralVerdict_WhenStructureFails()
        {
            var evaluator = CreateEvaluator();

            var result = evaluator.EvaluateSnapshot(
                EligibleSnapshot() with { IsVisible = false },
                EligibilityScope.Discovery);

            result.Included.Should().BeFalse();
            result.Verdict.Should().Be(WindowEligibilityVerdict.ExcludedHidden);
        }

        [Fact]
        public void EvaluateSnapshot_TitleExcludeRule_ShouldExcludeWhenTitleMatches()
        {
            var evaluator = CreateEvaluator();
            evaluator.UpdateRules(new[]
            {
                new WindowEligibilityRule(Allow: false, ProcessName: null, WindowClass: null, TitlePattern: @"^Secret.*$")
            });

            var result = evaluator.EvaluateSnapshot(
                EligibleSnapshot(title: "Secret Vault"),
                EligibilityScope.Discovery);

            result.Included.Should().BeFalse();
            result.Verdict.Should().Be(WindowEligibilityVerdict.ExcludedByRule);
        }

        [Fact]
        public void EvaluateSnapshot_TitleExcludeRule_ShouldPassWhenTitleDoesNotMatch()
        {
            var evaluator = CreateEvaluator();
            evaluator.UpdateRules(new[]
            {
                new WindowEligibilityRule(Allow: false, ProcessName: null, WindowClass: null, TitlePattern: @"^Secret.*$")
            });

            var result = evaluator.EvaluateSnapshot(
                EligibleSnapshot(title: "Ordinary Window"),
                EligibilityScope.Discovery);

            result.Included.Should().BeTrue();
        }

        // ===== 状态透传 =====

        [Fact]
        public void HasTitleDependentRules_ShouldReflectRules()
        {
            var evaluator = CreateEvaluator();

            evaluator.HasTitleDependentRules.Should().BeFalse();

            evaluator.UpdateRules(new[]
            {
                new WindowEligibilityRule(Allow: false, ProcessName: null, WindowClass: "SomeClass", TitlePattern: null)
            });

            evaluator.HasTitleDependentRules.Should().BeFalse();

            evaluator.UpdateRules(new[]
            {
                new WindowEligibilityRule(Allow: false, ProcessName: null, WindowClass: null, TitlePattern: "SomeTitle")
            });

            evaluator.HasTitleDependentRules.Should().BeTrue();
        }

        [Fact]
        public void UpdateBlacklist_ShouldReplaceWholeBlacklist()
        {
            var evaluator = CreateEvaluator();

            evaluator.UpdateBlacklist(new[] { "a.exe", "b.exe" });
            evaluator.UpdateBlacklist(new[] { "c.exe" });

            // 整体替换而非合并：a/b 不再被排除。
            evaluator.EvaluateSnapshot(EligibleSnapshot(processName: "a.exe"), EligibilityScope.Discovery)
                .Included.Should().BeTrue();
            evaluator.EvaluateSnapshot(EligibleSnapshot(processName: "c.exe"), EligibilityScope.Discovery)
                .Included.Should().BeFalse();
        }
    }
}
