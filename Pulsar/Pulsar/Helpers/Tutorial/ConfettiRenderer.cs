using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Pulsar.Helpers.Tutorial
{
    public class ConfettiRenderer : IDisposable
    {
        private const int ParticleCount = 50;
        private static readonly Color[] Palette = new[]
        {
            Color.FromRgb(0xFF, 0xD7, 0x00), // Gold
            Color.FromRgb(0x2E, 0xCC, 0x71), // Green
            Color.FromRgb(0x34, 0x98, 0xDB), // Blue
            Color.FromRgb(0xE9, 0x1E, 0x63), // Pink
            Color.FromRgb(0x9B, 0x59, 0xB6)  // Purple
        };

        private readonly ConfettiParticle[] _particles;
        private readonly DrawingVisual _drawingVisual;
        private readonly double _screenWidth;
        private readonly double _screenHeight;
        private readonly Random _random;

        private DateTime _startTime;
        private bool _isRunning;
        private event EventHandler? OnCompleted;

        public bool IsRunning => _isRunning;

        public ConfettiRenderer(double screenWidth, double screenHeight)
        {
            _screenWidth = screenWidth;
            _screenHeight = screenHeight;
            _random = new Random();
            _particles = new ConfettiParticle[ParticleCount];
            _drawingVisual = new DrawingVisual();
            _drawingVisual.CacheMode = new BitmapCache { RenderAtScale = 1.0 };
        }

        public DrawingVisual Visual => _drawingVisual;

        public void Start(EventHandler? onCompleted = null)
        {
            if (_isRunning) return;

            _startTime = DateTime.UtcNow;
            _isRunning = true;

            if (onCompleted != null)
                OnCompleted += onCompleted;

            for (int i = 0; i < ParticleCount; i++)
            {
                _particles[i] = CreateParticle();
            }

            CompositionTarget.Rendering += OnRendering;
        }

        public void Stop()
        {
            if (!_isRunning) return;

            _isRunning = false;
            CompositionTarget.Rendering -= OnRendering;
            OnCompleted = null;
        }

        private ConfettiParticle CreateParticle()
        {
            return new ConfettiParticle
            {
                Position = new Point(
                    _random.NextDouble() * _screenWidth,
                    -20 - _random.NextDouble() * 60),
                Velocity = new Vector(
                    (_random.NextDouble() - 0.5) * 4,
                    _random.NextDouble() * 3 + 2),
                Rotation = _random.NextDouble() * 360,
                RotationSpeed = (_random.NextDouble() - 0.5) * 8,
                Color = Palette[_random.Next(Palette.Length)],
                Opacity = 1.0,
                Lifetime = 2.0 + _random.NextDouble() * 1.0,
                Age = 0,
                Size = 4 + _random.NextDouble() * 4
            };
        }

        private void OnRendering(object? sender, EventArgs e)
        {
            if (!_isRunning) return;

            var elapsed = (DateTime.UtcNow - _startTime).TotalSeconds;
            bool anyAlive = false;

            using (var context = _drawingVisual.RenderOpen())
            {
                for (int i = 0; i < ParticleCount; i++)
                {
                    var p = _particles[i];
                    p.Age = elapsed;
                    p.Opacity = Math.Max(0, 1.0 - (p.Age / p.Lifetime));
                    p.Velocity = new Vector(p.Velocity.X, p.Velocity.Y + 0.15);
                    p.Position = new Point(p.Position.X + p.Velocity.X, p.Position.Y + p.Velocity.Y);
                    p.Rotation += p.RotationSpeed;
                    _particles[i] = p;

                    if (!p.IsAlive) continue;
                    anyAlive = true;

                    context.PushTransform(new RotateTransform(p.Rotation, p.Position.X, p.Position.Y));
                    context.DrawRectangle(
                        new SolidColorBrush(Color.FromArgb((byte)(p.Opacity * 255), p.Color.R, p.Color.G, p.Color.B)),
                        null,
                        new Rect(p.Position.X - p.Size / 2, p.Position.Y - p.Size / 2, p.Size, p.Size * 0.6));
                    context.Pop();
                }
            }

            if (!anyAlive)
            {
                Stop();
                OnCompleted?.Invoke(this, EventArgs.Empty);
            }
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
