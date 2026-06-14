using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Extensions.Logging;
using Pulsar.Core.Focus;
using Pulsar.Native;
using Pulsar.Services.Interfaces;

namespace Pulsar.Services
{
    public class FocusManager : IFocusManager, IFocusHistory
    {
        private readonly IFocusNativeAdapter _native;
        private readonly ILogger<FocusManager> _logger;
        private IModifierStateTracker? _tracker;

        private readonly object _stateLock = new();

        private const uint LSFW_LOCK = 1;
        private const uint LSFW_UNLOCK = 2;

        private FocusStateSnapshot? _capturedSnapshot;
        private FocusRestoreMode _restoreMode = FocusRestoreMode.RestorePrevious;
        private IntPtr _restoreTarget;

        private IntPtr _previousWindow;
        private IntPtr _currentWindow;

        private readonly int _ownProcessId;

        public FocusRestoreMode RestoreMode
        {
            get { lock (_stateLock) return _restoreMode; }
        }

        public FocusManager(IFocusNativeAdapter nativeAdapter, ILogger<FocusManager> logger, IModifierStateTracker? modifierStateTracker = null)
        {
            _native = nativeAdapter;
            _logger = logger;
            _tracker = modifierStateTracker;
            _ownProcessId = Environment.ProcessId;
        }

        public void RegisterModifierTracker(IModifierStateTracker tracker)
        {
            _tracker = tracker;
        }

        public void SetRestoreMode(FocusRestoreMode mode, IntPtr targetWindow = default)
        {
            lock (_stateLock)
            {
                _restoreMode = mode;
                _restoreTarget = targetWindow;
            }
        }

        public FocusCaptureResult Capture()
        {
            var hWnd = _native.GetForegroundWindow();

            if (hWnd == IntPtr.Zero)
            {
                _logger.LogInformation("[FocusManager] Capture: no foreground window");
                return new FocusCaptureResult { Success = false };
            }

            uint threadId = _native.GetWindowThreadProcessId(hWnd, out uint pid);
            if (pid == _ownProcessId)
            {
                _logger.LogInformation("[FocusManager] Capture: foreground is Pulsar's own window (pid={Pid}), skipping", pid);
                return new FocusCaptureResult { Success = false, CapturedHandle = hWnd, ProcessId = pid };
            }

            string processName = string.Empty;
            try
            {
                var proc = Process.GetProcessById((int)pid);
                processName = proc.ProcessName;
            }
            catch
            {
            }

            var snapshot = new FocusStateSnapshot
            {
                WindowHandle = hWnd,
                ProcessId = pid,
                ThreadId = threadId,
                CapturedAt = DateTime.UtcNow
            };

            lock (_stateLock)
            {
                _capturedSnapshot = snapshot;
            }

            _logger.LogInformation("[FocusManager] Capture: HWnd=0x{hWnd:X} PID={Pid} Name={Name}",
                hWnd.ToInt64(), pid, processName);

            return new FocusCaptureResult
            {
                Success = true,
                CapturedHandle = hWnd,
                ProcessId = pid,
                ProcessName = processName,
                Snapshot = snapshot
            };
        }

        public void ActivateMenu(Window window)
        {
            window.Topmost = true;
            window.Activate();
            window.Focus();
            window.IsHitTestVisible = true;
            _logger.LogDebug("[FocusManager] Menu window activated");
        }

