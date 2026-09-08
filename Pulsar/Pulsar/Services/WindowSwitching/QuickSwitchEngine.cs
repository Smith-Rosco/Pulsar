using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Pulsar.Services.WindowSwitching
{
    public sealed class QuickSwitchResolution
    {
        public IntPtr TargetWindow { get; init; }

        public bool UsedFallbackPreviousWindow { get; init; }
    }

    /// <summary>
    /// 上一个窗口的单一权威：MRU 历史栈、"上一对窗口"记忆，以及菜单唤起时捕获的
    /// 不过滤快照（MenuPrevious）。无 P/Invoke、无锁竞争外的副作用。
    /// 经 DI 注入 WindowService；构造函数公开以便容器实例化。
    /// </summary>
    public sealed class QuickSwitchEngine
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
        private IntPtr _menuSnapshot;

        /// <summary>
        /// 记录菜单唤起时捕获的前台窗口（MenuPrevious）。刻意不做 Alt-Tab 过滤——
        /// 该快照供 PulsarContext / SkipPreviousWindow / realCurrent 使用，需要知道
        /// 用户实际所在的程序（即使是非 Alt-Tab 窗口）。切换决策由 ResolveTarget
        /// 独立校验目标合法性，因此不合法快照永远不会被选中。
        /// </summary>
        public void SetMenuSnapshot(IntPtr handle)
        {
            Interlocked.Exchange(ref _menuSnapshot, handle);
        }

        /// <summary>
        /// 读取菜单唤起时捕获的前台窗口（MenuPrevious）。
        /// </summary>
        public IntPtr GetMenuSnapshot()
        {
            return Volatile.Read(ref _menuSnapshot);
        }

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

        /// <summary>
        /// 从 MRU 历史中移除指定窗口（保持其余顺序）。用于"切到了不可见窗口"后把幽灵逐出历史，
        /// 打破"切到幽灵 → 又进历史 → 再切幽灵"的循环。
        /// </summary>
        public void RemoveFromHistory(IntPtr hwnd)
        {
            lock (_historyLock)
            {
                if (_windowHistory.Count == 0 || hwnd == IntPtr.Zero)
                {
                    return;
                }

                IntPtr[] current = _windowHistory.ToArray();
                IntPtr[] kept = current.Where(handle => handle != hwnd).ToArray();
                if (kept.Length == current.Length)
                {
                    return;
                }

                _windowHistory.Clear();
                foreach (IntPtr handle in kept.Reverse())
                {
                    _windowHistory.Push(handle);
                }
            }
        }

        public QuickSwitchResolution ResolveTarget(
            IntPtr currentWindow,
            IntPtr previousWindow,
            int timeoutMs,
            Func<IntPtr, bool> isValidQuickSwitchWindow,
            Func<IntPtr, bool> isWindow,
            IntPtr excludeTarget = default)
        {
            lock (_switchPairLock)
            {
                if (excludeTarget == IntPtr.Zero &&
                    _activeSwitchPair != null &&
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

                IntPtr historyTarget = FindValidHistoryWindow(currentWindow, excludeTarget, isValidQuickSwitchWindow, isWindow);
                if (historyTarget != IntPtr.Zero)
                {
                    if (excludeTarget == IntPtr.Zero && currentWindow != IntPtr.Zero && currentWindow != historyTarget)
                    {
                        _activeSwitchPair = new SwitchPairSnapshot(currentWindow, historyTarget);
                    }

                    return new QuickSwitchResolution { TargetWindow = historyTarget };
                }

                if (previousWindow != IntPtr.Zero &&
                    previousWindow != currentWindow &&
                    previousWindow != excludeTarget &&
                    isWindow(previousWindow) &&
                    isValidQuickSwitchWindow(previousWindow))
                {
                    if (excludeTarget == IntPtr.Zero && currentWindow != IntPtr.Zero && currentWindow != previousWindow)
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
            IntPtr excludeTarget,
            Func<IntPtr, bool> isValidQuickSwitchWindow,
            Func<IntPtr, bool> isWindow)
        {
            lock (_historyLock)
            {
                IntPtr[] historyArray = _windowHistory.ToArray();

                foreach (IntPtr candidate in historyArray)
                {
                    if (candidate != excludeWindow &&
                        candidate != excludeTarget &&
                        isWindow(candidate) &&
                        isValidQuickSwitchWindow(candidate))
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
