using Microsoft.Xna.Framework;
using System;
using World.Core;

namespace World.Maps
{
    // A door that leads elsewhere, the way Basic.Levels' doors do: walk into the wall between A and B, your feet
    // within a step of Floor, and you're taken To, facing Yaw. It needs a wall there to walk into.
    //
    // With ToMap, it leads onto another map altogether (by its name, see Files.MapLibrary), at its start ToStart (that
    // map's DefaultStart if none): To and Yaw are left unused. An app that can't change maps leaves such a door shut.
    public readonly record struct Portal(Vector2 A, Vector2 B, float Floor, Vector3 To, float Yaw)
    {
        public const float Reach = 0.05f;   // how much further off than a walker's radius still counts as walking into it

        public string ToMap { get; init; }
        public string ToStart { get; init; }

        public bool LeadsOffMap => ToMap != null;

        public bool WalkedInto(Vector3 feet, float radius)
        {
            var q = new Vector2(feet.X, feet.Z);
            var along = B - A;
            var t = Vector2.Dot(q - A, along) / along.LengthSquared();
            return t > 0f && t < 1f && MathF.Abs(feet.Y - Floor) <= WorldConstants.MaxStepUp &&
                Vector2.Distance(q, A + along * t) <= radius + Reach;
        }
    }
}
