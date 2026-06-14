using System;
using System.Windows;
using System.Windows.Media;

namespace Pulsar.Helpers.Tutorial
{
    public sealed class ConfettiRenderer : IDisposable
    {
        private const int MaxParticles = 25;
        private const int BatchSize = 5;
        private const int SpawnFrameCount = 8;
        private const int BatchInterval = 2;

        private static readonly SolidColorBrush[] Brushes;

        private readonly ConfettiParticle[] _particles;
        private readonly DrawingVisual _drawingVisual;
        private readonly double _screenWidth;
        private readonly Random _random;

        private int _particleCount;
        private int _frame;
        private bool _isRunning;

        static ConfettiRenderer()
        {
            Brushes =
            [
                new SolidColorBrush(Color.FromRgb(0xFF, 0xD7, 0x00)),
                new SolidColorBrush(Color.FromRgb(0x2E, 0xCC, 0x71)),
                new SolidColorBrush(Color.FromRgb(0x34, 0x98, 0xDB)),
                new SolidColorBrush(Color.FromRgb(0xE9, 0x1E, 0x63)),
                new SolidColorBrush(Color.FromRgb(0x9B, 0x59, 0xB6)),
            ];
            Array.ForEach(Brushes, b => b.Freeze());
        }

        public ConfettiRenderer(double screenWidth, double screenHeight)
        {
            _screenWidth = screenWidth;
            _random = new Random();
            _particles = new ConfettiParticle[MaxParticles];
            _drawingVisual = new DrawingVisual();
        }

        public DrawingVisual Visual => _drawingVisual;

        public void Start()
        {
            if (_isRunning) return;
            _particleCount = 0;
            _frame = 0;
            _isRunning = true;
            EmitBatch();
            CompositionTarget.Rendering += OnFrame;
        }

        public void Stop()
        {
            if (!_isRunning) return;
            _isRunning = false;
            CompositionTarget.Rendering -= OnFrame;
        }

        private void EmitBatch()
        {
            int n = Math.Min(BatchSize, MaxParticles - _particleCount);
            for (int i = 0; i < n; i++)
            {
                _particles[_particleCount++] = new ConfettiParticle
                {
                    Position = new Point(
                        _random.NextDouble() * _screenWidth,
                        -10 - _random.NextDouble() * 20),
                    Velocity = new Vector(
                        (_random.NextDouble() - 0.5) * 2.5,
                        _random.NextDouble() * 2 + 0.5),
                    BrushIndex = _random.Next(Brushes.Length),
                    Age = 0,
                    Lifetime = 1.8 + _random.NextDouble() * 1.2,
                    Size = 4 + _random.NextDouble() * 6
                };
            }
        }

        private void OnFrame(object? sender, EventArgs e)
        {
            if (!_isRunning) return;

            _frame++;
            if (_frame / BatchInterval < SpawnFrameCount &&
                _frame % BatchInterval == 0 &&
                _particleCount < MaxParticles)
            {
                EmitBatch();
            }

            bool anyAlive = false;

            using (var dc = _drawingVisual.RenderOpen())
            {
                for (int i = 0; i < _particleCount; i++)
                {
                    var p = _particles[i];
                    p.Age += 1.0 / 60.0;
                    if (p.Age >= p.Lifetime) continue;
                    anyAlive = true;

                    p.Velocity = new Vector(p.Velocity.X, p.Velocity.Y + 0.15);
                    p.Position = new Point(
                        p.Position.X + p.Velocity.X,
                        p.Position.Y + p.Velocity.Y);
                    _particles[i] = p;

                    double half = p.Size * 0.5;
                    dc.DrawRectangle(Brushes[p.BrushIndex], null,
                        new Rect(p.Position.X - half, p.Position.Y - half,
                                 p.Size, p.Size));
                }
            }

            if (!anyAlive) Stop();
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
