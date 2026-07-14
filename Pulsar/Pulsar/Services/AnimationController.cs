using Pulsar.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

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
        private LayoutTarget _currentLayout;
        private readonly LayoutAnimator _animator = new();

        public bool IsPaused => _isPaused;

        public AnimationController()
        {
            _animator.Changed += OnAnimatorChanged;
        }

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
            var duration = opts.Duration;

            StopAnimations();

            _animator.Radius = _currentLayout.Radius;
            _animator.CenterSize = _currentLayout.CenterSize;
            _animator.SlotSize = _currentLayout.SlotSize;

            var easing = ToWpfEasing(opts.EasingFunction);

            _animator.BeginAnimation(LayoutAnimator.RadiusProperty, new DoubleAnimation(target.Radius, duration) { EasingFunction = easing });
            _animator.BeginAnimation(LayoutAnimator.CenterSizeProperty, new DoubleAnimation(target.CenterSize, duration) { EasingFunction = easing });
            _animator.BeginAnimation(LayoutAnimator.SlotSizeProperty, new DoubleAnimation(target.SlotSize, duration) { EasingFunction = easing });

            try
            {
                await Task.Delay(duration + TimeSpan.FromMilliseconds(50), ct);
            }
            catch (TaskCanceledException)
            {
                SnapToCurrent();
                return;
            }

            _currentLayout = target;
            _onLayoutUpdate?.Invoke(target);
        }

        public async Task BounceAsync(BounceDirection direction, CancellationToken ct = default)
        {
            var bounce = new BounceAnimator();
            bounce.Bounced += s => _onBounceUpdate?.Invoke(s);
            bounce.Scale = 1.0;

            var anim = new DoubleAnimationUsingKeyFrames();
            anim.KeyFrames.Add(new EasingDoubleKeyFrame(0.92, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(60))));
            anim.KeyFrames.Add(new EasingDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(120)))
            {
                EasingFunction = new ElasticEase { EasingMode = EasingMode.EaseOut, Oscillations = 2, Springiness = 0 }
            });

            bounce.BeginAnimation(BounceAnimator.ScaleProperty, anim);

            try
            {
                await Task.Delay(TimeSpan.FromMilliseconds(170), ct);
            }
            catch (TaskCanceledException)
            {
                bounce.BeginAnimation(BounceAnimator.ScaleProperty, null);
                _onBounceUpdate?.Invoke(1.0);
                return;
            }

            bounce.BeginAnimation(BounceAnimator.ScaleProperty, null);
            _onBounceUpdate?.Invoke(1.0);
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

        private void SnapToCurrent()
        {
            var r = _animator.Radius;
            var c = _animator.CenterSize;
            var s = _animator.SlotSize;
            StopAnimations();
            _currentLayout = new LayoutTarget(r, c, s);
            _animator.Radius = r;
            _animator.CenterSize = c;
            _animator.SlotSize = s;
        }

        private void OnAnimatorChanged()
        {
            var layout = new LayoutTarget(_animator.Radius, _animator.CenterSize, _animator.SlotSize);
            _currentLayout = layout;
            _onLayoutUpdate?.Invoke(layout);
        }

        private void StopAnimations()
        {
            _animator.BeginAnimation(LayoutAnimator.RadiusProperty, null);
            _animator.BeginAnimation(LayoutAnimator.CenterSizeProperty, null);
            _animator.BeginAnimation(LayoutAnimator.SlotSizeProperty, null);
        }

        private static IEasingFunction? ToWpfEasing(Func<double, double>? easing)
        {
            if (easing == EasingFunctions.EaseOutCubic) return new CubicEase { EasingMode = EasingMode.EaseOut };
            if (easing == EasingFunctions.EaseInOutCubic) return new CubicEase { EasingMode = EasingMode.EaseInOut };
            return null;
        }

        private sealed class BounceAnimator : UIElement
        {
            internal static readonly DependencyProperty ScaleProperty =
                DependencyProperty.Register(nameof(Scale), typeof(double), typeof(BounceAnimator),
                    new PropertyMetadata(1.0, (d, _) => ((BounceAnimator)d).Bounced?.Invoke((double)d.GetValue(ScaleProperty))));

            internal double Scale { get => (double)GetValue(ScaleProperty); set => SetValue(ScaleProperty, value); }

            internal event Action<double>? Bounced;
        }

        private sealed class LayoutAnimator : UIElement
        {
            internal static readonly DependencyProperty RadiusProperty = Register(nameof(Radius));
            internal static readonly DependencyProperty CenterSizeProperty = Register(nameof(CenterSize));
            internal static readonly DependencyProperty SlotSizeProperty = Register(nameof(SlotSize));

            internal double Radius { get => (double)GetValue(RadiusProperty); set => SetValue(RadiusProperty, value); }
            internal double CenterSize { get => (double)GetValue(CenterSizeProperty); set => SetValue(CenterSizeProperty, value); }
            internal double SlotSize { get => (double)GetValue(SlotSizeProperty); set => SetValue(SlotSizeProperty, value); }

            internal event Action? Changed;

            private static DependencyProperty Register(string name) =>
                DependencyProperty.Register(name, typeof(double), typeof(LayoutAnimator),
                    new PropertyMetadata(0.0, (d, _) => ((LayoutAnimator)d).Changed?.Invoke()));
        }
    }

}
