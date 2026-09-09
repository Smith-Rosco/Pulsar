using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Pulsar.ViewModels;
using Xunit;

namespace Pulsar.Tests.ViewModels;

/// <summary>
/// [R4 2026-09-09] Unit tests for the inactivity watchdog extracted from
/// MenuSession. Clock and dispatcher are faked; timeouts are sub-second so
/// the 1s poll loop is exercised in real time without real waits.
/// </summary>
public class MenuWatchdogTests
{
    private sealed class FakeUiDispatcher : IUiDispatcher
    {
        public int InvokeAsyncCalls { get; private set; }
        public bool CheckAccess() => true;
        public void Invoke(Action action) => action();
        public Task InvokeAsync(Action action) { InvokeAsyncCalls++; action(); return Task.CompletedTask; }
        public Task BeginInvoke(Action action) { InvokeAsyncCalls++; action(); return Task.CompletedTask; }
        public void InvokeWithInputPriority(Action action) => action();
    }

    private sealed class FakeClock
    {
        private DateTime _now = new(2026, 9, 9, 12, 0, 0, DateTimeKind.Utc);
        public DateTime UtcNow => _now;
        public void Advance(TimeSpan t) => _now += t;
    }

    private sealed class Harness
    {
        public FakeUiDispatcher Ui { get; } = new();
        public FakeClock Clock { get; } = new();
        public bool Visible { get; set; } = true;
        public int Dismissals { get; private set; }
        public MenuWatchdog Watchdog { get; }

        public Harness(TimeSpan? timeout = null)
        {
            Watchdog = new MenuWatchdog(
                Ui,
                () => Visible,
                () => Dismissals++,
                logger: null,
                clock: () => Clock.UtcNow,
                timeout: timeout);
        }

        /// <summary>Waits until the poll loop observes the fake clock (≤1s poll).</summary>
        public async Task WaitPollAsync(int attempts = 40)
        {
            for (var i = 0; i < attempts; i++)
            {
                await Task.Delay(100);
                if (Dismissals > 0) return;
            }
        }
    }

    [Fact]
    public void DefaultTimeout_Is60Seconds()
    {
        MenuWatchdog.DefaultTimeout.Should().Be(TimeSpan.FromSeconds(60));
    }

    [Fact]
    public async Task IdleBeyondTimeout_DismissesOnUiThread()
    {
        var h = new Harness(TimeSpan.FromMilliseconds(500));
        h.Watchdog.Start();
        h.Clock.Advance(TimeSpan.FromSeconds(2));
        await h.WaitPollAsync();

        h.Dismissals.Should().Be(1);
        h.Ui.InvokeAsyncCalls.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Touch_PushesDismissalOut()
    {
        var h = new Harness(TimeSpan.FromSeconds(1));
        h.Watchdog.Start();
        await Task.Delay(200);

        // Heartbeat right when the first poll would see idle ≈ 1s.
        h.Clock.Advance(TimeSpan.FromSeconds(1));
        h.Watchdog.Touch();

        await Task.Delay(300);
        h.Dismissals.Should().Be(0, "touch resets the idle window before the poll fires");

        h.Clock.Advance(TimeSpan.FromSeconds(2));
        await h.WaitPollAsync();
        h.Dismissals.Should().Be(1);
    }

    [Fact]
    public async Task Start_PreservesPriorHeartbeat()
    {
        var h = new Harness(TimeSpan.FromMilliseconds(500));
        h.Clock.Advance(TimeSpan.FromSeconds(3));
        h.Watchdog.Touch();
        h.Watchdog.Start();
        h.Clock.Advance(TimeSpan.FromSeconds(1));
        await h.WaitPollAsync();

        h.Dismissals.Should().Be(1, "Start does not reset the heartbeat (Touch happens at summon time)");
    }

    [Fact]
    public async Task Cancel_StopsDismissal()
    {
        var h = new Harness(TimeSpan.FromMilliseconds(500));
        h.Watchdog.Start();
        h.Clock.Advance(TimeSpan.FromSeconds(2));
        h.Watchdog.Cancel();
        await Task.Delay(1500);

        h.Dismissals.Should().Be(0);
    }

    [Fact]
    public async Task HiddenMenu_SkipsDismissal()
    {
        var h = new Harness(TimeSpan.FromMilliseconds(500));
        h.Visible = false;
        h.Watchdog.Start();
        h.Clock.Advance(TimeSpan.FromSeconds(2));
        await h.WaitPollAsync();

        h.Dismissals.Should().Be(0, "the visibility re-check closes the late-dismiss race");
    }

    [Fact]
    public async Task Restart_AfterDismissal_ArmsAgain()
    {
        var h = new Harness(TimeSpan.FromMilliseconds(500));
        h.Watchdog.Start();
        h.Clock.Advance(TimeSpan.FromSeconds(2));
        await h.WaitPollAsync();
        h.Dismissals.Should().Be(1);

        h.Watchdog.Start();
        h.Clock.Advance(TimeSpan.FromSeconds(2));
        await h.WaitPollAsync();
        h.Dismissals.Should().Be(2);
    }
}
