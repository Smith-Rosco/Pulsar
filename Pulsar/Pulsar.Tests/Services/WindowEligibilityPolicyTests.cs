using System;
using System.Collections.Generic;
using FluentAssertions;
using Pulsar.Native;
using Pulsar.Services.WindowSwitching;
using Xunit;

namespace Pulsar.Tests.Services
{
    public class WindowEligibilityPolicyTests
    {
        private static readonly PulsarNative.RECT VirtualScreen = new()
        {
            Left = 0,
            Top = 0,
            Right = 1920,
            Bottom = 1080
        };

        private static WindowEligibilityPolicy CreatePolicy(
            uint ownPid = 999,
            IReadOnlySet<string>? classBlacklist = null)
            => new(ownPid, classBlacklist);

        private static WindowEligibilitySnapshot EligibleSnapshot(
            IntPtr? hwnd = null,
            uint pid = 1,
            string processName = "notepad",
            string className = "NotepadClass",
            PulsarNative.RECT? rect = null)
            => new()
            {
                Hwnd = hwnd ?? new IntPtr(42),
                Pid = pid,
                ProcessName = processName,
                ClassName = className,
                IsVisible = true,
                IsCloaked = false,
                ExStyle = 0,
                OwnerHwnd = IntPtr.Zero,
                IsIconic = false,
                Rect = rect ?? new PulsarNative.RECT { Left = 100, Top = 100, Right = 800, Bottom = 600 },
                VirtualScreenRect = VirtualScreen
            };

        [Fact]
        public void NormalOnScreenWindow_ShouldBeEligible()
        {
            var policy = CreatePolicy();

            var result = policy.Evaluate(EligibleSnapshot());

            result.Included.Should().BeTrue();
            result.Verdict.Should().Be(WindowEligibilityVerdict.Eligible);
        }

        [Fact]
        public void HiddenWindow_ShouldBeExcluded()
        {
            var policy = CreatePolicy();

            var result = policy.Evaluate(EligibleSnapshot() with { IsVisible = false });

            result.Included.Should().BeFalse();
            result.Verdict.Should().Be(WindowEligibilityVerdict.ExcludedHidden);
        }

        [Fact]
        public void CloakedWindow_ShouldBeExcluded()
        {
            var policy = CreatePolicy();

            var result = policy.Evaluate(EligibleSnapshot() with { IsCloaked = true });

            result.Included.Should().BeFalse();
            result.Verdict.Should().Be(WindowEligibilityVerdict.ExcludedCloaked);
        }

        [Fact]
        public void ToolWindow_ShouldBeExcluded()
        {
            var policy = CreatePolicy();

            var result = policy.Evaluate(EligibleSnapshot() with { ExStyle = PulsarNative.WS_EX_TOOLWINDOW });

            result.Included.Should().BeFalse();
            result.Verdict.Should().Be(WindowEligibilityVerdict.ExcludedToolWindow);
        }

        [Fact]
        public void ChildWindow_ShouldBeExcluded()
        {
            var policy = CreatePolicy();

            var result = policy.Evaluate(EligibleSnapshot() with { Style = PulsarNative.WS_CHILD });

            result.Included.Should().BeFalse();
            result.Verdict.Should().Be(WindowEligibilityVerdict.ExcludedChild);
        }

        [Fact]
        public void OwnedNonAppWindow_ShouldBeExcluded()
        {
            var policy = CreatePolicy();

            var result = policy.Evaluate(EligibleSnapshot() with { OwnerHwnd = new IntPtr(7) });

            result.Included.Should().BeFalse();
            result.Verdict.Should().Be(WindowEligibilityVerdict.ExcludedOwned);
        }

        [Fact]
        public void OwnedWindowWithAppWindowStyle_ShouldBeEligible()
        {
            var policy = CreatePolicy();

            var result = policy.Evaluate(EligibleSnapshot() with
            {
                OwnerHwnd = new IntPtr(7),
                ExStyle = PulsarNative.WS_EX_APPWINDOW
            });

            result.Included.Should().BeTrue();
        }