        public async Task<FocusReleaseResult> ReleaseAsync(FocusRestoreMode? mode = null, IntPtr targetWindow = default)
        {
            FocusRestoreMode effectiveMode;
            IntPtr effectiveTarget;

            lock (_stateLock)
            {
                effectiveMode = mode ?? _restoreMode;
                effectiveTarget = targetWindow != default ? targetWindow : _restoreTarget;
            }

            IntPtr releasedTo = IntPtr.Zero;

            switch (effectiveMode)
            {
                case FocusRestoreMode.NoRestore:
                    _logger.LogInformation("[FocusManager] Release: NoRestore mode, skipping release");
                    break;

                case FocusRestoreMode.RestorePrevious:
                    {
                        IntPtr prev = IntPtr.Zero;
                        lock (_stateLock)
                        {
                            prev = _capturedSnapshot?.WindowHandle ?? IntPtr.Zero;
                        }

                        _logger.LogInformation("[FocusManager] Release: RestorePrevious - captured=0x{Captured:X} ownPid={OwnPid}",
                            prev.ToInt64(), _ownProcessId);

                        if (prev != IntPtr.Zero && _native.IsWindow(prev))
                        {
                            var currentFg = _native.GetForegroundWindow();
                            _native.GetWindowThreadProcessId(currentFg, out uint currentFgPid);
                            _logger.LogInformation("[FocusManager] Release: currentForeground=0x{CurrentFg:X} currentFgPid={CurrentFgPid} ownPid={OwnPid}",
                                currentFg.ToInt64(), currentFgPid, _ownProcessId);

                            if (currentFgPid == _ownProcessId)
                            {
                                _logger.LogInformation("[FocusManager] Release: foreground still Pulsar, restoring to captured 0x{Captured:X}", prev.ToInt64());
                                var result = await ActivateWindowAsync(prev, new FocusActivationOptions { FlashAfterActivation = false });
                                _logger.LogInformation("[FocusManager] Release: restore result success={Success}", result.Success);
                                releasedTo = result.Success ? prev : IntPtr.Zero;
                            }
                            else
                            {
                                _logger.LogInformation("[FocusManager] Release: foreground changed (pid={CurrentFgPid} != ownPid={OwnPid}), skipping restore",
                                    currentFgPid, _ownProcessId);
                            }
                        }
                        else
                        {
                            _logger.LogWarning("[FocusManager] Release: captured window invalid or null (prev=0x{Prev:X})", prev.ToInt64());
                        }
                        break;
                    }

                case FocusRestoreMode.RestoreTarget:
                    if (effectiveTarget != IntPtr.Zero && _native.IsWindow(effectiveTarget))
                    {
                        _logger.LogDebug("[FocusManager] Release: restoring to target window 0x{hWnd:X}", effectiveTarget.ToInt64());
                        var result = await ActivateWindowAsync(effectiveTarget, new FocusActivationOptions { FlashAfterActivation = false });
                        releasedTo = result.Success ? effectiveTarget : IntPtr.Zero;
                    }
                    else
                    {
                        _logger.LogWarning("[FocusManager] Release: target invalid, falling back to previous");
                        goto case FocusRestoreMode.RestorePrevious;
                    }
                    break;
            }

            lock (_stateLock)
            {
                _restoreMode = FocusRestoreMode.RestorePrevious;
                _restoreTarget = IntPtr.Zero;
            }

            return new FocusReleaseResult
            {
                Success = releasedTo != IntPtr.Zero,
                ReleasedToHandle = releasedTo
            };
        }

