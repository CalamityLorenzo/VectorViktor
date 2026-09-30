using Microsoft.Xna.Framework;

namespace World.Buildings
{
    // Something solid and level-topped along the line from A to B (world X, Z), HalfWidth either side of it and
    // round its ends, from Bottom up to Top (world heights): a monorail's beam, say. Its top is ground to stand
    // and walk on; its sides, if it's taller than a step, block walkers as a wall does.
    public readonly record struct Ledge(Vector2 A, Vector2 B, float HalfWidth, float Bottom, float Top);
}
