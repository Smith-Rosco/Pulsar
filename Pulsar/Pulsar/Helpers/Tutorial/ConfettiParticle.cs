using System.Windows;

namespace Pulsar.Helpers.Tutorial
{
    internal struct ConfettiParticle
    {
        public Point Position;
        public Vector Velocity;
        public int BrushIndex;
        public double Age;
        public double Lifetime;
        public double Size;

        public bool IsAlive => Age < Lifetime;
    }
}
