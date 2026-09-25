using Microsoft.Xna.Framework;

namespace World.Core
{
    // Standing water: a lake or a pond, its surface level at `Level`, filling whatever of the terrain inside
    // the circle `Radius` round `Centre` (world X, Z) lies below that. So its shore is wherever the ground
    // rises out of it, and the circle only needs to be big enough to take in the hollow it's in.
    public readonly record struct Pool(Vector2 Centre, float Radius, float Level)
    {
        public bool Covers(float x, float z) => Vector2.DistanceSquared(new Vector2(x, z), Centre) <= Radius * Radius;
    }
}
