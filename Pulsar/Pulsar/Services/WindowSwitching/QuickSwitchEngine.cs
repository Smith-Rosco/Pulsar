using System;
using System.Collections.Generic;
using System.Linq;

namespace Pulsar.Services.WindowSwitching
{
    internal sealed class QuickSwitchResolution
    {
        public IntPtr TargetWindow { get; init; }

        public bool UsedFallbackPreviousWindow { get; init; }
    }

    internal sealed class QuickSwitchEngine
    {
        private sealed class SwitchPairSnapshot
        {
            public IntPtr SourceWindow { get; }

            public IntPtr TargetWindow { get; }

            public DateTime CreatedAt { get; }

            public SwitchPairSnapshot(IntPtr sourceWindow, IntPtr targetWindow)
            {
                SourceWindow = sourceWindow;
                TargetWindow = targetWindow;
                CreatedAt = DateTime.Now;
            }

            public bool IsExpired(int timeoutMs)
            {
                return (DateTime.Now - CreatedAt).TotalMilliseconds > timeoutMs;
            }
        }

        private readonly object _historyLock = new();
        private readonly object _switchPairLock = new();
        private readonly Stack<IntPtr> _windowHistory = new();
        private SwitchPairSnapshot? _activeSwitchPair;

        public void RecordWindowActivation(IntPtr hwnd, int maxHistorySize)
        {
            lock (_historyLock)
            {
                if (_windowHistory.Count > 0 && _windowHistory.Peek() == hwnd)
                {
                    return;
                }

                _windowHistory.Push(hwnd);
                if (_windowHistory.Count > maxHistorySize)
                {
                    IntPtr[] temp = _windowHistory.ToArray();
                    _windowHistory.Clear();
                    foreach (IntPtr handle in temp.Take(maxHistorySize).Reverse())
                    {
                        _windowHistory.Push(handle);
                    }
                }
            }
        }

        public IntPtr[] SnapshotHistory()
        {
            lock (_historyLock)
            {
                return _windowHistory.ToArray();
            }
        }

        public QuickSwitchResolution ResolveTarget(
            IntPtr currentWindow,
            IntPtr previousWindow,
            int timeoutMs,
            Func<IntPtr, bool> isValidQuickSwitchWindow,
            Func<IntPtr, bool> isWindow)
        {
            lock (_switchPairLock)
            {
                if (_activeSwitchPair != null &&
                    !_activeSwitchPair.IsExpired(timeoutMs) &&
                    isWindow(_activeSwitchPair.SourceWindow) &&
                    isWindow(_activeSwitchPair.TargetWindow) &&
                    isValidQuickSwitchWindow(_activeSwitchPair.SourceWindow) &&
                    isValidQuickSwitchWindow(_activeSwitchPair.TargetWindow))
                {
                    if (currentWindow == _activeSwitchPair.TargetWindow)
                    {
                        return new QuickSwitchResolution { TargetWindow = _activeSwitchPair.SourceWindow };
                    }

                    if (currentWindow == _activeSwitchPair.SourceWindow)
                    {
                        return new QuickSwitchResolution { TargetWindow = _activeSwitchPair.TargetWindow };
                    }

                    _activeSwitchPair = null;
                }

                IntPtr historyTarget = FindValidHistoryWindow(currentWindow, isValidQuickSwitchWindow, isWindow);
                if (historyTarget != IntPtr.Zero)
                {
                    if (currentWindow != IntPtr.Zero && currentWindow != historyTarget)
                    {
                        _activeSwitchPair = new SwitchPairSnapshot(currentWindow, historyTarget);
                    }

                    return new QuickSwitchResolution { TargetWindow = historyTarget };
                }

                if (previousWindow != IntPtr.Zero && isWindow(previousWindow) && isValidQuickSwitchWindow(previousWindow))
                {
                    if (currentWindow != IntPtr.Zero && currentWindow != previousWindow)
                    {
                        _activeSwitchPair = new SwitchPairSnapshot(currentWindow, previousWindow);
                    }

                    return new QuickSwitchResolution
                    {
                        TargetWindow = previousWindow,
                        UsedFallbackPreviousWindow = true
                    };
                }

                return new QuickSwitchResolution();
            }
        }

        private IntPtr FindValidHistoryWindow(
            IntPtr excludeWindow,
            Func<IntPtr, bool> isValidQuickSwitchWindow,
            Func<IntPtr, bool> isWindow)
        {
            lock (_historyLock)
            {
                IntPtr[] historyArray = _windowHistory.ToArray();

                foreach (IntPtr candidate in historyArray)
                {
                    if (candidate != excludeWindow && isWindow(candidate) && isValidQuickSwitchWindow(candidate))
                    {
                        return candidate;
                    }
                }

                IntPtr[] validWindows = historyArray.Where(isWindow).ToArray();
                if (validWindows.Length < historyArray.Length)
                {
                    _windowHistory.Clear();
                    foreach (IntPtr handle in validWindows.Reverse())
                    {
                        _windowHistory.Push(handle);
                    }
                }

                return IntPtr.Zero;
            }
        }
    }
}
