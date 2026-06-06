using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
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
                return new FocusCaptureResult { Success = false };
            }

            uint threadId = _native.GetWindowThreadProcessId(hWnd, out uint pid);
            if (pid == _ownProcessId)
            {
                _logger.LogDebug("[FocusManager] Foreground window is Pulsar's own window, skipping capture");
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

            _logger.LogDebug("[FocusManager] Captured foreground window: HWnd=0x{hWnd:X}, PID={pid}, Name={name}",
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
                    _logger.LogDebug("[FocusManager] Release: NoRestore mode, skipping");
                    break;

                case FocusRestoreMode.RestorePrevious:
                    {
                        IntPtr prev = IntPtr.Zero;
                        lock (_stateLock)
                        {
                            prev = _capturedSnapshot?.WindowHandle ?? IntPtr.Zero;
                        }

                        if (prev != IntPtr.Zero && _native.IsWindow(prev))
                        {
                            _logger.LogDebug("[FocusManager] Release: restoring to captured window 0x{hWnd:X}", prev.ToInt64());
                            var result = await ActivateWindowAsync(prev, new FocusActivationOptions { FlashAfterActivation = false });
                            releasedTo = result.Success ? prev : IntPtr.Zero;
                        }
                        else
                        {
                            _logger.LogWarning("[FocusManager] Release: captured window is invalid or null");
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
                return new FocusActivationResult
                {
                    Success = false,
                    FailureReason = FocusActivationFailureReason.InvalidHandle,
                    TargetHandle = hWnd
                };
            }

            if (_native.IsIconic(hWnd))
            {
                _native.ShowWindow(hWnd, PulsarNative.SW_RESTORE);
            }

            bool activated;
            uint targetThread = _native.GetWindowThreadProcessId(hWnd, out uint targetPid);
            var currentThread = _native.GetCurrentThreadId();

            _tracker?.OnSyntheticEventBegin();

            try
            {
                bool attached = _native.AttachThreadInput(currentThread, targetThread, true);

                if (attached)
                {
                    try
                    {
                        _native.AllowSetForegroundWindow((int)targetPid);
                        activated = _native.SetForegroundWindowNative(hWnd);

                        if (!activated)
                        {
                            _native.BringWindowToTop(hWnd);
                            activated = _native.SetForegroundWindowNative(hWnd);
                        }
                    }
                    finally
                    {
                        _native.AttachThreadInput(currentThread, targetThread, false);
                    }
                }
                else
                {
                    _logger.LogDebug("[FocusManager] AttachThreadInput failed, using fallback path");
                    activated = FallbackActivate(hWnd);
                }
            }
            finally
            {
                _tracker?.OnSyntheticEventEnd();
            }

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
                _native.GetWindowThreadProcessId(hWnd, out uint pid);
                _native.AllowSetForegroundWindow((int)pid);
                return _native.SetForegroundWindowNative(hWnd);
            }
            finally
            {
                _native.UnlockForegroundTimeout();
            }
        }

        private async Task<bool> VerifyActivationAsync(IntPtr hWnd, FocusActivationOptions options)
        {
            for (int i = 0; i <= options.MaxRetries; i++)
            {
                if (i > 0)
                {
                    await Task.Delay(options.VerifyDelayMs);
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