        public async Task<FocusActivationResult> ActivateWindowAsync(IntPtr hWnd, FocusActivationOptions? options = null)
        {
            options ??= new FocusActivationOptions();

            if (hWnd == IntPtr.Zero || !_native.IsWindow(hWnd))
            {
                _logger.LogWarning("[FocusManager] ActivateWindow: invalid handle 0x{hWnd:X}", hWnd.ToInt64());
                return new FocusActivationResult
                {
                    Success = false,
                    FailureReason = FocusActivationFailureReason.InvalidHandle,
                    TargetHandle = hWnd
                };
            }

            if (_native.IsIconic(hWnd))
            {
                _logger.LogInformation("[FocusManager] ActivateWindow: restoring minimized window 0x{hWnd:X}", hWnd.ToInt64());
                _native.ShowWindow(hWnd, PulsarNative.SW_RESTORE);
            }

            bool activated;
            uint targetThread = _native.GetWindowThreadProcessId(hWnd, out uint targetPid);
            var currentThread = _native.GetCurrentThreadId();

            _logger.LogInformation("[FocusManager] ActivateWindow: hWnd=0x{hWnd:X} targetPid={TargetPid} targetThread={TargetThread} currentThread={CurrentThread} ownPid={OwnPid}",
                hWnd.ToInt64(), targetPid, targetThread, currentThread, _ownProcessId);

            _tracker?.OnSyntheticEventBegin();

            try
            {
                uint sendInputResult = _native.SendInputMouse();
                _logger.LogInformation("[FocusManager] ActivateWindow: SendInputMouse result={SendInputResult}", sendInputResult);

                activated = _native.SetForegroundWindowNative(hWnd);
                _logger.LogInformation("[FocusManager] ActivateWindow: SetForegroundWindow (simple) result={Activated}", activated);

                if (!activated)
                {
                    bool attached = _native.AttachThreadInput(currentThread, targetThread, true);
                    _logger.LogInformation("[FocusManager] ActivateWindow: AttachThreadInput result={Attached}", attached);

                    if (attached)
                    {
                        try
                        {
                            _native.LockSetForegroundWindow(LSFW_UNLOCK);
                            try
                            {
                                _native.AllowSetForegroundWindow((int)targetPid);
                                _native.SendInputMouse();
                                activated = _native.SetForegroundWindowNative(hWnd);
                                _logger.LogInformation("[FocusManager] ActivateWindow: SetForegroundWindow (primary) result={Activated}", activated);

                                if (!activated)
                                {
                                    _native.SwitchToThisWindow(hWnd, true);
                                    _logger.LogInformation("[FocusManager] ActivateWindow: SwitchToThisWindow called (first fallback)");

                                    var fgAfterSwitch = _native.GetForegroundWindow();
                                    if (fgAfterSwitch == hWnd)
                                    {
                                        activated = true;
                                        _logger.LogInformation("[FocusManager] ActivateWindow: SwitchToThisWindow succeeded, foreground verified");
                                    }
                                    else
                                    {
                                        _logger.LogInformation("[FocusManager] ActivateWindow: SwitchToThisWindow failed, foreground=0x{ActualFg:X} != target=0x{Target:X}",
                                            fgAfterSwitch.ToInt64(), hWnd.ToInt64());

                                        _native.BringWindowToTop(hWnd);
                                        _native.SendInputMouse();
                                        activated = _native.SetForegroundWindowNative(hWnd);
                                        _logger.LogInformation("[FocusManager] ActivateWindow: SetForegroundWindow (retry with BringWindowToTop) result={Activated}", activated);
                                    }
                                }
                            }
                            finally
                            {
                                _native.LockSetForegroundWindow(LSFW_LOCK);
                            }

                            if (!activated)
                            {
                                activated = ForceActivate(hWnd);
                            }
                        }
                        finally
                        {
                            _native.AttachThreadInput(currentThread, targetThread, false);
                        }
                    }
                    else
                    {
                        _logger.LogInformation("[FocusManager] ActivateWindow: AttachThreadInput failed, using fallback path");
                        activated = FallbackActivate(hWnd);
                    }
                }
            }
            finally
            {
                _tracker?.OnSyntheticEventEnd();
            }

            var actualFgAfter = _native.GetForegroundWindow();
            _logger.LogInformation("[FocusManager] ActivateWindow: final result activated={Activated} actualForeground=0x{ActualFg:X} target=0x{Target:X}",
                activated, actualFgAfter.ToInt64(), hWnd.ToInt64());

            if (activated && options.FlashAfterActivation)
            {
                var flashInfo = new PulsarNative.FLASHWINFO
                {
                    cbSize = (uint)Marshal.SizeOf<PulsarNative.FLASHWINFO>(),
                    hwnd = hWnd,
                    dwFlags = PulsarNative.FLASHW_CAPTION | PulsarNative.FLASHW_TRAY,
                    uCount = 3,
                    dwTimeout = 0
                };
                _native.FlashWindowEx(ref flashInfo);
            }

            bool verificationPassed = true;
            IntPtr actualForeground = IntPtr.Zero;

            if (activated && options.VerifyAfterActivation)
            {
                verificationPassed = await VerifyActivationAsync(hWnd, options);
                actualForeground = _native.GetForegroundWindow();
            }

            return new FocusActivationResult
            {
                Success = activated && verificationPassed,
                FailureReason = !activated
                    ? FocusActivationFailureReason.ForegroundSwitchFailed
                    : !verificationPassed
                        ? FocusActivationFailureReason.VerificationFailed
                        : FocusActivationFailureReason.None,
                TargetHandle = hWnd,
                VerificationPassed = verificationPassed,
                ActualForegroundAfterActivation = actualForeground
            };
        }

        public async Task<QuickSwitchResult> QuickSwitchAsync()
        {
            var prev = ((IFocusHistory)this).GetPreviousWindow();
            if (prev == IntPtr.Zero || !_native.IsWindow(prev))
            {
                _logger.LogWarning("[FocusManager] QuickSwitch: no valid previous window");
                return new QuickSwitchResult { Success = false, NoValidHistory = true };
            }

            var result = await ActivateWindowAsync(prev);
            if (result.Success)
            {
                SetRestoreMode(FocusRestoreMode.NoRestore);
            }

            return new QuickSwitchResult
            {
                Success = result.Success,
                SwitchedToHandle = prev,
                NoValidHistory = false
            };
        }

