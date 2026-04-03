using Pulsar.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace Pulsar.Services
{
    public class AnimationController : IAnimationController
    {
        private bool _isPaused;
        private DateTime _pauseTime;
        private TimeSpan _pausedDuration;
        private readonly Queue<Func<CancellationToken, Task>> _animationQueue = new();
        private bool _isQueueProcessing;
        private readonly object _queueLock = new();

        private Action<LayoutTarget>? _onLayoutUpdate;
        private Action<double>? _onBounceUpdate;
        private Action<Vector, IList<SlotAnimationTarget>>? _onMagnetismUpdate;
        private IList<SlotAnimationTarget>? _slotTargets;

        public bool IsPaused => _isPaused;

        public void SetLayoutUpdateCallback(Action<LayoutTarget> callback) => _onLayoutUpdate = callback;
        public void SetBounceUpdateCallback(Action<double> callback) => _onBounceUpdate = callback;
        public void SetMagnetismUpdateCallback(Action<Vector, IList<SlotAnimationTarget>> callback)
        {
            _onMagnetismUpdate = callback;
        }

        public void SetSlotTargets(IList<SlotAnimationTarget> targets) => _slotTargets = targets;

        public async Task AnimateLayoutAsync(LayoutTarget target, AnimationOptions? options = null, CancellationToken ct = default)
        {
            var opts = options ?? AnimationOptionsDefaults.Smooth;
            var startTime = DateTime.Now;
            var duration = opts.Duration;
            var easing = opts.EasingFunction ?? EasingFunctions.EaseOutCubic;

            LayoutTarget current = default;
            bool firstFrame = true;

            void AnimationLoop(object? sender, EventArgs e)
            {
                if (_isPaused) return;
                if (ct.IsCancellationRequested) return;

                var elapsed = DateTime.Now - startTime - _pausedDuration;
                var t = Math.Min(elapsed.TotalMilliseconds / duration.TotalMilliseconds, 1.0);
                var easedT = easing(t);

                if (firstFrame)
                {
                    firstFrame = false;
                    return;
                }

                current = new LayoutTarget(
                    Lerp(current.Radius, target.Radius, easedT),
                    Lerp(current.CenterSize, target.CenterSize, easedT),
                    Lerp(current.SlotSize, target.SlotSize, easedT));

                _onLayoutUpdate?.Invoke(current);

                if (t >= 1.0)
                {
                    _onLayoutUpdate?.Invoke(target);
                    CompositionTarget.Rendering -= AnimationLoop;
                }
            }

            CompositionTarget.Rendering += AnimationLoop;

            try
            {
                await Task.Delay(duration + TimeSpan.FromMilliseconds(100), ct);
            }
            catch (TaskCanceledException)
            {
                CompositionTarget.Rendering -= AnimationLoop;
            }
        }

        public async Task BounceAsync(BounceDirection direction, CancellationToken ct = default)
        {
            const int BounceDuration = 120;
            const double BounceScale = 0.92;
            var halfDuration = BounceDuration / 2;
            var startTime = DateTime.Now;

            void AnimationLoop(object? sender, EventArgs e)
            {
                if (_isPaused) return;
                if (ct.IsCancellationRequested) return;

                var elapsed = (DateTime.Now - startTime - _pausedDuration).TotalMilliseconds;

                if (elapsed < halfDuration)
                {
                    var progress = elapsed / halfDuration;
                    var scale = 1.0 + (BounceScale - 1.0) * progress;
                    _onBounceUpdate?.Invoke(scale);
                }
                else if (elapsed < BounceDuration)
                {
                    var progress = (elapsed - halfDuration) / halfDuration;
                    var c4 = (2 * Math.PI) / 3;
                    var easedProgress = Math.Pow(2, -10 * progress) * Math.Sin((progress * 10 - 0.75) * c4) + 1;
                    var scale = BounceScale + (1.0 - BounceScale) * easedProgress;
                    _onBounceUpdate?.Invoke(scale);
                }
                else
                {
                    _onBounceUpdate?.Invoke(1.0);
                    CompositionTarget.Rendering -= AnimationLoop;
                }
            }

            CompositionTarget.Rendering += AnimationLoop;

            try
            {
                await Task.Delay(BounceDuration + 100, ct);
            }
            catch (TaskCanceledException)
            {
                CompositionTarget.Rendering -= AnimationLoop;
            }
        }

        public void UpdateMagnetism(Vector cursorPosition)
        {
            if (_slotTargets == null || _onMagnetismUpdate == null) return;

            const double magnetRadius = 150.0;
            const double maxOffsetRatio = 0.18;

            foreach (var slot in _slotTargets)
            {
                double dx = cursorPosition.X - slot.CenterX;
                double dy = cursorPosition.Y - slot.CenterY;
                double dist = Math.Sqrt(dx * dx + dy * dy);

                if (dist < magnetRadius)
                {
                    double strength = Math.Pow(1.0 - (dist / magnetRadius), 2);
                    slot.DesiredOffsetX = dx * strength * maxOffsetRatio;
                    slot.DesiredOffsetY = dy * strength * maxOffsetRatio;
                }
                else
                {
                    slot.DesiredOffsetX = 0;
                    slot.DesiredOffsetY = 0;
                }
            }

            _onMagnetismUpdate?.Invoke(cursorPosition, _slotTargets);
        }

        public void Pause()
        {
            if (_isPaused) return;
            _isPaused = true;
            _pauseTime = DateTime.Now;
        }

        public void Resume()
        {
            if (!_isPaused) return;
            _isPaused = false;
            _pausedDuration += DateTime.Now - _pauseTime;
        }

        public async Task QueueAsync(Func<CancellationToken, Task> animation, CancellationToken ct = default)
        {
            Task? currentTask = null;

            lock (_queueLock)
            {
                if (!_isQueueProcessing)
                {
                    _isQueueProcessing = true;
                    currentTask = ProcessQueueAsync(ct);
                }
                _animationQueue.Enqueue(animation);
            }

            if (currentTask != null)
            {
                await currentTask;
            }
        }

        private async Task ProcessQueueAsync(CancellationToken ct)
        {
            while (true)
            {
                Func<CancellationToken, Task>? animation = null;

                lock (_queueLock)
                {
                    if (_animationQueue.Count > 0)
                    {
                        animation = _animationQueue.Dequeue();
                    }
                    else
                    {
                        _isQueueProcessing = false;
                        return;
                    }
                }

                if (animation != null)
                {
                    await animation.Invoke(ct);
                }
            }
        }

        private static double Lerp(double start, double end, double t) => start + (end - start) * t;
    }

    public class SlotAnimationTarget
    {
        public double CenterX { get; set; }
        public double CenterY { get; set; }
        public double DesiredOffsetX { get; set; }
        public double DesiredOffsetY { get; set; }
        public Action<double, double>? ApplyOffset { get; set; }
    }
}
