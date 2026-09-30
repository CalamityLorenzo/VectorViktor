using Microsoft.Xna.Framework;
using System;
using World.Core.Characters;

namespace World.Core.Vehicles
{
    // What the driver's asking for this tick: Throttle -1..1, forward to drive on (or brake, going backwards), back to
    // brake (or reverse, once stopped); Steer -1..1, positive turning right; and the handbrake, held.
    public readonly record struct DriveInput(float Throttle, float Steer, bool Handbrake = false)
    {
        public static readonly DriveInput None = new DriveInput(0f, 0f);
        public static readonly DriveInput Parked = new DriveInput(0f, 0f, Handbrake: true);
    }

    // A car on an IGround, driven arcade-fashion - it isn't a physics solver's, but it goes where a car would. Position
    // is the ground under its middle. Yaw as a walker's: 0 faces north (-Z), increasing turns right.
    //
    // On the ground, each tick:
    //  - the engine pushes it along, harder at low speed than high, up to MaxSpeed; back on the throttle brakes it, and
    //    once it's stopped, reverses it; left alone it slows, the engine and the road dragging on it
    //  - the slope under it pulls it back down, so it rolls back on a hill if it's let go, and a climb slows it
    //  - the front wheels turn it about its back axle, less far the faster it goes (SteerFade); the tyres hold it to
    //    the way it's pointing with no more than Grip, so too fast into a bend and it slides wide - and on the
    //    handbrake, far more. That's less, the steeper the ground (it's pressed onto it less), and across a slope the
    //    slope pulls it sideways, so on one too steep - landed on a mountainside off a jump - it slides down it
    // It moves in sub-steps, and each is checked at the edges of the car: ground rising more than MaxClimb in the last
    // half metre in to any of them - a wall's foot, a cliff - stops it there, bouncing it back off, less whatever it
    // was going along the wall; it can't turn into one either. If it's come down already in one - off a jump, onto a
    // rock face - it's lodged, and then it can go anywhere but further in, and turn as it likes, to get out. Over a
    // crest or off a ramp, where the ground falls away quicker than it would fall, it leaves the ground, and flies
    // until it comes down - and over an edge it falls.
    // In water deeper than FloodDepth, the engine floods and dies, for good.
    public sealed class Car
    {
        public const float Length = 4.2f, Width = 2.2f;          // the body, bumper to bumper and side to side
        public const float Wheelbase = 2.65f, Track = 1.9f;      // axle to axle, and wheel to wheel across
        public const float MaxSpeed = 38f;                        // metres per second: about 137 km/h
        public const float ReverseSpeed = 8f;
        public const float EngineAcceleration = 6f;               // from rest, falling off towards MaxSpeed
        public const float Braking = 11f;
        public const float EngineBraking = 1.2f;                  // off the throttle
        public const float RollingResistance = 0.3f;
        public const float AirDrag = 0.0012f;                     // slowing, per metre per second of speed, squared
        public const float Grip = 9.81f;                          // the most the tyres hold sideways: one g
        public const float HandbrakeGrip = 3f;
        public const float MaxSteer = 0.6f;                       // radians, the front wheels' furthest turn at rest...
        public const float SteerFade = 12f;                       // ...half that at this speed
        public const float SteerRate = 2.5f;                      // radians per second the wheels turn
        public const float MaxClimb = 0.35f;                      // a rise it rides over, within half a metre
        public const float Bounce = 0.2f;                         // of the speed into a wall, bounced back off it
        public const float FloodDepth = 0.7f;
        public const float Gravity = WorldConstants.Gravity;
        public const float EyeHeight = 1.1f, SeatOffset = 0.45f;  // the driver's eyes above the ground, and right of the middle
        private const float MaxSubStep = 0.25f;
        private const float Probe = 0.5f;                         // how far in from an edge a rise is measured from

