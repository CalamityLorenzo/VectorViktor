using Microsoft.Xna.Framework;
using System;
using World.Core.Animation;
using World.Core.Movement;

namespace World.Core.Characters
{
    // How the droid's rig follows it about (see DroidRig): each wheel turned by how far it has rolled - the two differ
    // as it turns, and go opposite ways when it turns on the spot - and everything above the wheels leaning into
    // speeding up and back from slowing down, as a Segway does, settling smoothly. Follow it each tick with how the
    // droid moved; Pose puts that on the rig.
    //
    // The lean here only shows how it's moving. A Segway's lean is what makes it move (it leans, then goes); that
    // is an experiment of its own (see Droid.Playground's Segway), which sets Lean itself.
    //
    // On something other than the Segway's wheels (see Locomotion), the same, its own way:
    //  - tracks run round by how far each side has rolled, and the hull pitches to lie along the ground under its
    //    ends (Pitch), so it noses up a kerb and over it; its turntable turns by itself (Turret), asked for each tick
    //  - the tri-star's spiders tip forward a third of a turn for each step it goes up or down (ClusterTurn)
    //  - in the churn, nothing: it goes nowhere, so there's nothing to follow
    // The body's feet go up a step in one go (see CharacterController), so the rig doesn't: it's left where it was
    // (Heave) and catches up over a moment, at ClimbRate or more.
    public sealed class DroidMotion
    {
        public const float LeanPerAcceleration = 0.06f;   // radians of lean for each m/s² it speeds up or slows by
        public const float MaxLean = 0.35f;               // about 20 degrees
        public const float LeanSettling = 8f;             // how quickly the lean catches up with what it should be, per second

        public const float StepTick = 0.03f;     // the feet rising or dropping more than this in a tick, on the ground: a step
        public const float MaxStep = 0.4f;       // and more than this isn't one: a fall, or put somewhere else
        public const float ClimbRate = 0.8f;     // metres a second the rig catches up with a step, at least
        public const float PitchSettling = 12f;  // how quickly the hull settles onto the ground under it, per second
        public const float MaxPitch = 0.6f;

        private float _speed;          // along its heading, last tick
        private float _yaw;
        private bool _started;
        private float _feetY, _riser, _direction = 1f;
        private bool _wasGrounded;

        public DroidMotion(Locomotion locomotion = Locomotion.Segway) => Base = locomotion;

        // What it's going about on
        public Locomotion Base { get; }

        public float LeftRolled { get; private set; }     // metres each wheel's rim (each track) has rolled: forward is more
        public float RightRolled { get; private set; }
        public float Acceleration { get; private set; }   // along its heading, m/s², last tick
        public float Speed => _speed;

        // Tilt forward (more than nothing) or back, in radians; set it to lean it some other way.
        public float Lean { get; set; }

        // How far the rig is above its feet (below, less than nothing), catching up with a step
        public float Heave { get; private set; }

        // On tracks: the hull nose up (down, less than nothing), radians; and the turntable from straight ahead,
        // towards its left more
        public float Pitch { get; private set; }
        public float Turret { get; set; }

        // On tri-star wheels: how far the spiders have turned forward, a third of a turn for each step
        public float ClusterTurn { get; private set; }

        // How much it leans for each m/s² it speeds up or slows by: a tank or a tri-star, standing on its own, rocks
        // only a little
        private float LeanFactor => Base == Locomotion.Segway ? LeanPerAcceleration : LeanPerAcceleration * 0.4f;

        // After a tick: where it's facing and how fast it went, and how long the tick was.
        public void Follow(Vector3 velocity, float yaw, float dt)
        {
            if (dt <= 0f)
                return;
            var heading = new Vector3(MathF.Sin(yaw), 0f, -MathF.Cos(yaw));
            var speed = Vector3.Dot(new Vector3(velocity.X, 0f, velocity.Z), heading);
            var turned = _started ? MathHelper.WrapAngle(yaw - _yaw) : 0f;   // radians, turning right is more

            // Turning right, the left wheel goes round the outside of the turn, the right one the inside
            var along = speed * dt;
            var around = turned * DroidBases.Gauge(Base) / 2f;
            LeftRolled += along + around;
            RightRolled += along - around;

            Acceleration = _started ? (speed - _speed) / dt : 0f;
            var target = Math.Clamp(Acceleration * LeanFactor, -MaxLean, MaxLean);
            Lean += (target - Lean) * (1f - MathF.Exp(-LeanSettling * dt));

            if (MathF.Abs(speed) > 0.05f)
                _direction = MathF.Sign(speed);
            _speed = speed;
            _yaw = yaw;
            _started = true;
        }

