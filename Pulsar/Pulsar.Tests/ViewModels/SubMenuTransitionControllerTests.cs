using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using FluentAssertions;
using Moq;
using Pulsar.Core.Localization;
using Pulsar.Models;
using Pulsar.ViewModels;
using Xunit;

namespace Pulsar.Tests.ViewModels
{
    /// <summary>
    /// [R3 2026-09-09] The submenu transition choreography (extracted verbatim from
    /// MenuSession) is tested directly: the fake host records state callbacks and the
    /// real per-frame animator drives actual durations, so collapse → callbacks →
    /// bloom ordering and the Fan/Ring/Window branch matrix are pinned at unit level
    /// (the D7/D8 class of real-machine regressions).
    /// </summary>
    public class SubMenuTransitionControllerTests
    {
        private sealed class FakeDispatcher : IUiDispatcher
        {
            public readonly List<Action> Posted = new();
            public bool CheckAccess() => true;
            public void Invoke(Action action) => action();
            public Task InvokeAsync(Action action) { action(); return Task.CompletedTask; }
            public Task BeginInvoke(Action action) { Posted.Add(action); return Task.CompletedTask; }
            public void InvokeWithInputPriority(Action action) => action();
        }

        private sealed class Host
        {
            public List<SlotViewModel> Slots { get; } =
                Enumerable.Range(1, 8).Select(i => new SlotViewModel(i, 100 + i, 100, 50)).ToList();
            public List<SlotViewModel> SubMenuSlots { get; } = new();
            public SlotViewModel CenterSlot { get; } = new(0, 225, 225, 50);

            public Host()
            {
                // SlotViewModel 默认 CurrentScale/Opacity = 0（"start invisible"）；
                // 真实会话在根轮呈现后槽位为 1/1。测试 host 预置该状态。
                foreach (var slot in Slots)
                {
                    slot.ResetAnimation();
                }
                CenterSlot.ResetAnimation();
            }
            public FakeDispatcher Dispatcher { get; } = new();
            public List<Point> GlideTargets { get; } = new();
            public List<int> ActiveSlotUpdates { get; } = new();
            public List<string> CenterTexts { get; } = new();
            public int ClearStateCalls { get; private set; }
            public int ReleaseSlotsCalls { get; private set; }
            public int ResetCenterCalls { get; private set; }
            public int CoordinatorRestoreCalls { get; private set; }
            public Action? OnCoordinatorRestore { get; set; }
            public string CenterText { get; private set; } = "Pulsar";

            public SubMenuTransitionController BuildController(
                SubMenuLayoutStyle declaredStyle = SubMenuLayoutStyle.Ring)
            {
                var loc = new Mock<ILocalizationService>();
                loc.Setup(l => l["RadialMenu.Back"]).Returns("Back");

                return new SubMenuTransitionController(new SubMenuTransitionHost
                {
                    CenterSlot = () => CenterSlot,
                    Slots = () => Slots,
                    SubMenuSlots = () => SubMenuSlots,
                    CurrentCenterSize = () => 50.0,
                    Ui = Dispatcher,
                    Loc = loc.Object,
                    UpdateActiveSlot = index => ActiveSlotUpdates.Add(index),
                    SetCenterText = text => { CenterText = text; CenterTexts.Add(text); },
                    AnimateMenuCenter = (target, _, _, _) => { GlideTargets.Add(target); return Task.CompletedTask; },
                    EffectiveCascadeStyle = _ => declaredStyle,
                    SlotsPerPage = () => 8,
                    ClearSubMenuState = () => ClearStateCalls++,
                    ReleaseSubMenuSlots = () => ReleaseSlotsCalls++,
                    ResetCenterForRoot = () => ResetCenterCalls++,
                    RestoreRootMenuFromCoordinator = () =>
                    {
                        CoordinatorRestoreCalls++;
                        OnCoordinatorRestore?.Invoke();
                    }
                });
            }
        }

        // ---------- Transition lifecycle ----------