        public Vector3 Position { get; private set; }
        public float Yaw { get; private set; }
        public Vector2 Velocity { get; private set; }             // across the ground (X, Z)
        public float VerticalSpeed { get; private set; }
        public bool Grounded { get; private set; }
        public float SteerAngle { get; private set; }
        public bool Flooded { get; private set; }
        public float WaterDepth { get; private set; }

        // Which way it's tipped, from where its wheels are on the ground: its own forward (nose up or down) and up
        public Vector3 Forward { get; private set; }
        public Vector3 Up { get; private set; } = Vector3.Up;

        // Its chase camera: further back and higher than a walker's drone, and quicker to follow
        public Drone Chase { get; }

        public Vector2 Heading2 => new Vector2(MathF.Sin(Yaw), -MathF.Cos(Yaw));
        public Vector2 Right2 => new Vector2(MathF.Cos(Yaw), MathF.Sin(Yaw));
        public Vector3 Heading => new Vector3(MathF.Sin(Yaw), 0f, -MathF.Cos(Yaw));

        // Forward, along the way it's pointing (negative reversing)
        public float Speed => Vector2.Dot(Velocity, Heading2);

        // Where the driver's eyes are
        public Vector3 Eye => Position + Up * EyeHeight + new Vector3(Right2.X, 0f, Right2.Y) * SeatOffset;

        public Car(Vector3 position, float yaw, IGround ground)
        {
            Position = position;
            Yaw = yaw;
            Forward = Heading;
            Chase = new Drone(position, yaw, followDistance: 7f, followHeight: 2.6f, stiffness: 8f);
            Settle(ground);
            Chase.Reset(Position, Yaw, ground);
        }

        // Down onto the ground under it, at rest
        public void Settle(IGround ground)
        {
            var below = GroundAt(new Vector2(Position.X, Position.Z), Position.Y + 1000f, ground);
            if (below.HasValue)
                Position = Position with { Y = below.Value };
            Velocity = Vector2.Zero;
            VerticalSpeed = 0f;
            Grounded = below.HasValue;
            Tilt(ground);
        }

        // Put somewhere else at once, at rest: to bring it to you, or you to it.
        public void Teleport(Vector3 position, float yaw, IGround ground)
        {
            Position = position;
            Yaw = yaw;
            Settle(ground);
            Chase.Reset(Position, Yaw, ground);
        }

        public void Step(in DriveInput input, float dt, IGround ground)
        {
            var water = ground.WaterAt(Position);
            WaterDepth = water.HasValue ? MathF.Max(0f, water.Value - Position.Y) : 0f;
            Flooded |= WaterDepth > FloodDepth;
            var throttle = Flooded ? 0f : MathHelper.Clamp(input.Throttle, -1f, 1f);

            // The front wheels turn towards where they're steered, less far the faster it's going
            var speed = Speed;
            var wanted = MathHelper.Clamp(input.Steer, -1f, 1f) * MaxSteer / (1f + MathF.Abs(speed) / SteerFade);
            SteerAngle += MathHelper.Clamp(wanted - SteerAngle, -SteerRate * dt, SteerRate * dt);

            if (Grounded)
                Drive(throttle, input.Handbrake, dt, ground);
            else
                Velocity *= 1f - AirDrag * Velocity.Length() * dt;

            Move(dt, ground);
            Fall(dt, ground);
            Tilt(ground);
            Chase.Step(Position, Yaw, Position + Up * 1.4f, dt, ground);
        }

