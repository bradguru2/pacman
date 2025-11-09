
using Silk.NET.Maths;
using SixLabors.ImageSharp.Memory;

namespace PacMan
{
    public enum GhostMode { Scatter, Chase, Frightened, Dead }

    public class Ghost(Vector2D<float> pos, Vector2D<float> dir, Vector3D<float> color, String name)
    {
        public Vector2D<float> PosUV = pos;
        public Vector2D<float> Dir = dir;
        public Vector2D<float> PreviousDir = dir;
        public ValueTuple<int,int> PreviousTile = (0, 0);
        public Vector3D<float> Color = color;
        public readonly Vector2D<float> StartingPosUV = pos;
        public bool InBox = true;
        public bool Teleport = false;
        public String Name = name;
        public GhostMode Mode = GhostMode.Scatter;
    }
}