        // After a tick of the body: as above, and its steps and the ground under it (`ground`, if there's any to look
        // at), and on tracks the turntable turned as asked (-1 to 1, towards its left more).
        public void Follow(CharacterController body, float dt, IGround? ground = null, float turretTurn = 0f)
        {
            var first = !_started;
            Follow(body.Velocity, body.Yaw, dt);
            if (dt <= 0f)
                return;
            var feet = body.Position;
            var grounded = body.Grounded;

            if (Base == Locomotion.Tracks)
            {
                Turret = MathHelper.WrapAngle(Turret + Math.Clamp(turretTurn, -1f, 1f) * DroidBases.TurretSpeed * dt);
                LieAlong(feet, body.Heading, grounded, ground, dt);
            }
            else if (!first && grounded && _wasGrounded)
                StepWith(feet.Y - _feetY, ground?.RiserAt(feet) ?? 0f, dt);
            else
                Heave = 0f;   // off the ground: it goes with its feet

            _feetY = feet.Y;
            _wasGrounded = grounded;
        }

        // The feet having gone up (down, less than nothing) `rise` this tick: a step, if they went all at once, which the
        // rig catches up with over a moment, and a tri-star's spiders turn by. On stairs walked as a slope (`riser`
        // high each), the rise is the climb, a little each tick.
        private void StepWith(float rise, float riser, float dt)
        {
            if (MathF.Abs(rise) > MaxStep)
            {
                Heave = 0f;   // put somewhere else
                return;
            }
            if (MathF.Abs(rise) > StepTick)
            {
                Heave -= rise;
                _riser = MathF.Abs(rise);
            }
            else if (riser > 0f && Base == Locomotion.TriStar)
                ClusterTurn += MathF.Abs(rise) / riser * DroidBases.ClusterStep * _direction;

            var before = Heave;
            var catchUp = (ClimbRate + 4f * MathF.Abs(Heave)) * dt;
            Heave = MathF.Abs(Heave) <= catchUp ? 0f : Heave - MathF.Sign(Heave) * catchUp;
            if (Base == Locomotion.TriStar && _riser > 0f)
                ClusterTurn += MathF.Abs(before - Heave) / _riser * DroidBases.ClusterStep * _direction;
        }

        // On tracks: the hull settling onto the ground under its two ends, `heading` being forward: nosing up onto a kerb
        // before its middle gets there, and level again once its tail's up too.
        private void LieAlong(Vector3 feet, Vector3 heading, bool grounded, IGround? ground, float dt)
        {
            var (pitch, heave) = (0f, 0f);
            if (grounded && ground != null)
            {
                const float reach = 0.3f;
                var half = DroidBases.TrackSpan / 2f + DroidBases.PathRadius * 0.5f;
                float Under(Vector3 p) => Math.Clamp(ground.GroundBelow(p, reach) ?? feet.Y, feet.Y - reach, feet.Y + reach);
                var (front, back) = (Under(feet + heading * half), Under(feet - heading * half));
                pitch = Math.Clamp(MathF.Atan2(front - back, 2f * half), -MaxPitch, MaxPitch);
                heave = (front + back) / 2f - feet.Y;
            }
            var settle = 1f - MathF.Exp(-PitchSettling * dt);
            Pitch += (pitch - Pitch) * settle;
            Heave += (heave - Heave) * settle;
        }

        // Its wheels (tracks, spiders), its lean and its ear dishes (`seconds` in) on the rig, over whatever else is on it.
        public void Pose(Rig rig, float seconds)
        {
            if (Heave != 0f)
                rig.Change(DroidRig.Root, p => p with { Translation = p.Translation + Vector3.Up * Heave });
            switch (Base)
            {
                case Locomotion.Tracks:
                    DroidBases.RunTracks(rig, LeftRolled, RightRolled);
                    DroidBases.Pitch(rig, Pitch);
                    DroidBases.TurnTurret(rig, Turret);
                    // The lean is into the way it's going, which the turntable may have turned away from
                    var axis = Vector3.Transform(Vector3.UnitX, Quaternion.CreateFromAxisAngle(Vector3.Up, -Turret));
                    rig.Change(DroidRig.Lean, p => p with { Rotation = Animation.Pose.Turn(axis, Lean) });
                    break;
                case Locomotion.TriStar:
                    DroidBases.Clusters(rig, LeftRolled, RightRolled, ClusterTurn);
                    DroidRig.Tilt(rig, Lean);
                    break;
                case Locomotion.Churn:
                    break;   // nothing to roll, and it doesn't go anywhere to lean into
                default:
                    DroidRig.Roll(rig, LeftRolled, RightRolled);
                    DroidRig.Tilt(rig, Lean);
                    break;
            }
            DroidRig.Listen(rig, seconds);
        }
    }
}
