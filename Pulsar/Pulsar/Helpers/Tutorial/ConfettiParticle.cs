using System.Windows;
using System.Windows.Media;

namespace Pulsar.Helpers.Tutorial
{
    public struct ConfettiParticle
    {
        public Point Position { get; set; }
        public Vector Velocity { get; set; }
        public double Rotation { get; set; }
        public double RotationSpeed { get; set; }
        public Color Color { get; set; }
        public double Opacity { get; set; }
        public double Lifetime { get; set; }
        public double Age { get; set; }
        public double Size { get; set; }

        public bool IsAlive => Age < Lifetime && Opacity > 0;
    }
}