        [Fact]
        public void SelfWindow_ShouldBeExcluded()
        {
            var policy = CreatePolicy(ownPid: 123);

            var result = policy.Evaluate(EligibleSnapshot(pid: 123));

            result.Included.Should().BeFalse();
            result.Verdict.Should().Be(WindowEligibilityVerdict.ExcludedSelf);
        }

        [Fact]
        public void ZeroSizeWindow_ShouldBeExcludedAsOffScreen()
        {
            var policy = CreatePolicy();
            var snapshot = EligibleSnapshot(rect: new PulsarNative.RECT { Left = 0, Top = 0, Right = 0, Bottom = 0 });

            var result = policy.Evaluate(snapshot);

            result.Included.Should().BeFalse();
            result.Verdict.Should().Be(WindowEligibilityVerdict.ExcludedOffScreen);
        }

        [Fact]
        public void OffScreenWindow_ShouldBeExcludedAsOffScreen()
        {
            var policy = CreatePolicy();
            // 完全位于虚拟屏幕右下（主屏 1920x1080 之外的副屏位置）。
            var snapshot = EligibleSnapshot(rect: new PulsarNative.RECT { Left = 2000, Top = 1200, Right = 2400, Bottom = 1500 });

            var result = policy.Evaluate(snapshot);

            result.Included.Should().BeFalse();
            result.Verdict.Should().Be(WindowEligibilityVerdict.ExcludedOffScreen);
        }

        [Fact]
        public void PartiallyOnScreenWindow_ShouldBeEligible()
        {
            var policy = CreatePolicy();
            var snapshot = EligibleSnapshot(rect: new PulsarNative.RECT { Left = -200, Top = -200, Right = 400, Bottom = 400 });

            var result = policy.Evaluate(snapshot);

            result.Included.Should().BeTrue();
        }

        [Fact]
        public void MinimizedWindow_ShouldBeAllowedEvenWhenOffScreen()
        {
            var policy = CreatePolicy();
            var snapshot = EligibleSnapshot(rect: new PulsarNative.RECT { Left = 2000, Top = 1200, Right = 2400, Bottom = 1500 }) with
            {
                IsIconic = true
            };

            var result = policy.Evaluate(snapshot);

            result.Included.Should().BeTrue();
        }

        [Fact]
        public void MissingRect_ShouldBeExcludedAsOffScreen()
        {
            var policy = CreatePolicy();

            var result = policy.Evaluate(EligibleSnapshot() with { Rect = null });

            result.Included.Should().BeFalse();
            result.Verdict.Should().Be(WindowEligibilityVerdict.ExcludedOffScreen);
        }

        [Fact]
        public void BlacklistedClass_ShouldBeExcluded()
        {
            var policy = CreatePolicy(classBlacklist: new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "KxWppQuickHelpBarContainer"
            });

            var result = policy.Evaluate(EligibleSnapshot(className: "kxwppquickhelpbarcontainer"));

            result.Included.Should().BeFalse();
            result.Verdict.Should().Be(WindowEligibilityVerdict.ExcludedBlacklistedClass);
        }

        [Fact]
        public void BlacklistedProcess_ShouldBeExcludedWhenPredicateMatches()
        {
            var policy = CreatePolicy();

            var result = policy.Evaluate(EligibleSnapshot(processName: "wps"), name => name == "wps");

            result.Included.Should().BeFalse();
            result.Verdict.Should().Be(WindowEligibilityVerdict.ExcludedBlacklistedProcess);
        }

        [Fact]
        public void ProcessBlacklistNull_ShouldNotExcludeByProcess()
        {
            var policy = CreatePolicy();

            var result = policy.Evaluate(EligibleSnapshot(processName: "wps"));

            result.Included.Should().BeTrue();
        }

        [Fact]
        public void ProcessBlacklistPredicateFalse_ShouldNotExclude()
        {
            var policy = CreatePolicy();

            var result = policy.Evaluate(EligibleSnapshot(processName: "wps"), _ => false);

            result.Included.Should().BeTrue();
        }