        [Fact]
        public void BeginTransition_SetsGuard_AndReturnsScope()
        {
            var controller = new Host().BuildController();

            using var scope = controller.BeginTransition();

            controller.IsTransitioning.Should().BeTrue();
            scope.Token.CanBeCanceled.Should().BeTrue();
        }

        [Fact]
        public void EndTransition_OwnScope_ClearsGuardAndCts()
        {
            var controller = new Host().BuildController();
            var scope = controller.BeginTransition();

            controller.EndTransition(scope);

            controller.IsTransitioning.Should().BeFalse();
        }

        [Fact]
        public void EndTransition_StaleScope_ClearsGuardButKeepsCurrentCts()
        {
            var controller = new Host().BuildController();
            var first = controller.BeginTransition();
            var second = controller.BeginTransition();
            first.Token.IsCancellationRequested.Should().BeTrue("a newer transition cancels the in-flight one");

            controller.EndTransition(first);

            controller.IsTransitioning.Should().BeFalse("the original finally semantics always un-guard");
            controller.CancelCurrentTransition();
            second.Token.IsCancellationRequested.Should().BeTrue(
                "a stale scope must not clear the current CTS (ReferenceEquals guard)");
        }

        [Fact]
        public void CancelCurrentTransition_CancelsInFlightToken_KeepsGuard()
        {
            var controller = new Host().BuildController();
            using var scope = controller.BeginTransition();

            controller.CancelCurrentTransition();

            scope.Token.IsCancellationRequested.Should().BeTrue();
            controller.IsTransitioning.Should().BeTrue("the release path must not un-guard mid-morph");
        }

        [Fact]
        public void HardReset_CancelsDropsAndUnguards()
        {
            var controller = new Host().BuildController();
            using var scope = controller.BeginTransition();

            controller.HardReset();

            scope.Token.IsCancellationRequested.Should().BeTrue();
            controller.IsTransitioning.Should().BeFalse();
        }

        [Fact]
        public void GetSubMenuEnterDuration_ClampedAndDistanceAdaptive()
        {
            var near = SubMenuTransitionController.GetSubMenuEnterDuration(0);
            var far = SubMenuTransitionController.GetSubMenuEnterDuration(5000);

            near.TotalMilliseconds.Should().Be(110);
            far.TotalMilliseconds.Should().Be(240);
            SubMenuTransitionController.GetSubMenuEnterDuration(600)
                .Should().BeGreaterThan(SubMenuTransitionController.GetSubMenuEnterDuration(80));
        }

        [Fact]
        public void GetSubMenuBloomDuration_FollowsEnterPlus30_Clamped()
        {
            SubMenuTransitionController.GetSubMenuBloomDuration(TimeSpan.FromMilliseconds(110))
                .TotalMilliseconds.Should().Be(150);
            SubMenuTransitionController.GetSubMenuBloomDuration(TimeSpan.FromMilliseconds(220))
                .TotalMilliseconds.Should().Be(230);
        }

        // ---------- Cascade enter ----------

        [Fact]
        public async Task EnterCascade_Fan_ChildrenBloomFromParent_RootWheelUntouched()
        {
            var host = new Host();
            host.SubMenuSlots.Add(new SlotViewModel(1, 200, 300, 50));
            var parent = host.Slots[0];
            parent.Label = "Macro";
            var descriptor = new CascadeSubMenuDescriptor(Array.Empty<SubSlotDescriptor>(), SubMenuLayoutStyle.Fan);
            var controller = host.BuildController(SubMenuLayoutStyle.Fan);

            await controller.EnterCascadeVisualsAsync(
                descriptor, parent, parent.X + 25, parent.Y + 25, CancellationToken.None);

            var child = host.SubMenuSlots[0];
            child.CurrentOpacity.Should().Be(1.0, "children bloom in");
            child.CurrentScale.Should().Be(1.0);
            // Root wheel is a frozen read-only backdrop in Fan mode.
            host.Slots.Should().OnlyContain(s => s.CurrentOpacity == 1.0 && s.CurrentScale == 1.0);
            // [ADR-024 D8] Frozen centre keeps root look — no identity, no page label.
            host.CenterText.Should().Be("Pulsar");
            host.CenterTexts.Should().BeEmpty();
            // Dismiss anchor lights as soon as the Fan is open.
            host.Dispatcher.Posted.Should().HaveCount(1);
            host.Dispatcher.Posted[0].Invoke();
            host.ActiveSlotUpdates.Should().ContainSingle().Which.Should().Be(0);
        }

