using Microsoft.Xna.Framework;
using System;

namespace World.Core
{
    // Standing water: a lake or a pond, its surface level at `Level`, filling whatever of the terrain inside
    // the circle `Radius` round `Centre` (world X, Z) lies below that. So its shore is wherever the ground
    // rises out of it, and the circle only needs to be big enough to take in the hollow it's in.
    //
    // With a `Half` it's a rectangle instead, `Half` either side of `Centre`, and `Radius` isn't used: a
    // swimming pool, dug square into the ground. `Turn` turns the rectangle about its centre, as a yaw does (0
    // square to the world, increasing clockwise seen from above): its Half.X then runs along (cos, sin) of it.
    //
    // `Shore` is how far above the water its shore is sand, where the drawing's own isn't enough: a beach.
    //
    // `Current`, in metres per second across the ground, is how fast the water flows, and which way: a river's
    // ford. It carries off whoever's in it (see CharacterController), more the deeper they're in.
    public readonly record struct Pool(Vector2 Centre, float Radius, float Level, Vector2 Half = default, float? Shore = null,
        float Turn = 0f, Vector2 Current = default)
    {
        public bool IsRectangle => Half != Vector2.Zero;

        public bool Covers(float x, float z)
        {
            if (!IsRectangle)
                return Vector2.DistanceSquared(new Vector2(x, z), Centre) <= Radius * Radius;
            var local = Local(new Vector2(x, z));
            return MathF.Abs(local.X) <= Half.X && MathF.Abs(local.Y) <= Half.Y;
        }

        // A point in the world, in a rectangle's own frame: from its centre, along its Half.X and Half.Y
        public Vector2 Local(Vector2 p)
        {
            var d = p - Centre;
            if (Turn == 0f)
                return d;
            var (along, across) = Axes;
            return new Vector2(Vector2.Dot(d, along), Vector2.Dot(d, across));
        }

        // Which way a rectangle's Half.X and Half.Y run, in the world
        public (Vector2 along, Vector2 across) Axes => (new Vector2(MathF.Cos(Turn), MathF.Sin(Turn)), new Vector2(-MathF.Sin(Turn), MathF.Cos(Turn)));

        public static Pool Rectangle(Vector2 centre, Vector2 half, float level, float? shore = null) => new Pool(centre, 0f, level, half, shore);
    }
}
