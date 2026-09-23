using Microsoft.Xna.Framework;
using World.Core.Movement;

namespace World.Core.Characters
{
    public enum ViewMode { FirstPerson, Drone }

    // The player character: a walker, and the camera drone that follows it. You see the world either
    // through the character's own eyes or from the drone.
    public sealed class Player
    {
        public const float EyeHeight = 1.6f;

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
        }
    }
}
