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
    }
}
