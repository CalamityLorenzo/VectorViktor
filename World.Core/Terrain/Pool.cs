using Microsoft.Xna.Framework;
using System;

namespace World.Core
{
    // Standing water: a lake or a pond, its surface level at `Level`, filling whatever of the terrain inside
    // the circle `Radius` round `Centre` (world X, Z) lies below that. So its shore is wherever the ground
    // rises out of it, and the circle only needs to be big enough to take in the hollow it's in.
    //
    // With a `Half` it's a rectangle instead, `Half` either side of `Centre`, and `Radius` isn't used: a
    // swimming pool, dug square into the ground.
    public readonly record struct Pool(Vector2 Centre, float Radius, float Level, Vector2 Half = default)
    {
        public bool IsRectangle => Half != Vector2.Zero;

        public bool Covers(float x, float z) => IsRectangle
            ? MathF.Abs(x - Centre.X) <= Half.X && MathF.Abs(z - Centre.Y) <= Half.Y
            : Vector2.DistanceSquared(new Vector2(x, z), Centre) <= Radius * Radius;

        public static Pool Rectangle(Vector2 centre, Vector2 half, float level) => new Pool(centre, 0f, level, half);
    }
}