        [Fact]
        public async Task EnterCascade_Ring_RootWheelFades_CentreCarriesParentIdentity()
        {
            var host = new Host();
            host.SubMenuSlots.Add(new SlotViewModel(1, 200, 300, 50));
            var parent = host.Slots[0];
            parent.Label = "Report";
            var descriptor = new CascadeSubMenuDescriptor(Array.Empty<SubSlotDescriptor>(), SubMenuLayoutStyle.Ring);
            var controller = host.BuildController(SubMenuLayoutStyle.Ring);

            await controller.EnterCascadeVisualsAsync(
                descriptor, parent, parent.X + 25, parent.Y + 25, CancellationToken.None);

            host.Slots.Where(s => s != parent)
                .Should().OnlyContain(s => s.CurrentOpacity == 0.0, "Ring replaces the root wheel via pure fade");
            host.CenterText.Should().Be("Report");
            host.CenterSlot.Label.Should().Be("Report", "[2026-09-06 user spec] the orb reads as the parent slot");
            host.CenterSlot.CurrentOpacity.Should().Be(1.0, "the centre orb blooms back with the children");
            host.CenterSlot.AnimationOffsetX.Should().Be(parent.X + 25 - 250);
            host.CenterSlot.AnimationOffsetY.Should().Be(parent.Y + 25 - 250);
        }

        [Fact]
        public async Task EnterCascade_Ring_ParentWithoutLabel_CentreTextFallsBackToBack()
        {
            var host = new Host();
            host.SubMenuSlots.Add(new SlotViewModel(1, 200, 300, 50));
            var parent = host.Slots[0];
            parent.Label = "  ";
            var descriptor = new CascadeSubMenuDescriptor(Array.Empty<SubSlotDescriptor>(), SubMenuLayoutStyle.Ring);
            var controller = host.BuildController(SubMenuLayoutStyle.Ring);

            await controller.EnterCascadeVisualsAsync(
                descriptor, parent, 250, 250, CancellationToken.None);

            host.CenterText.Should().Be("Back");
            host.CenterSlot.Label.Should().NotBe("  ", "a whitespace parent label never replaces the orb label");
        }

        // ---------- Window submenu enter ----------

        [Fact]
        public async Task EnterWindow_GlidesViewport_CollapsesOthers_BloomsChildren()
        {
            var host = new Host();
            var parent = host.Slots[0];
            parent.Label = "VS Code";
            var controller = host.BuildController();

            await controller.EnterWindowSubMenuVisualsAsync(
                parent, parent.X + 25, parent.Y + 25,
                new Point(180, 220),
                TimeSpan.FromMilliseconds(1),
                TimeSpan.FromMilliseconds(1),
                clickedSlotIndex: 1,
                CancellationToken.None);

            host.GlideTargets.Should().ContainSingle().Which.Should().Be(new Point(180, 220));
            // The legacy morph reuses the root slots as the window list: others
            // collapse (transient) and the final bloom re-lights ALL child slots.
            host.Slots.Should().OnlyContain(s => s.CurrentOpacity == 1.0 && s.CurrentScale == 1.0);
            parent.CurrentScale.Should().Be(1.0, "clicked slot target = clamp(centerSize/parentSize, 1, 1.45)");
            host.CenterText.Should().Be("VS Code");
            host.Dispatcher.Posted.Should().HaveCount(1);
        }