        private void Drive(float throttle, bool handbrake, float dt, IGround ground)
        {
            var forward = Vector2.Dot(Velocity, Heading2);
            var sideways = Vector2.Dot(Velocity, Right2);

            // The engine and the brakes
            if (throttle > 0f)
                forward += forward < -0.5f ? Braking * throttle * dt : EngineAcceleration * (1f - forward / MaxSpeed) * throttle * dt;
            else if (throttle < 0f)
                forward += forward > 0.5f ? Braking * throttle * dt : EngineAcceleration * 0.6f * (1f + forward / ReverseSpeed) * throttle * dt;

            // The slope under it, along it and across it, and the handbrake against it, and whatever else it's doing
            forward -= Gravity * Forward.Y * dt;
            sideways -= Gravity * Vector3.Cross(Forward, Up).Y * dt;
            if (handbrake)
                forward -= MathF.Sign(forward) * MathF.Min(MathF.Abs(forward), Braking * dt);

            // The road and the air, and the engine off the throttle - none of which holds it still on a hill: the
            // rolling resistance and the engine braking fade away as it comes to a stop
            var creeping = MathF.Min(1f, MathF.Abs(forward) / 2f);
            var drag = (RollingResistance + (throttle == 0f ? EngineBraking : 0f)) * creeping + AirDrag * forward * forward;
            forward -= MathF.Sign(forward) * MathF.Min(MathF.Abs(forward), drag * dt);

            // Turning about the back axle - unless that would swing it into a wall - while the velocity stays the way
            // it was going, and the tyres pull it round to the new heading as far as they can grip
            Velocity = Heading2 * forward + Right2 * sideways;
            var yaw = MathHelper.WrapAngle(Yaw + forward * MathF.Tan(SteerAngle) / Wheelbase * dt);
            var middle = new Vector2(Position.X, Position.Z);
            if (Blocked(middle, yaw, ground) == null || Blocked(middle, Yaw, ground) != null)
                Yaw = yaw;
            var slip = Vector2.Dot(Velocity, Right2);
            var grip = (handbrake ? HandbrakeGrip : Grip) * MathF.Max(Up.Y, 0f) * dt;   // as hard as it's pressed onto the ground
            Velocity -= Right2 * MathF.Sign(slip) * MathF.Min(MathF.Abs(slip), grip);
        }

        // Across the ground, a sub-step at a time, stopped by any wall in the way
        private void Move(float dt, IGround ground)
        {
            var travel = Velocity * dt;
            var steps = Math.Max(1, (int)MathF.Ceiling(travel.Length() / MaxSubStep));
            for (var k = 0; k < steps; k++)
            {
                var step = Velocity * dt / steps;
                var here = new Vector2(Position.X, Position.Z);
                var at = here + step;
                if (Blocked(at, Yaw, ground) is { } wall && (Blocked(here, Yaw, ground) is not { } lodged || Vector2.Dot(step, lodged) < 0f))
                {
                    // Bounced back off it, and scraping along it
                    var into = Vector2.Dot(Velocity, wall);
                    if (into < 0f)
                        Velocity -= wall * into * (1f + Bounce);
                    Velocity *= 0.9f;
                    continue;
                }
                Position = new Vector3(at.X, Position.Y, at.Y);
            }
        }

        // Down onto the ground, or off it where it falls away faster than the car would fall: over a crest taken fast,
        // off a ramp, or over an edge. (Not quite as soon as it does: the road's height changes slope a little
        // wherever two of its straight pieces meet, and that's no reason to hop.)
        private void Fall(float dt, IGround ground)
        {
            const float stick = 0.03f;
            var below = GroundAt(new Vector2(Position.X, Position.Z), Position.Y + MaxClimb, ground);
            var falling = Position.Y + (VerticalSpeed - Gravity * dt) * dt;
            if (Grounded && below.HasValue && below.Value >= falling - stick)
            {
                // Rising and falling with the ground, no quicker than the slope it's on would take it
                var most = Velocity.Length() + 1f;
                VerticalSpeed = MathHelper.Clamp((below.Value - Position.Y) / dt, -most, most);
                Position = Position with { Y = below.Value };
                return;
            }
            VerticalSpeed -= Gravity * dt;
            if (below.HasValue && falling <= below.Value)
            {
                Position = Position with { Y = below.Value };
                VerticalSpeed = 0f;
                Grounded = true;
            }
            else
            {
                Position = Position with { Y = falling };
                Grounded = false;
            }
        }

