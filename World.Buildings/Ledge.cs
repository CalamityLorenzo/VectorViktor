using Microsoft.Xna.Framework;
using System;

namespace World.Buildings
{
    // Something solid and level-topped along the line from A to B (world X, Z), HalfWidth either side of it and
    // round its ends, from Bottom up to Top (world heights): a monorail's beam, say. Its top is ground to stand
    // and walk on; its sides, if it's taller than a step, block walkers as a wall does.
    //
    // With TopAtB, its top isn't level: it slopes evenly from Top at A to TopAtB at B (level past either end), a ramp.
    // Built from level strips instead, a ramp is a run of tiny steps, and whatever goes up it jerks up each one.
    public readonly record struct Ledge(Vector2 A, Vector2 B, float HalfWidth, float Bottom, float Top, float? TopAtB = null)
    {
        // Its top at the point of its line nearest `p`
        public float TopAt(Vector2 p)
        {
            if (TopAtB is not { } end)
                return Top;
            var along = B - A;
            var length = along.LengthSquared();
            var t = length < 1e-8f ? 0f : Math.Clamp(Vector2.Dot(p - A, along) / length, 0f, 1f);
            return Top + (end - Top) * t;
        }

        // Its highest top, anywhere along it
        public float Highest => MathF.Max(Top, TopAtB ?? Top);

        // Which way its top faces at `p`: up, unless it slopes, and then tipped back from the way it rises
        public Vector3 NormalAt(Vector2 p)
        {
            var along = B - A;
            var length = along.Length();
            if (TopAtB is not { } end || length < 1e-4f)
                return Vector3.Up;
            var t = Vector2.Dot(p - A, along) / (length * length);
            if (t < 0f || t > 1f)
                return Vector3.Up;   // past its ends, it's level
            var rise = along / length * ((end - Top) / length);   // metres up for each metre along, each way
            return Vector3.Normalize(new Vector3(-rise.X, 1f, -rise.Y));
        }
    }
}