        public FocusStateSnapshot? Snapshot()
        {
            lock (_stateLock)
            {
                return _capturedSnapshot;
            }
        }

        private bool FallbackActivate(IntPtr hWnd)
        {
            _native.LockForegroundTimeout();
            try
            {
                _native.LockSetForegroundWindow(LSFW_UNLOCK);
                try
                {
                    _native.GetWindowThreadProcessId(hWnd, out uint pid);
                    _native.AllowSetForegroundWindow((int)pid);

                    _native.SendInputMouse();
                    bool result = _native.SetForegroundWindowNative(hWnd);
                    _logger.LogInformation("[FocusManager] FallbackActivate: SetForegroundWindow result={Result} hWnd=0x{hWnd:X} pid={Pid}",
                        result, hWnd.ToInt64(), pid);
            if (!result)
            {
                _native.SwitchToThisWindow(hWnd, true);
                _logger.LogInformation("[FocusManager] FallbackActivate: SwitchToThisWindow called (first fallback)");

                var fgAfterSwitch = _native.GetForegroundWindow();
                if (fgAfterSwitch == hWnd)
                {
                    result = true;
                    _logger.LogInformation("[FocusManager] FallbackActivate: SwitchToThisWindow succeeded, foreground verified");
                }
                else
                {
                    _logger.LogInformation("[FocusManager] FallbackActivate: SwitchToThisWindow failed, foreground=0x{ActualFg:X} != target=0x{Target:X}",
                        fgAfterSwitch.ToInt64(), hWnd.ToInt64());

                    _native.BringWindowToTop(hWnd);
                    _native.SendInputMouse();
                    result = _native.SetForegroundWindowNative(hWnd);
                    _logger.LogInformation("[FocusManager] FallbackActivate: SetForegroundWindow (retry) result={Result}", result);
                }
            }
                    if (!result)
                    {
                        result = ForceActivate(hWnd);
                    }
                    return result;
                }
                finally
                {
                    _native.LockSetForegroundWindow(LSFW_LOCK);
                }
            }
            finally
            {
                _native.UnlockForegroundTimeout();
            }
        }

        /// <summary>
        /// Last-resort activation for stubborn windows (e.g. custom-skinned or self-locking apps).
        /// Uses AllowSetForegroundWindow(ASFW_ANY) to bypass foreground lock, then retries SetForegroundWindow.
        /// </summary>
        private bool ForceActivate(IntPtr hWnd)
        {
            _logger.LogInformation("[FocusManager] ForceActivate: trying aggressive approach for 0x{hWnd:X}", hWnd.ToInt64());

            _native.AllowSetForegroundWindow(-1); // ASFW_ANY

            _native.SendInputMouse();
            bool result = _native.SetForegroundWindowNative(hWnd);
            if (!result)
            {
                _native.BringWindowToTop(hWnd);
                _native.SendInputMouse();
                result = _native.SetForegroundWindowNative(hWnd);
            }

            _logger.LogInformation("[FocusManager] ForceActivate: SetForegroundWindow result={Result}", result);
            return result;
        }

        private async Task<bool> VerifyActivationAsync(IntPtr hWnd, FocusActivationOptions options, CancellationToken cancellationToken = default)
        {
            for (int i = 0; i <= options.MaxRetries; i++)
            {
                if (i > 0)
                {
                    await Task.Delay(options.VerifyDelayMs, cancellationToken);
                }

                var fg = _native.GetForegroundWindow();
                if (fg == hWnd)
                {
                    return true;
                }
            }

            _logger.LogWarning("[FocusManager] Activation verification failed for 0x{hWnd:X}", hWnd.ToInt64());
            return false;
        }

        void IFocusHistory.RecordWindow(IntPtr hWnd)
        {
            lock (_stateLock)
            {
                if (hWnd != _currentWindow)
                {
                    _previousWindow = _currentWindow;
                    _currentWindow = hWnd;
                }
            }
        }

        IntPtr IFocusHistory.GetPreviousWindow()
        {
            lock (_stateLock)
            {
                return _previousWindow;
            }
        }

        IReadOnlyList<IntPtr> IFocusHistory.SnapshotHistory()
        {
            lock (_stateLock)
            {
                return new[] { _previousWindow, _currentWindow };
            }
        }
    }
}