        [Fact]
        public async Task EnterWindow_PreselectsClickedSlot_OnlyWhenEnabledAndReal()
        {
            var host = new Host();
            var disabled = host.Slots[2];
            disabled.Type = SlotType.None;
            disabled.IsEnabled = false;
            var controller = host.BuildController();

            await controller.EnterWindowSubMenuVisualsAsync(
                host.Slots[0], 125, 125, new Point(180, 220),
                TimeSpan.FromMilliseconds(1), TimeSpan.FromMilliseconds(1),
                clickedSlotIndex: 3, CancellationToken.None);

            host.Dispatcher.Posted.Should().HaveCount(1);
            host.Dispatcher.Posted[0].Invoke();
            host.ActiveSlotUpdates.Should().ContainSingle().Which.Should().Be(-1,
                "a None/disabled slot must not be preselected");
        }

        // ---------- Cascade exit ----------

        [Fact]
        public async Task RestoreFromCascade_Ring_RetractsChildren_AndReBloomsRoot()
        {
            var host = new Host();
            var child = new SlotViewModel(1, 200, 300, 50) { CurrentOpacity = 1.0 };
            host.SubMenuSlots.Add(child);
            var descriptor = new CascadeSubMenuDescriptor(Array.Empty<SubSlotDescriptor>(), SubMenuLayoutStyle.Ring);
            var controller = host.BuildController(SubMenuLayoutStyle.Ring);

            await controller.RestoreFromCascadeAsync(
                descriptor, 140, 160, CancellationToken.None);

            child.CurrentOpacity.Should().Be(0.0, "children retract toward the parent slot");
            child.AnimationOffsetX.Should().Be(140 - 225, "retract pose points at the origin");
            host.ClearStateCalls.Should().Be(1);
            host.ReleaseSlotsCalls.Should().Be(1);
            host.ResetCenterCalls.Should().Be(1);
            host.Slots.Should().OnlyContain(s => s.CurrentOpacity == 1.0, "Ring re-blooms the root wheel");
            host.CenterSlot.CurrentOpacity.Should().Be(1.0);
        }

        [Fact]
        public async Task RestoreFromCascade_Fan_RunsCallbacks_ButNeverReBloomsRoot()
        {
            var host = new Host();
            var child = new SlotViewModel(1, 200, 300, 50) { CurrentOpacity = 1.0 };
            host.SubMenuSlots.Add(child);
            var descriptor = new CascadeSubMenuDescriptor(Array.Empty<SubSlotDescriptor>(), SubMenuLayoutStyle.Fan);
            var controller = host.BuildController(SubMenuLayoutStyle.Fan);

            await controller.RestoreFromCascadeAsync(
                descriptor, 140, 160, CancellationToken.None);

            child.CurrentOpacity.Should().Be(0.0);
            host.ClearStateCalls.Should().Be(1);
            host.ReleaseSlotsCalls.Should().Be(1);
            host.ResetCenterCalls.Should().Be(1);
            // [ADR-024 D4/D8] The main wheel never moved — nothing to bring back.
            host.CoordinatorRestoreCalls.Should().Be(0);
        }

        // ---------- Window submenu exit ----------

        [Fact]
        public async Task RestoreWindow_RunsStateCallbacksBetweenCollapseAndBloom()
        {
            var host = new Host();
            // The real coordinator restore re-populates the root slots (fresh page
            // opacities) between collapse and bloom; emulate that so the
            // capture-then-restore contract is exercised end to end.
            host.OnCoordinatorRestore = () =>
            {
                host.Slots[0].CurrentOpacity = 1.0;
                host.Slots[1].CurrentOpacity = 0.5;
                host.Slots[2].CurrentOpacity = 0.8;
            };
            var controller = host.BuildController();

            await controller.RestoreWindowSubMenuAsync(
                140, 160, new Point(250, 250), CancellationToken.None);

            host.GlideTargets.Should().Contain(new Point(250, 250));
            host.ClearStateCalls.Should().Be(1);
            host.ResetCenterCalls.Should().Be(1);
            host.CoordinatorRestoreCalls.Should().Be(1);
            host.Slots[0].CurrentOpacity.Should().Be(1.0);
            host.Slots[1].CurrentOpacity.Should().Be(0.5, "bloom restores the captured opacities");
            host.Slots[2].CurrentOpacity.Should().Be(0.8);
            host.CenterSlot.CurrentOpacity.Should().Be(1.0);
        }
    }
}
