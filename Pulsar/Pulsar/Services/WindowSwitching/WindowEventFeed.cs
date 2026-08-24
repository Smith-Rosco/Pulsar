using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Pulsar.Services.WindowSwitching
{
    internal enum WindowEventKind
    {
        /// <summary>EVENT_SYSTEM_FOREGROUND — 某窗口获得前台焦点。</summary>
        Foreground,

        /// <summary>EVENT_OBJECT_SHOW — 某顶层窗口变为可见。</summary>
        Shown
    }

    internal readonly record struct WindowHistoryEvent(WindowEventKind Kind, IntPtr Hwnd);

    /// <summary>
    /// 窗口事件输入管道：WinEvent 回调只做 O(1) 入队，后台消费者负责过滤、去重与记录。
    /// 把阻塞工作（GetWindowText、加锁、日志）从 OS 事件线程上移走，避免拖慢全局事件系统。
    /// </summary>
    internal sealed class WindowEventFeed
    {
        private readonly Channel<WindowHistoryEvent> _channel;
        private readonly Func<WindowHistoryEvent, bool> _filter;
        private readonly Action<WindowHistoryEvent> _onEvent;
        private readonly CancellationTokenSource _cts;
        private readonly Task _drainTask;

        public WindowEventFeed(
            Action<WindowHistoryEvent> onEvent,
            Func<WindowHistoryEvent, bool>? filter = null,
            int capacity = 512)
        {
            _onEvent = onEvent ?? throw new ArgumentNullException(nameof(onEvent));
            _filter = filter ?? (_ => true);
            _channel = Channel.CreateBounded<WindowHistoryEvent>(
                new BoundedChannelOptions(capacity)
                {
                    FullMode = BoundedChannelFullMode.DropWrite,
                    SingleReader = true
                });
            _cts = new CancellationTokenSource();
            _drainTask = Task.Run(() => DrainAsync(_cts.Token));
        }

        /// <summary>
        /// O(1) 入队，非阻塞；队列已满时丢弃（MRU 只需要最近的事件）。
        /// </summary>
        public void Enqueue(WindowHistoryEvent evt)
        {
            _channel.Writer.TryWrite(evt);
        }

        private async Task DrainAsync(CancellationToken token)
        {
            IntPtr lastHwnd = IntPtr.Zero;

            await foreach (var evt in _channel.Reader.ReadAllAsync(token))
            {
                // 连续重复的 HWND（FOREGROUND 与 SHOWN 常先后送达同一窗口）直接合并。
                if (evt.Hwnd == lastHwnd)
                {
                    continue;
                }

                lastHwnd = evt.Hwnd;

                if (!_filter(evt))
                {
                    continue;
                }

                try
                {
                    _onEvent(evt);
                }
                catch
                {
                    // 消费者异常不应终止管道。
                }
            }
        }

        public void Stop()
        {
            try
            {
                _cts.Cancel();
            }
            catch
            {
            }

            _channel.Writer.TryComplete();
        }
    }
}
