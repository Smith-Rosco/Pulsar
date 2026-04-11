using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace Pulsar.Services.Interfaces
{
    public enum BounceDirection
    {
        FirstPage,
        LastPage
    }

    public interface IAnimationController
    {
        Task AnimateLayoutAsync(LayoutTarget target, AnimationOptions? options = null, CancellationToken ct = default);
        Task BounceAsync(BounceDirection direction, CancellationToken ct = default);
        void UpdateMagnetism(Vector cursorPosition);
        void SetLayoutUpdateCallback(Action<LayoutTarget> callback);
        void SetBounceUpdateCallback(Action<double> callback);
        void SetMagnetismUpdateCallback(Action<Vector, IList<SlotAnimationTarget>> callback);
        void SetSlotTargets(IList<SlotAnimationTarget> targets);
        void Pause();
        void Resume();
        Task QueueAsync(Func<CancellationToken, Task> animation, CancellationToken ct = default);
        bool IsPaused { get; }
    }

    public class SlotAnimationTarget
    {
        public double CenterX { get; set; }
        public double CenterY { get; set; }
        public double DesiredOffsetX { get; set; }
        public double DesiredOffsetY { get; set; }
        public Action<double, double>? ApplyOffset { get; set; }
    }

    public readonly record struct LayoutTarget(
        double Radius,
        double CenterSize,
        double SlotSize);

    public readonly record struct AnimationOptions(
        TimeSpan Duration,
        Func<double, double>? EasingFunction = null,
        bool EnableMagnetism = true);

    public static class AnimationOptionsDefaults
    {
        public static readonly AnimationOptions Smooth = new(
            Duration: TimeSpan.FromMilliseconds(300),
            EasingFunction: EasingFunctions.EaseOutCubic,
            EnableMagnetism: true);

        public static readonly AnimationOptions SubMenuEnter = new(
            Duration: TimeSpan.FromMilliseconds(360),
            EasingFunction: EasingFunctions.EaseInOutCubic,
            EnableMagnetism: true);

        public static readonly AnimationOptions SubMenuExit = new(
            Duration: TimeSpan.FromMilliseconds(280),
            EasingFunction: EasingFunctions.EaseInOutCubic,
            EnableMagnetism: true);

        public static readonly AnimationOptions Bounce = new(
            Duration: TimeSpan.FromMilliseconds(120),
            EasingFunction: null,
            EnableMagnetism: false);
    }

    public static class EasingFunctions
    {
        public static double EaseOutCubic(double t) => 1 - Math.Pow(1 - t, 3);
        public static double EaseInOutCubic(double t) => t < 0.5 ? 4 * t * t * t : 1 - Math.Pow(-2 * t + 2, 3) / 2;
        public static double EaseOutElastic(double t)
        {
            var c4 = (2 * Math.PI) / 3;
            return t == 0 ? 0 : t == 1 ? 1 : Math.Pow(2, -10 * t) * Math.Sin((t * 10 - 0.75) * c4) + 1;
        }
    }
}
