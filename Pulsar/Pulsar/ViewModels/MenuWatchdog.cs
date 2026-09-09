using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Pulsar.ViewModels;

/// <summary>
/// [R4 2026-09-09] Inactivity watchdog for the radial menu session — extracted
/// verbatim from <see cref="MenuSession"/> (StartMenuWatchdog / WatchdogLoopAsync).
/// Polls once per second; when the idle time since the last <see cref="Touch"/>
/// exceeds the timeout, dismisses the menu on the UI thread (with a visibility
/// re-check to close the late-dismiss race). The session keeps ownership of the
/// dismissal action; the watchdog owns only the timing loop.
/// <para>Clock is injectable so the poll loop is testable without real 60s waits.</para>
/// </summary>
internal sealed class MenuWatchdog
{
    /// <summary>Single source of truth for the 60s inactivity budget.</summary>
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(60);

    private const int PollSeconds = 1;

    private readonly TimeSpan _timeout;
    private readonly IUiDispatcher _ui;
    private readonly Func<bool> _isVisible;
    private readonly Action _dismiss;
    private readonly ILogger? _logger;
    private readonly Func<DateTime> _clock;

    private CancellationTokenSource? _cts;
    private DateTime _lastInteractionUtc;

    public MenuWatchdog(
        IUiDispatcher ui,
        Func<bool> isVisible,
        Action dismiss,
        ILogger? logger = null,
        Func<DateTime>? clock = null,
        TimeSpan? timeout = null)
    {
        _ui = ui ?? throw new ArgumentNullException(nameof(ui));
        _isVisible = isVisible ?? throw new ArgumentNullException(nameof(isVisible));
        _dismiss = dismiss ?? throw new ArgumentNullException(nameof(dismiss));
        _logger = logger;
        _clock = clock ?? (() => DateTime.UtcNow);
        _timeout = timeout ?? DefaultTimeout;
        _lastInteractionUtc = _clock();
    }

    /// <summary>Marks a menu interaction; the pending dismissal is pushed out.</summary>
    public void Touch() => _lastInteractionUtc = _clock();

    /// <summary>
    /// (Re)starts the watchdog loop. Safe to call repeatedly: the previous
    /// loop, if any, is cancelled first. Does not reset the heartbeat — the
    /// session touches explicitly at summon time, preserving the original
    /// field-write ordering.
    /// </summary>
    public void Start()
    {
        Cancel();
        var cts = new CancellationTokenSource();
        _cts = cts;
        _ = LoopAsync(cts);
    }

    /// <summary>Cancels the loop and releases the CTS (called when the menu hides).</summary>
    public void Cancel()
    {
        _cts?.Cancel();
        _cts = null;
    }

    private async Task LoopAsync(CancellationTokenSource cts)
    {
        while (!cts.IsCancellationRequested)
        {
            var idleDuration = _clock() - _lastInteractionUtc;
            var remaining = _timeout - idleDuration;
            if (remaining <= TimeSpan.Zero)
            {
                await _ui.InvokeAsync(() =>
                {
                    if (!_isVisible())
                    {
                        return;
                    }

                    _logger?.LogWarning(
                        "[MenuSession] Watchdog dismissed the menu after {TimeoutMs}ms of inactivity",
                        _timeout.TotalMilliseconds);
                    _dismiss();
                });

                return;
            }

            var wait = remaining < TimeSpan.FromSeconds(PollSeconds)
                ? remaining
                : TimeSpan.FromSeconds(PollSeconds);

            try
            {
                await Task.Delay(wait, cts.Token);
            }
            catch (TaskCanceledException)
            {
                return;
            }
        }
    }
}