        // Its forward and up, from the ground under its four wheels (level, in the air)
        private void Tilt(IGround ground)
        {
            var heading = Heading2;
            var right = Right2;
            if (!Grounded)
            {
                Forward = Vector3.Normalize(new Vector3(heading.X, 0f, heading.Y) + Vector3.Up * MathHelper.Clamp(VerticalSpeed / 40f, -0.4f, 0.4f));
                Up = Vector3.Normalize(Vector3.Cross(Vector3.Cross(Forward, Vector3.Up), Forward));
                return;
            }
            var middle = new Vector2(Position.X, Position.Z);
            float Wheel(float along, float across) =>
                GroundAt(middle + heading * along + right * across, Position.Y + MaxClimb, ground) ?? Position.Y;
            float frontLeft = Wheel(Wheelbase / 2f, -Track / 2f), frontRight = Wheel(Wheelbase / 2f, Track / 2f);
            float backLeft = Wheel(-Wheelbase / 2f, -Track / 2f), backRight = Wheel(-Wheelbase / 2f, Track / 2f);
            var forward = new Vector3(heading.X * Wheelbase, (frontLeft + frontRight - backLeft - backRight) / 2f, heading.Y * Wheelbase);
            var across = new Vector3(right.X * Track, (frontRight + backRight - frontLeft - backLeft) / 2f, right.Y * Track);
            Forward = Vector3.Normalize(forward);
            Up = Vector3.Normalize(Vector3.Cross(across, forward));
        }

        // If the car at `at` (X, Z), facing `yaw`, would be up against a wall: which way out of it, across the ground
        private Vector2? Blocked(Vector2 at, float yaw, IGround ground)
        {
            var heading = new Vector2(MathF.Sin(yaw), -MathF.Cos(yaw));
            var right = new Vector2(MathF.Cos(yaw), MathF.Sin(yaw));
            const float halfLength = Length / 2f, halfWidth = Width / 2f;
            Span<(float along, float across)> edges = stackalloc (float, float)[]
            {
                (halfLength, -halfWidth), (halfLength, 0f), (halfLength, halfWidth), (0f, halfWidth),
                (-halfLength, halfWidth), (-halfLength, 0f), (-halfLength, -halfWidth), (0f, -halfWidth),
            };
            foreach (var (along, across) in edges)
            {
                var edge = at + heading * along + right * across;
                var inward = Vector2.Normalize(at - edge);
                var there = GroundAt(edge, Position.Y + 2f * MaxClimb, ground);
                if (!there.HasValue)
                    return inward;   // the edge of the world
                var inside = GroundAt(edge + inward * Probe, Position.Y + 2f * MaxClimb, ground) ?? Position.Y;
                if (there.Value > MathF.Max(inside, Position.Y) + MaxClimb)
                {
                    var normal = ground.NormalAt(new Vector3(edge.X, there.Value, edge.Y));
                    var away = new Vector2(normal.X, normal.Z);
                    return away.LengthSquared() > 0.01f ? Vector2.Normalize(away) : inward;
                }
            }
            return null;
        }

        private static float? GroundAt(Vector2 at, float from, IGround ground) => ground.GroundBelow(new Vector3(at.X, from, at.Y), 0f);

        // Where to draw it: its mesh (see CarMesh), a unit long, facing +Z, its middle half its roof's height above
        // the ground, scaled up to Length
        public Matrix World
        {
            get
            {
                const float scale = 4.2f, groundToMiddle = 0.145f * scale;
                var right = Vector3.Normalize(Vector3.Cross(Up, Forward));
                var up = Vector3.Cross(Forward, right);
                var turn = Matrix.Identity;
                turn.Right = right;
                turn.Up = up;
                turn.Backward = Forward;
                return Matrix.CreateScale(scale) * turn * Matrix.CreateTranslation(Position + up * groundToMiddle);
            }
        }
    }
}
