using System;
using FluentAssertions;
using Pulsar.Services.WindowSwitching;

namespace Pulsar.Tests.Services
{
    public class QuickSwitchEngineTests
    {
        [Fact]
        public void ResolveTarget_ShouldUseReverseTarget_WhenPairIsStillActive()
        {
            var engine = new QuickSwitchEngine();
            IntPtr source = new(11);
            IntPtr target = new(22);

            engine.RecordWindowActivation(target, 10);
            var first = engine.ResolveTarget(source, IntPtr.Zero, 5000, _ => true, _ => true);
            var second = engine.ResolveTarget(first.TargetWindow, IntPtr.Zero, 5000, _ => true, _ => true);

            first.TargetWindow.Should().Be(target);
            second.TargetWindow.Should().Be(source);
        }

        [Fact]
        public void ResolveTarget_ShouldFallbackToPreviousWindow_WhenHistoryIsInvalid()
        {
            var engine = new QuickSwitchEngine();
            IntPtr current = new(11);
            IntPtr previous = new(22);

            engine.RecordWindowActivation(new IntPtr(33), 10);
            var result = engine.ResolveTarget(current, previous, 5000, h => h == previous, h => h == previous);

            result.TargetWindow.Should().Be(previous);
            result.UsedFallbackPreviousWindow.Should().BeTrue();
        }

        [Fact]
        public void ResolveTarget_ShouldExpirePair_AfterTimeout()
        {
            var engine = new QuickSwitchEngine();
            IntPtr source = new(11);
            IntPtr target = new(22);
            IntPtr fallback = new(33);

            engine.RecordWindowActivation(target, 10);
            _ = engine.ResolveTarget(source, fallback, 1, _ => true, _ => true);
            System.Threading.Thread.Sleep(20);
            engine.RecordWindowActivation(fallback, 10);

            var result = engine.ResolveTarget(target, fallback, 1, _ => true, _ => true);

            result.TargetWindow.Should().Be(fallback);
        }

        [Fact]
        public void FindValidHistoryWindow_ShouldSkipOwnedWindow_WhenNotExplicitlyRecorded()
        {
            var engine = new QuickSwitchEngine();
            IntPtr ownerWindow = new(11);
            IntPtr ownedWindow = new(22);
            IntPtr currentApp = new(33);

            engine.RecordWindowActivation(ownerWindow, 10);
            engine.RecordWindowActivation(currentApp, 10);

            var result = engine.ResolveTarget(currentApp, ownerWindow, 5000, _ => true, _ => true);

            result.TargetWindow.Should().Be(ownerWindow);
            result.UsedFallbackPreviousWindow.Should().BeFalse();
        }

        [Fact]
        public void FindValidHistoryWindow_ShouldFindOwnedWindow_WhenExplicitlyRecorded()
        {
            var engine = new QuickSwitchEngine();
            IntPtr ownerWindow = new(11);
            IntPtr ownedWindow = new(22);
            IntPtr currentApp = new(33);

            engine.RecordWindowActivation(ownerWindow, 10);
            engine.RecordWindowActivation(currentApp, 10);
            engine.RecordWindowActivation(ownedWindow, 10);

            var result = engine.ResolveTarget(currentApp, ownerWindow, 5000, _ => true, _ => true);

            result.TargetWindow.Should().Be(ownedWindow);
        }

        [Fact]
        public void ResolveTarget_ShouldSkipClosedWindow_AndFallThroughToNextInHistory()
        {
            var engine = new QuickSwitchEngine();
            IntPtr current = new(11);
            IntPtr closed = new(22);
            IntPtr next = new(33);

            // Activation order (most recent last): current, then next, then closed.
            engine.RecordWindowActivation(current, 10);
            engine.RecordWindowActivation(next, 10);
            engine.RecordWindowActivation(closed, 10);

            // closed window is dead (isWindow returns false for it)
            var result = engine.ResolveTarget(current, closed, 5000,
                _ => true,
                h => h != closed);

            result.TargetWindow.Should().Be(next);
            result.UsedFallbackPreviousWindow.Should().BeFalse();
        }

        [Fact]
        public void ResolveTarget_ShouldSkipExcludedTarget_WhenRetryingActivationFailure()
        {
            var engine = new QuickSwitchEngine();
            IntPtr current = new(11);
            IntPtr failedTarget = new(22);
            IntPtr next = new(33);

            engine.RecordWindowActivation(current, 10);
            engine.RecordWindowActivation(next, 10);
            engine.RecordWindowActivation(failedTarget, 10);

            // First resolve returns the most recent (failedTarget). Retry with it excluded
            // must fall through to the next candidate instead of returning it again.
            var retry = engine.ResolveTarget(current, IntPtr.Zero, 5000,
                _ => true,
                _ => true,
                failedTarget);

            retry.TargetWindow.Should().Be(next);
        }

        [Fact]
        public void ResolveTarget_ShouldNotReturnCurrentWindow_AsFallback()
        {
            var engine = new QuickSwitchEngine();
            IntPtr current = new(11);
            IntPtr previous = new(22);

            engine.RecordWindowActivation(previous, 10);
            engine.RecordWindowActivation(current, 10);

            // History top is current (excluded). Only other entry is previous == previousWindow
            // which is now dead, so nothing remains.
            var result = engine.ResolveTarget(current, previous, 5000,
                _ => true,
                h => h == current);

            result.TargetWindow.Should().Be(IntPtr.Zero);
        }

