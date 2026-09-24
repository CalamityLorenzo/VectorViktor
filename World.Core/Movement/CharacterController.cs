using Microsoft.Xna.Framework;
using System;

namespace World.Core.Movement
{
    // A walker on an IGround: moves itself (it isn't pushed around by a physics solver), under gravity.
    // Position is its feet. Yaw 0 faces north (-Z), increasing turns right - the same as Basic.Levels.
    //
    // Each tick it moves across the ground in short sub-steps, so a runner can't skip over a thin feature
    // in one go (the octagon ladder's overshoot, the other way round), and each sub-step is checked:
    //  - ground rising more than MaxStepUp, or rising at all where it's too steep to walk, blocks it; the
    //    move then slides along the slope's contour instead, so you're stopped by a cliff, not stuck to it
    //  - ground dropping away by no more than SnapDown, where it's walkable, keeps you on it; any more and
    //    you're airborne, which is how you walk off a cliff's edge
    // Airborne, gravity pulls you down until your feet reach the ground again, and a ceiling stops you
    // going up any further. Stood on ground too steep to stand on (landed on a cliff face), you slide
    // off it downhill. Walls (see IGround.KeepOut) push you back out after every sub-step, so you slide
    // along one you walk into at an angle. On a ladder (see IGround.StepUpAt), each stride may rise, or
    // drop, further than a step.
    public sealed class CharacterController
    {
        public const float WalkSpeed = 2.5f;          // metres per second
        public const float RunMultiplier = 2.5f;
        public const float TurnSpeed = 2.0f;          // radians per second
        public const float MaxStepUp = 0.3f;          // the same as RoomSpec.DefaultMaxStepUp
        public const float SnapDown = 0.3f;
        public const float Gravity = 9.81f;
        public const float TerminalSpeed = 50f;
        public const float JumpSpeed = 4.5f;          // clears about a metre
        public const float GroundAcceleration = 30f;  // how quickly you reach the speed asked for, or stop
        public const float AirAcceleration = 4f;      // a little steering in the air, no more
        public const float SlideAcceleration = 12f;   // down a slope too steep to stand on
        public const float Radius = 0.3f;             // how far ahead a blocking slope is felt, so the eye stays clear of it
        public const float MaxSubStep = 0.1f;         // metres
        public const float Height = 1.8f;             // feet to the top of the head, for walls and ceilings

        // Against bodies (see PhysicsWorld.PushWalker): how heavy you are when one hits you, and how hard
        // and how powerfully you can push one. The force is the most you can shove with at all, so
        // anything with more grip than that (a body of about 90 kg, at PhysicsWorld.Friction) won't move;
        // the power is what caps how fast you can keep one going - about 1 m/s for 60 kg.
        public const float Mass = 75f;                // kg
        public const float PushForce = 450f;          // newtons
        public const float PushPower = 300f;          // watts
        public const float PushHeight = 1.2f;         // how far above your feet your pushes land: chest height

        public Vector3 Position { get; set; }
        public Vector3 Velocity { get; set; }
        public float Yaw { get; set; }
        public bool Grounded { get; private set; }

        // The velocity asked for on the last tick: where you're trying to go, whether or not you can.
        public Vector3 Wish { get; private set; }

        // Knocked off balance: for this long you can't brake or steer on your feet any better than in the
        // air, so a hard shove slides you along instead of stopping dead.
        public float Staggered { get; private set; }

        public void Stagger(float seconds) => Staggered = MathF.Max(Staggered, seconds);

        public Vector3 Heading => new Vector3(MathF.Sin(Yaw), 0f, -MathF.Cos(Yaw));
        public Vector3 Right => new Vector3(MathF.Cos(Yaw), 0f, MathF.Sin(Yaw));

        public CharacterController(Vector3 feet, float yaw = 0f)
        {
            Position = feet;
            Yaw = yaw;
        }

        // Puts the feet on the ground below them, at rest.
        public void SnapToGround(IGround ground)
        {
            var height = ground.GroundBelow(Position, MaxStepUp);
            if (height.HasValue)
                Position = new Vector3(Position.X, height.Value, Position.Z);
            Velocity = Vector3.Zero;
            Grounded = height.HasValue;
        }

