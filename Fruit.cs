using System.Runtime.ConstrainedExecution;
using Silk.NET.Maths;

namespace PacMan
{
    public class Fruit(Vector2D<float> position)
    {
        public Vector2D<float> PosUV = position;
        public bool Active = false;
        public double SpawnTime = 0;
        public const double Lifetime = 10.0; // seconds before disappearing
    }
}
