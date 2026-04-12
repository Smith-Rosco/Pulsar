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
    }
}