        public void Step(in MoveInput input, float dt, IGround ground)
        {
            Yaw = MathHelper.WrapAngle(Yaw + input.Turn * TurnSpeed * dt);

            // Steer the horizontal velocity towards what's asked for: hard on the ground, gently in the air
            var wish = Right * input.Move.X + Heading * input.Move.Y;
            if (wish.LengthSquared() > 1f)
                wish.Normalize();   // so going diagonally isn't faster
            wish *= input.Run ? WalkSpeed * RunMultiplier : WalkSpeed;
            Wish = wish;

            var velocity = Velocity;
            var horizontal = new Vector3(velocity.X, 0f, velocity.Z);

            // On a cliff face: it won't hold you, and you can't brake on it either (or you'd never slide)
            var sliding = Grounded && !ground.IsWalkable(Position);
            if (sliding)
                Grounded = false;

            var footing = Grounded && Staggered <= 0f;
            horizontal = Approach(horizontal, wish, (footing ? GroundAcceleration : AirAcceleration) * dt);
            Staggered = MathF.Max(0f, Staggered - dt);
            if (sliding)
                horizontal += Downhill(ground.NormalAt(Position)) * SlideAcceleration * dt;

            if (Grounded && input.Jump)
            {
                Grounded = false;
                velocity.Y = JumpSpeed;
            }
            if (!Grounded)
                velocity.Y = MathF.Max(velocity.Y - Gravity * dt, -TerminalSpeed);
            else
                velocity.Y = 0f;

            // Across the ground, a sub-step at a time
            var position = Position;
            var travel = horizontal * dt;
            var steps = Math.Max(1, (int)MathF.Ceiling(travel.Length() / MaxSubStep));
            var stepTravel = travel / steps;
            for (var s = 0; s < steps; s++)
            {
                if (!TryMove(ground, ref position, stepTravel) && !TrySlide(ground, ref position, stepTravel, ref horizontal))
                {
                    horizontal = Vector3.Zero;   // walked straight into it: stop
                    break;
                }

                // Out of any wall this has taken you into, losing the part of the velocity that went into it
                var before = position;
                position = ground.KeepOut(position, Radius, Height);
                var pushed = new Vector3(position.X - before.X, 0f, position.Z - before.Z);
                if (pushed.LengthSquared() > 1e-12f)
                {
                    var outward = Vector3.Normalize(pushed);
                    horizontal -= outward * MathF.Min(0f, Vector3.Dot(horizontal, outward));
                }

                if (Grounded)
                {
                    var step = ground.StepUpAt(position, MaxStepUp);
                    var below = ground.GroundBelow(position, step);
                    if (below.HasValue && position.Y - below.Value <= MathF.Max(SnapDown, step) && ground.IsWalkable(position))
                        position.Y = below.Value;
                    else
                        Grounded = false;   // the ground's dropped away: off the edge
                }
            }

            // Up and down
            if (!Grounded)
            {
                // Looking for ground as far above the feet as they've just fallen past, so a fast fall
                // can't drop straight through the top of a box in one tick
                position.Y += velocity.Y * dt;
                var below = ground.GroundBelow(position, MaxStepUp + MathF.Max(0f, -velocity.Y * dt));
                if (below.HasValue && position.Y <= below.Value)
                {
                    position.Y = below.Value;
                    velocity.Y = 0f;
                    Grounded = true;
                }

                // Head against a ceiling: no higher
                var ceiling = ground.CeilingAbove(position);
                if (ceiling.HasValue && position.Y + Height > ceiling.Value)
                {
                    position.Y = MathF.Max(ceiling.Value - Height, below ?? float.MinValue);
                    velocity.Y = MathF.Min(velocity.Y, 0f);
                }
            }

            Position = position;
            Velocity = new Vector3(horizontal.X, velocity.Y, horizontal.Z);
        }

        // Whether the ground lets you move `travel` (horizontally) from `position`; if so, moves you.
        private static bool TryMove(IGround ground, ref Vector3 position, Vector3 travel)
        {
            if (travel.LengthSquared() < 1e-12f)
                return true;

            // Where the feet go: no higher than a step (more, up a ladder), and not up a slope too steep to walk
            var target = position + travel;
            var step = ground.StepUpAt(target, MaxStepUp);
            var below = ground.GroundBelow(target, step);
            if (!below.HasValue)
                return false;   // the edge of the world
            var rise = below.Value - position.Y;
            if (rise > step || (rise > 1e-4f && !ground.IsWalkable(target)))
                return false;

            // A body's width further on, only whether it's a cliff rising in front of you - so you're stopped
            // with your face short of it, not in it. (It's further away, so it's allowed to be more than a step up.)
            var probe = target + Vector3.Normalize(travel) * Radius;
            var ahead = ground.GroundBelow(probe, step + Radius);
            if (!ahead.HasValue || (ahead.Value - position.Y > 1e-4f && !ground.IsWalkable(probe)))
                return false;

            position = target;
            return true;
        }

        // Blocked: try going along the slope that stopped you instead of into it, then along each axis.
        private static bool TrySlide(IGround ground, ref Vector3 position, Vector3 travel, ref Vector3 horizontal)
        {
            var target = position + travel;
            var into = Downhill(ground.NormalAt(target + Vector3.Normalize(travel) * Radius));
            var candidates = new[]
            {
                into == Vector3.Zero ? Vector3.Zero : travel - into * MathF.Min(0f, Vector3.Dot(travel, into)),
                new Vector3(travel.X, 0f, 0f),
                new Vector3(0f, 0f, travel.Z),
            };
            foreach (var slide in candidates)
            {
                if (slide.LengthSquared() < 1e-10f || !TryMove(ground, ref position, slide))
                    continue;
                // Keep only the part of the velocity that went somewhere
                var along = Vector3.Normalize(slide);
                horizontal = along * MathF.Max(0f, Vector3.Dot(horizontal, along));
                return true;
            }
            return false;
        }

        // The level direction straight down a slope with this normal (zero on the flat).
        private static Vector3 Downhill(Vector3 normal)
        {
            var d = new Vector3(normal.X, 0f, normal.Z);
            return d.LengthSquared() < 1e-8f ? Vector3.Zero : Vector3.Normalize(d);
        }

        private static Vector3 Approach(Vector3 current, Vector3 target, float maxChange)
        {
            var delta = target - current;
            var length = delta.Length();
            return length <= maxChange ? target : current + delta / length * maxChange;
        }
    }
}
