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
    public sealed class DroidMotion
    {
        public const float LeanPerAcceleration = 0.06f;   // radians of lean for each m/s² it speeds up or slows by
        public const float MaxLean = 0.35f;               // about 20 degrees
        public const float LeanSettling = 8f;             // how quickly the lean catches up with what it should be, per second

        private float _speed;          // along its heading, last tick
        private float _yaw;
        private bool _started;

        public float LeftRolled { get; private set; }     // metres each wheel's rim has rolled: forward is more
        public float RightRolled { get; private set; }
        public float Acceleration { get; private set; }   // along its heading, m/s², last tick
        public float Speed => _speed;

        // Tilt forward (more than nothing) or back, in radians; set it to lean it some other way.
        public float Lean { get; set; }

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
            var around = turned * DroidRig.Track / 2f;
            LeftRolled += along + around;
            RightRolled += along - around;

            Acceleration = _started ? (speed - _speed) / dt : 0f;
            var target = Math.Clamp(Acceleration * LeanPerAcceleration, -MaxLean, MaxLean);
            Lean += (target - Lean) * (1f - MathF.Exp(-LeanSettling * dt));

            _speed = speed;
            _yaw = yaw;
            _started = true;
        }

        public void Follow(CharacterController body, float dt) => Follow(body.Velocity, body.Yaw, dt);

        // Its wheels, its lean and its ear dishes (`seconds` in) on the rig, over whatever else is on it.
        public void Pose(Rig rig, float seconds)
        {
            DroidRig.Roll(rig, LeftRolled, RightRolled);
            DroidRig.Tilt(rig, Lean);
            DroidRig.Listen(rig, seconds);
        }
    }
}