        [Fact]
        public void RemoveFromHistory_ShouldRemoveWindow_PreservingOrder()
        {
            var engine = new QuickSwitchEngine();
            IntPtr a = new(11);
            IntPtr b = new(22);
            IntPtr c = new(33);

            // Activation order (most recent last): a, then b, then c.
            engine.RecordWindowActivation(a, 10);
            engine.RecordWindowActivation(b, 10);
            engine.RecordWindowActivation(c, 10);

            engine.RemoveFromHistory(b);

            // 栈顶为最近激活：移除 b 后应为 [c, a]（最近在前的快照顺序）。
            engine.SnapshotHistory().Should().ContainInOrder(c, a);
            engine.SnapshotHistory().Should().NotContain(b);
        }

        [Fact]
        public void RemoveFromHistory_ShouldNoOp_WhenWindowNotPresent()
        {
            var engine = new QuickSwitchEngine();
            IntPtr a = new(11);
            engine.RecordWindowActivation(a, 10);

            engine.RemoveFromHistory(new IntPtr(99));

            engine.SnapshotHistory().Should().ContainInOrder(a);
        }

        [Fact]
        public void RemoveFromHistory_ShouldNoOp_ForZeroHandle()
        {
            var engine = new QuickSwitchEngine();
            IntPtr a = new(11);
            engine.RecordWindowActivation(a, 10);

            engine.RemoveFromHistory(IntPtr.Zero);

            engine.SnapshotHistory().Should().ContainInOrder(a);
        }

        [Fact]
        public void RemoveFromHistory_ShouldNotReintroducePhantom_OnNextResolve()
        {
            var engine = new QuickSwitchEngine();
            IntPtr current = new(11);
            IntPtr phantom = new(22);
            IntPtr next = new(33);

            engine.RecordWindowActivation(current, 10);
            engine.RecordWindowActivation(phantom, 10);
            engine.RecordWindowActivation(next, 10);

            engine.RemoveFromHistory(phantom);

            var result = engine.ResolveTarget(current, IntPtr.Zero, 5000,
                _ => true,
                h => h != current);

            result.TargetWindow.Should().Be(next);
        }

        [Fact]
        public void SetMenuSnapshot_ThenGetMenuSnapshot_ReturnsSameHandle()
        {
            var engine = new QuickSwitchEngine();
            IntPtr snapshot = new(0x1001);

            engine.SetMenuSnapshot(snapshot);

            engine.GetMenuSnapshot().Should().Be(snapshot);
        }

        [Fact]
        public void SetMenuSnapshot_DoesNotAffectHistoryStack()
        {
            var engine = new QuickSwitchEngine();
            IntPtr historyWindow = new(0x2001);

            engine.RecordWindowActivation(historyWindow, 10);
            engine.SetMenuSnapshot(new IntPtr(0x1001));

            engine.SnapshotHistory().Should().ContainInOrder(historyWindow);
        }

        [Fact]
        public void RecordWindowActivation_DoesNotAffectMenuSnapshot()
        {
            var engine = new QuickSwitchEngine();
            IntPtr snapshot = new(0x1001);

            engine.SetMenuSnapshot(snapshot);
            engine.RecordWindowActivation(new IntPtr(0x2001), 10);

            engine.GetMenuSnapshot().Should().Be(snapshot);
        }

        [Fact]
        public void MenuSnapshot_AndHistoryTop_CanDiverge()
        {
            // Two-view contract: the unfiltered menu snapshot (e.g. a non-Alt-Tab
            // credential window) and the filtered MRU history top (e.g. Chrome)
            // are independent — the dual-track behaviour quick switch depends on.
            var engine = new QuickSwitchEngine();
            IntPtr credentialWindow = new(0x3001);
            IntPtr chrome = new(0x3002);

            engine.SetMenuSnapshot(credentialWindow);
            engine.RecordWindowActivation(chrome, 10);

            engine.GetMenuSnapshot().Should().Be(credentialWindow);
            engine.SnapshotHistory().Should().ContainInOrder(chrome);
        }

        [Fact]
        public void RecordWindowActivation_ShouldTrimHistory_ToMaxSize()
        {
            var engine = new QuickSwitchEngine();

            for (int i = 1; i <= 12; i++)
            {
                engine.RecordWindowActivation(new IntPtr(0x4000 + i), 10);
            }

            var snapshot = engine.SnapshotHistory();

            snapshot.Should().HaveCount(10);
            // Snapshot order is most-recent-first (stack top = front).
            snapshot[0].Should().Be(new IntPtr(0x4000 + 12));
        }

        [Fact]
        public void ResolveTarget_ShouldFallThroughTwoClosedWindows_ToThirdInHistory()
        {
            var engine = new QuickSwitchEngine();
            IntPtr current = new(11);
            IntPtr closedA = new(22);
            IntPtr closedB = new(33);
            IntPtr valid = new(44);

            // Most recent last: current, valid, closedA, closedB.
            engine.RecordWindowActivation(current, 10);
            engine.RecordWindowActivation(valid, 10);
            engine.RecordWindowActivation(closedA, 10);
            engine.RecordWindowActivation(closedB, 10);

            var result = engine.ResolveTarget(current, IntPtr.Zero, 5000,
                _ => true,
                h => h == current || h == valid);

            result.TargetWindow.Should().Be(valid);
        }
    }
}
