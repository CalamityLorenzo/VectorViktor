using Microsoft.Xna.Framework;
using System;

namespace World.Core.Characters
{
    // The character's camera drone: a real thing in the world, flying after its owner rather than bolted
    // to it. It heads for a point FollowDistance behind and FollowHeight above them on a critically
    // damped spring (it closes in as fast as it can without overshooting), so it lags when they run and
    // swings round behind when they turn. It never flies lower than MinClearance over the ground, lifting
    // over a ridge rather than going through it, and it keeps them in sight: if the ground (or a box) lies
    // between its station and their head - they've gone over a cliff's edge, say - it comes in closer,
    // to where it can see them. It turns lazily to keep facing them.
    public sealed class Drone
    {
        public const float FollowDistance = 3f;
        public const float FollowHeight = 2f;
        public const float MinClearance = 1f;
        public const float Stiffness = 4f;       // the spring's natural frequency, radians per second
        public const float TurnRate = 3f;        // radians per second
        public const float SightClearance = 0.3f; // how far above the ground its line of sight to them must stay
        private const int SightSamples = 16;

        public Vector3 Position { get; private set; }
        public Vector3 Velocity { get; private set; }
        public float Yaw { get; private set; }

        public Drone(Vector3 position, float yaw = 0f)
        {
            Position = position;
            Yaw = yaw;
        }

        // Where it wants to be for an owner with feet at `feet`, facing `ownerYaw`.
        public static Vector3 Station(Vector3 feet, float ownerYaw) =>
            feet + Vector3.Up * FollowHeight - new Vector3(MathF.Sin(ownerYaw), 0f, -MathF.Cos(ownerYaw)) * FollowDistance;

        // Jumps straight to its station, at rest: for when the owner is first placed, or teleported.
        public void Reset(Vector3 feet, float ownerYaw, IGround ground)
        {
            Position = KeepClear(Station(feet, ownerYaw), ground);
            Velocity = Vector3.Zero;
            Yaw = ownerYaw;
        }

        // `lookAt` is what it keeps facing: its owner's head.
        public void Step(Vector3 feet, float ownerYaw, Vector3 lookAt, float dt, IGround ground)
        {
            // Critically damped spring, integrated semi-implicitly (velocity first), which stays stable at 60 Hz
            var station = InSight(lookAt, Station(feet, ownerYaw), ground);
            var acceleration = (station - Position) * (Stiffness * Stiffness) - Velocity * (2f * Stiffness);
            Velocity += acceleration * dt;
            var position = Position + Velocity * dt;

            var cleared = KeepClear(position, ground);
            if (cleared.Y > position.Y && Velocity.Y < 0f)
                Velocity = new Vector3(Velocity.X, 0f, Velocity.Z);   // bumped up off the ground: stop sinking into it
            Position = cleared;

            var toTarget = lookAt - Position;
            if (toTarget.X * toTarget.X + toTarget.Z * toTarget.Z > 1e-6f)
            {
                var wanted = MathF.Atan2(toTarget.X, -toTarget.Z);
                var turn = MathHelper.WrapAngle(wanted - Yaw);
                Yaw = MathHelper.WrapAngle(Yaw + MathHelper.Clamp(turn, -TurnRate * dt, TurnRate * dt));
            }
        }

        // The station, or, if something's in the way of seeing `head` from it, the furthest point towards
        // it along the line between them that's still in sight.
        public static Vector3 InSight(Vector3 head, Vector3 station, IGround ground)
        {
            for (var k = 1; k <= SightSamples; k++)
            {
                var point = Vector3.Lerp(head, station, k / (float)SightSamples);
                var below = ground.GroundBelow(point, float.MaxValue);
                if (below.HasValue && point.Y < below.Value + SightClearance)
                    return Vector3.Lerp(head, station, (k - 1) / (float)SightSamples);
            }
            return station;
        }

        private static Vector3 KeepClear(Vector3 position, IGround ground)
        {
            var below = ground.GroundBelow(position, float.MaxValue);
            if (below.HasValue && position.Y < below.Value + MinClearance)
                position.Y = below.Value + MinClearance;
            return position;
        }
    }
}