        [Fact]
        public void ExcludedOffScreen_ShouldWinOverProcessBlacklist_ForOffScreenWindow()
        {
            var policy = CreatePolicy();
            var snapshot = EligibleSnapshot(
                processName: "wps",
                rect: new PulsarNative.RECT { Left = 2000, Top = 1200, Right = 2400, Bottom = 1500 });

            var result = policy.Evaluate(snapshot, name => name == "wps");

            result.Verdict.Should().Be(WindowEligibilityVerdict.ExcludedOffScreen);
        }

        [Theory]
        [InlineData(100, 100, 800, 600, true)]
        [InlineData(-200, -200, 400, 400, true)]
        [InlineData(1920, 0, 2200, 400, false)]   // 完全在右侧屏幕外
        [InlineData(0, 1080, 800, 1500, false)]   // 完全在下方屏幕外
        [InlineData(1919, 0, 1920, 400, true)]    // 仅 1px 重叠（贴右缘）
        public void Intersects_ShouldRequirePositiveOverlap(int l, int t, int r, int b, bool expected)
        {
            var window = new PulsarNative.RECT { Left = l, Top = t, Right = r, Bottom = b };

            WindowEligibilityPolicy.Intersects(window, VirtualScreen).Should().Be(expected);
        }

        // ==================== 用户规则（候选 2） ====================

        [Fact]
        public void ExcludeRule_ByClassName_ShouldExclude()
        {
            var policy = CreatePolicy();
            policy.UpdateRules(new[] { new WindowEligibilityRule(false, null, "GhostClass", null) });

            var result = policy.Evaluate(EligibleSnapshot(className: "GhostClass"));

            result.Included.Should().BeFalse();
            result.Verdict.Should().Be(WindowEligibilityVerdict.ExcludedByRule);
        }

        [Fact]
        public void ExcludeRule_ByTitle_ShouldExclude_WhenSnapshotHasTitle()
        {
            var policy = CreatePolicy();
            policy.UpdateRules(new[] { new WindowEligibilityRule(false, null, null, "^Chrome Legacy Window$") });

            var result = policy.Evaluate(EligibleSnapshot() with { Title = "Chrome Legacy Window" });

            result.Included.Should().BeFalse();
            result.Verdict.Should().Be(WindowEligibilityVerdict.ExcludedByRule);
        }

        [Fact]
        public void TitleRule_ShouldNotMatch_WhenSnapshotHasNoTitle()
        {
            var policy = CreatePolicy();
            policy.UpdateRules(new[] { new WindowEligibilityRule(false, null, null, "^Chrome Legacy Window$") });

            // 快照未携带标题（热路径）→ 标题规则不命中。
            var result = policy.Evaluate(EligibleSnapshot());

            result.Included.Should().BeTrue();
        }

        [Fact]
        public void HasTitleDependentRules_ShouldBeTrue_OnlyWhenTitleRulesExist()
        {
            var policy = CreatePolicy();
            policy.UpdateRules(new[] { new WindowEligibilityRule(false, null, null, "^x$") });
            policy.HasTitleDependentRules.Should().BeTrue();

            policy.UpdateRules(new[] { new WindowEligibilityRule(false, null, "SomeClass", null) });
            policy.HasTitleDependentRules.Should().BeFalse();
        }

        [Fact]
        public void ProcessNameQualifier_ShouldNarrowClassRule()
        {
            var policy = CreatePolicy();
            policy.UpdateRules(new[] { new WindowEligibilityRule(false, "chrome", "GhostClass", null) });

            policy.Evaluate(EligibleSnapshot(className: "GhostClass", processName: "chrome")).Included.Should().BeFalse();
            policy.Evaluate(EligibleSnapshot(className: "GhostClass", processName: "notepad")).Included.Should().BeTrue();
        }

