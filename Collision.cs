// Collision.cs
// Simple helpers for AABB <-> circle intersection

using Silk.NET.Maths;

namespace PacMan
{
    public static class Collision
    {
        // Test whether a circle centered at 'c' with radius 'r' intersects rectangle defined by min (x,y) and size (w,h).
        public static bool CircleIntersectsRect(Vector2D<float> c, float r, float rectX, float rectY, float rectW, float rectH)
        {
            // rectX,rectY = bottom-left in normalized UV (0..1)
            float closestX = MathF.Max(rectX, MathF.Min(c.X, rectX + rectW));
            float closestY = MathF.Max(rectY, MathF.Min(c.Y, rectY + rectH));

            float dx = c.X - closestX;
            float dy = c.Y - closestY;
            return (dx * dx + dy * dy) < (r * r);
        }
    }
}
