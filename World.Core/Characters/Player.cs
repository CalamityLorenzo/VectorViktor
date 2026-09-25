using Microsoft.Xna.Framework;
using System;
using World.Core.Movement;
using World.Core.Physics;

namespace World.Core.Characters
{
    public enum ViewMode { FirstPerson, Drone }

    // The player character: a walker, and the camera drone that follows it. You see the world either
    // through the character's own eyes or from the drone.
    //
    // Wading or swimming soaks you as high as the water comes up you, at once; out of it, you dry off, from
    // soaked to dry in DryingTime.
    public sealed class Player
    {
        public const float EyeHeight = 1.6f;
        public const float Height = CharacterController.Height;
        public const float DryingTime = 90f;   // seconds

        // How wet you are: 0 dry, 1 soaked to the top of your head.
        public float Wetness { get; private set; }

        public CharacterController Body { get; }
        public Drone Drone { get; }
        public ViewMode View { get; set; } = ViewMode.FirstPerson;

        public Vector3 Eye => Body.Position + Vector3.Up * EyeHeight;

        public Player(Vector3 feet, float yaw, IGround ground)
        {
            Body = new CharacterController(feet, yaw);
            Body.SnapToGround(ground);
            Drone = new Drone(feet, yaw);
            Drone.Reset(Body.Position, yaw, ground);
        }

        public void ToggleView() => View = View == ViewMode.FirstPerson ? ViewMode.Drone : ViewMode.FirstPerson;

        public void Step(in MoveInput input, float dt, IGround ground)
        {
            Body.Step(input, dt, ground);
            Drone.Step(Body.Position, Body.Yaw, Eye, dt, ground);
            Soak(dt);
        }

        private void Soak(float dt)
        {
            var immersion = MathHelper.Clamp(Body.WaterDepth / Height, 0f, 1f);
            Wetness = MathF.Max(immersion, Wetness - dt / DryingTime);
        }

        // Among bodies: stood on one, it carries you along with it; walk into one and you push it. Step
        // the world itself after this, so it moves them on with this tick's pushes.
        public void Step(in MoveInput input, float dt, PhysicsWorld world)
        {
            var under = Body.Grounded ? world.BodyUnder(Body.Position) : null;
            if (under != null)
                Body.Position += new Vector3(under.Velocity.X, 0f, under.Velocity.Z) * dt;

            Body.Step(input, dt, world);
            world.PushWalker(Body, Height);
            Drone.Step(Body.Position, Body.Yaw, Eye, dt, world);
            Soak(dt);
        }
    }
}