        [Fact]
        public void AllowRule_ShouldOverrideEarlierExcludeRule()
        {
            var policy = CreatePolicy();
            policy.UpdateRules(new[]
            {
                new WindowEligibilityRule(false, null, "GhostClass", null),
                new WindowEligibilityRule(true, "chrome", "GhostClass", null)
            });

            // chrome 的 GhostClass 被 Allow 放行；其他进程仍被 Exclude。
            policy.Evaluate(EligibleSnapshot(className: "GhostClass", processName: "chrome")).Included.Should().BeTrue();
            policy.Evaluate(EligibleSnapshot(className: "GhostClass", processName: "wps")).Included.Should().BeFalse();
        }

        [Fact]
        public void AllowRule_ShouldWinRegardlessOfPosition()
        {
            var policy = CreatePolicy();
            policy.UpdateRules(new[]
            {
                new WindowEligibilityRule(true, null, "A", null),
                new WindowEligibilityRule(false, null, "A", null)
            });

            var result = policy.Evaluate(EligibleSnapshot(className: "A"));

            result.Included.Should().BeTrue();
        }

        [Fact]
        public void AllowRule_ShouldNotOverrideHardPhysicalRule()
        {
            var policy = CreatePolicy();
            policy.UpdateRules(new[] { new WindowEligibilityRule(true, null, "GhostClass", null) });

            // 屏幕外窗口：物理硬规则优先，Allow 规则不能放行。
            var offScreen = EligibleSnapshot(className: "GhostClass", rect: new PulsarNative.RECT { Left = 2000, Top = 1200, Right = 2400, Bottom = 1500 });

            var result = policy.Evaluate(offScreen);

            result.Included.Should().BeFalse();
            result.Verdict.Should().Be(WindowEligibilityVerdict.ExcludedOffScreen);
        }

        [Fact]
        public void ExcludeRule_AnyMatchingRule_ShouldExclude()
        {
            var policy = CreatePolicy();
            policy.UpdateRules(new[]
            {
                new WindowEligibilityRule(false, "chrome", "A", null),
                new WindowEligibilityRule(false, null, "B", null)
            });

            // 进程限定不匹配第一条，但类名命中第二条 → 仍被排除。
            policy.Evaluate(EligibleSnapshot(className: "B", processName: "chrome")).Included.Should().BeFalse();
        }

        [Fact]
        public void UpdateRules_ShouldDropNonIdentityRules()
        {
            var policy = CreatePolicy();
            policy.UpdateRules(new[] { new WindowEligibilityRule(false, "notepad", null, null) });

            policy.Rules.Should().BeEmpty();
        }

        [Fact]
        public void Evaluate_WithoutRules_ShouldNotApplyRuleChain()
        {
            var policy = CreatePolicy();

            policy.Evaluate(EligibleSnapshot()).Included.Should().BeTrue();
        }

        // ===== 两段式判定：结构筛（无需进程名）+ 身份筛（需要进程名） =====
        // 全桌面枚举先做结构筛、只对幸存窗口解析进程元数据，因此结构筛必须在
        // ProcessName 为空时也能给出与合并判定一致的结论。

        [Fact]
        public void EvaluateStructural_ShouldNotRequireProcessName()
        {
            var policy = CreatePolicy();

            var result = policy.EvaluateStructural(EligibleSnapshot() with { ProcessName = string.Empty });

            result.Included.Should().BeTrue();
            result.Verdict.Should().Be(WindowEligibilityVerdict.Eligible);
        }

