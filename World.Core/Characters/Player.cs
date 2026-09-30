using Microsoft.Xna.Framework;
using System;
using World.Core.Movement;
using World.Core.Physics;
using World.Core.Vehicles;

namespace World.Core.Characters
{
    public enum ViewMode { FirstPerson, Drone }

    // The player character: a walker, and the camera drone that follows it. You see the world either
    // through the character's own eyes or from the drone.
    //
    // Wading or swimming soaks you as high as the water comes up you, at once; out of it, you dry off, from
    // soaked to dry in DryingTime.
    //
    // You can get into a car (see Car) and drive it, seen from its chase camera or from the driver's seat (on the right: it's a British car); while
    // you do, you go where it goes. You get out beside the driver's door, once it's all but stopped.
    public sealed class Player
    {
        public const float EyeHeight = WorldConstants.EyeHeight;
        public const float Height = CharacterController.Height;
        public const float DryingTime = 90f;   // seconds
        public const float CarReach = 3.5f;    // how near a car's middle you must be to get in
        public const float StoppedSpeed = 1.5f;   // metres per second: slow enough to get out

        // How wet you are: 0 dry, 1 soaked to the top of your head.
        public float Wetness { get; private set; }

        public CharacterController Body { get; }
        public Drone Drone { get; }
        public ViewMode View { get; set; } = ViewMode.FirstPerson;

        // The car you're driving, if you are
        public Car? Driving { get; private set; }

        public Vector3 Eye => Driving?.Eye ?? Body.Position + Vector3.Up * EyeHeight;

        public Player(Vector3 feet, float yaw, IGround ground)
        {
            Body = new CharacterController(feet, yaw);
            Body.SnapToGround(ground);
            Drone = new Drone(feet, yaw);
            Drone.Reset(Body.Position, yaw, ground);
        }

        // Puts you somewhere else at once, at rest, facing `yaw` - through a door that leads elsewhere, say - and
        // the drone straight to its station behind you.
        public void Teleport(Vector3 feet, float yaw, IGround ground)
        {
            Body.Position = feet;
            Body.Yaw = yaw;
            Body.SnapToGround(ground);
            Drone.Reset(Body.Position, yaw, ground);
        }

        public void ToggleView() => View = View == ViewMode.FirstPerson ? ViewMode.Drone : ViewMode.FirstPerson;

        // Whether you're near enough `car` to get in
        public bool CanReach(Car car) =>
            Driving == null && Vector2.Distance(new Vector2(car.Position.X, car.Position.Z), new Vector2(Body.Position.X, Body.Position.Z)) <= CarReach &&
            MathF.Abs(car.Position.Y - Body.Position.Y) < 1.5f;

        // Into the driver's seat, seen from there to begin with (the chase camera's the other view)
        public void GetIn(Car car)
        {
            Driving = car;
            View = ViewMode.FirstPerson;
            FollowCar();
        }

        // Out of the car, onto the ground beside the driver's door - or the other side's, if there's no standing
        // there - facing the way it does; not while it's still going, or if there's nowhere to stand either side.
        public bool GetOut(IGround ground)
        {
            if (Driving is not { } car || MathF.Abs(car.Speed) > StoppedSpeed || !car.Grounded)
                return false;
            foreach (var side in new[] { 1f, -1f })
            {
                var right = car.Right2 * side * (Car.Width / 2f + CharacterController.Radius + 0.2f);
                var spot = new Vector3(car.Position.X + right.X, car.Position.Y + CharacterController.MaxStepUp, car.Position.Z + right.Y);
                var below = ground.GroundBelow(spot, CharacterController.MaxStepUp * 2f);
                if (!below.HasValue || MathF.Abs(below.Value - car.Position.Y) > 0.5f)
                    continue;
                var feet = spot with { Y = below.Value };
                if (!ground.IsWalkable(feet) || Vector3.DistanceSquared(ground.KeepOut(feet, CharacterController.Radius, Height), feet) > 1e-4f)
                    continue;
                Driving = null;
                View = ViewMode.FirstPerson;
                Teleport(feet, car.Yaw, ground);
                return true;
            }
            return false;
        }

        // Driving: the car goes, and you with it
        public void Drive(in DriveInput input, float dt, IGround ground)
        {
            if (Driving is not { } car)
                return;
            car.Step(input, dt, ground);
            FollowCar();
        }

        private void FollowCar()
        {
            Body.Position = Driving!.Position;
            Body.Yaw = Driving.Yaw;
        }

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