        [Fact]
        public void EvaluateStructural_ShouldCatchHardRules_WithoutProcessName()
        {
            var policy = CreatePolicy(ownPid: 999);
            var s = EligibleSnapshot() with { ProcessName = string.Empty };

            var cases = new (WindowEligibilitySnapshot Snapshot, WindowEligibilityVerdict Expected)[]
            {
                (s with { IsVisible = false }, WindowEligibilityVerdict.ExcludedHidden),
                (s with { IsCloaked = true }, WindowEligibilityVerdict.ExcludedCloaked),
                (s with { ExStyle = PulsarNative.WS_EX_TOOLWINDOW }, WindowEligibilityVerdict.ExcludedToolWindow),
                (s with { Style = PulsarNative.WS_CHILD }, WindowEligibilityVerdict.ExcludedChild),
                (s with { OwnerHwnd = new IntPtr(7) }, WindowEligibilityVerdict.ExcludedOwned),
                (s with { Rect = null }, WindowEligibilityVerdict.ExcludedOffScreen),
                (s with { Pid = 999 }, WindowEligibilityVerdict.ExcludedSelf)
            };

            foreach (var (snapshot, expected) in cases)
            {
                var result = policy.EvaluateStructural(snapshot);

                result.Included.Should().BeFalse("{0} 应在结构筛被排除", expected);
                result.Verdict.Should().Be(expected);
            }
        }

        [Fact]
        public void EvaluateStructural_ShouldNotApplyProcessBlacklist()
        {
            var policy = CreatePolicy();
            var snapshot = EligibleSnapshot(processName: "blacklisted");

            // 结构筛不接受黑名单谓词；进程黑名单只在身份筛生效。
            policy.EvaluateStructural(snapshot).Included.Should().BeTrue();
            policy.EvaluateIdentity(snapshot, name => name == "blacklisted").Included.Should().BeFalse();
        }

        [Fact]
        public void EvaluateStructural_ShouldNotApplyRuleChain()
        {
            var policy = CreatePolicy();
            policy.UpdateRules(new[] { new WindowEligibilityRule(false, null, "NotepadClass", null) });

            // 规则链依赖进程名限定，因此整体留在身份筛；结构筛必须放行。
            policy.EvaluateStructural(EligibleSnapshot()).Included.Should().BeTrue();
            policy.EvaluateIdentity(EligibleSnapshot()).Included.Should().BeFalse();
        }

        /// <summary>
        /// 回归护栏：合并判定必须严格等于"结构筛 → 身份筛"的两段组合。
        /// 枚举路径走两段、快速切换/Inspector 走合并，两者结论不得分叉。
        /// </summary>
        [Fact]
        public void Evaluate_ShouldEqual_StructuralThenIdentity_ForAllVariants()
        {
            var policy = CreatePolicy(
                ownPid: 999,
                classBlacklist: new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "BlockedClass" });
            policy.UpdateRules(new[]
            {
                new WindowEligibilityRule(false, null, "RuledClass", null),
                new WindowEligibilityRule(true, "allowme", "AllowedClass", null)
            });

            var baseline = EligibleSnapshot();
            var variants = new[]
            {
                baseline,
                baseline with { IsVisible = false },
                baseline with { IsCloaked = true },
                baseline with { ExStyle = PulsarNative.WS_EX_TOOLWINDOW },
                baseline with { Style = PulsarNative.WS_CHILD },
                baseline with { OwnerHwnd = new IntPtr(7) },
                baseline with { OwnerHwnd = new IntPtr(7), ExStyle = PulsarNative.WS_EX_APPWINDOW },
                baseline with { Rect = null },
                baseline with { IsIconic = true, Rect = null },
                baseline with { Pid = 999 },
                baseline with { ClassName = "BlockedClass" },
                baseline with { ClassName = "RuledClass" },
                baseline with { ClassName = "AllowedClass", ProcessName = "allowme" },
                baseline with { ClassName = "RuledClass", ProcessName = "blocked" },
                baseline with { ProcessName = "blocked" },
                baseline with { ProcessName = string.Empty }
            };

            Func<string, bool> blacklist = name => name == "blocked";

            foreach (var snapshot in variants)
            {
                var combined = policy.Evaluate(snapshot, blacklist);

                var structural = policy.EvaluateStructural(snapshot);
                var twoStage = structural.Included
                    ? policy.EvaluateIdentity(snapshot, blacklist)
                    : structural;

                combined.Should().Be(
                    twoStage,
                    "合并判定与两段判定在 {0} / {1} 上必须一致",
                    snapshot.ClassName,
                    snapshot.ProcessName);
            }
        }
    }
}
