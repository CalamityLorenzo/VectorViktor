using Microsoft.Xna.Framework;
using System;
using World.Buildings;
using World.Core.Movement;
using World.Core.Physics;
using Xunit;

namespace World.Core.Tests
{
    // Doors hung in buildings' doorways (see Door, BuildingGround.StepDoors and Interact).
    public class DoorTests
    {
        private const float Floor = 0.15f;
        private const float South = MathHelper.Pi, North = 0f;

        // A 6 x 6 room at the origin, a door in the middle of its north wall (the wall's inner face at z = -3)
        private static RoomSpec Hut(float doorWidth = 1f) => new RoomSpec
        {
            Id = "hut", Name = "hut",
            Outline = RoomSpec.Rectangle(6f, 6f), Height = 2.6f,
            Floor = Color.Gray, WallA = Color.White, WallB = Color.LightGray, Ceiling = Color.DarkGray,
            WorldOffset = new Vector3(0f, Floor, 0f),
            Openings = new[] { new OpeningSpec(Walls.North, 0f, doorWidth, 2.1f, null, Door: true) },
        };

        private static BuildingGround Ground(float doorWidth = 1f) =>
            new BuildingGround(Grounds.Flat(), new[] { new Building("hut", Hut(doorWidth)) });

        private static CharacterController Walker(IGround ground, Vector3 at, float yaw)
        {
            var walker = new CharacterController(at, yaw);
            walker.SnapToGround(ground);
            return walker;
        }

        // Ticks the doors and the walker together, as the game does.
        private static void Run(BuildingGround ground, CharacterController walker, MoveInput input, float seconds, PhysicsWorld world = null)
        {
            for (var t = 0; t < (int)MathF.Round(seconds / Grounds.Tick); t++)
            {
                ground.StepDoors(Grounds.Tick, world?.Bodies ?? Array.Empty<Body>(),
                                 new[] { (walker.Position, CharacterController.Radius, CharacterController.Height) });
                walker.Step(input, Grounds.Tick, (IGround)world ?? ground);
                world?.Step(Grounds.Tick);
            }
        }

        [Fact]
        public void AShutDoorStopsYou()
        {
            var ground = Ground();
            var walker = Walker(ground, new Vector3(0f, 0f, -8f), South);
            Run(ground, walker, Grounds.Forward(), 5f);
            Assert.InRange(walker.Position.Z, -3f - CharacterController.Radius - Door.Thickness, -3f - CharacterController.Radius + 0.01f);
        }

        [Fact]
        public void OpenedItLetsYouIn()
        {
            var ground = Ground();
            var walker = Walker(ground, new Vector3(0f, 0f, -4f), South);
            var door = ground.Interact(walker.Position, walker.Heading);
            Assert.NotNull(door);
            Run(ground, walker, MoveInput.None, 1f);
            Assert.Equal(Door.MaxOpen, door.Angle);

            Run(ground, walker, Grounds.Forward(), 3f);
            Assert.True(walker.Position.Z > -2f);
            Assert.Equal(Floor, walker.Position.Y, 3);
        }

        [Fact]
        public void ItShutsAgain()
        {
            var ground = Ground();
            var walker = Walker(ground, new Vector3(0f, 0f, -4f), South);
            var door = ground.Interact(walker.Position, walker.Heading);
            Run(ground, walker, MoveInput.None, 1f);
            Assert.Same(door, ground.Interact(walker.Position, walker.Heading));
            Run(ground, walker, MoveInput.None, 1f);
            Assert.True(door.IsShut);
        }

        [Fact]
        public void OnlyTheDoorInFrontOfYouOpens()
        {
            var ground = Ground();
            Assert.Null(ground.Interact(new Vector3(0f, 0f, -4f), new Vector3(0f, 0f, -1f)));   // facing away
            Assert.Null(ground.Interact(new Vector3(0f, 0f, -6f), new Vector3(0f, 0f, 1f)));    // too far off
            Assert.True(ground.Doors[0].IsShut);
        }

        [Fact]
        public void ItWontShutOnYou()
        {
            var ground = Ground();
            var walker = Walker(ground, new Vector3(0f, 0f, -4f), South);
            var door = ground.Interact(walker.Position, walker.Heading);
            Run(ground, walker, MoveInput.None, 1f);

            // Into the doorway, and shut it on yourself
            Run(ground, walker, Grounds.Forward(), 0.4f);
            Assert.InRange(walker.Position.Z, -3.4f, -2.6f);
            door.Toggle();
            Run(ground, walker, MoveInput.None, 1f);
            Assert.True(door.Angle > 0.5f);
        }

        [Fact]
        public void ItWontSwingThroughACrate()
        {
            var ground = Ground();
            var world = new PhysicsWorld(ground);
            world.Add(new Body("crate", new Vector3(0.5f, 0.5f, 0.5f), 20f, new Vector3(0.1f, Floor, -2.2f)));   // just inside, in its swing
            var walker = Walker(world, new Vector3(0f, 0f, -4f), South);
            var door = ground.Interact(walker.Position, walker.Heading);
            Run(ground, walker, MoveInput.None, 1f, world);
            Assert.InRange(door.Angle, 0.1f, Door.MaxOpen - 0.1f);
        }

        [Fact]
        public void AWideDoorwayGetsAPair()
        {
            var ground = Ground(doorWidth: 3f);
            Assert.Equal(2, ground.Doors.Count);
            Assert.All(ground.Doors, d => Assert.Equal(1.5f, d.Width, 1));

            // Standing just either side of the middle, it's the leaf on that side that opens
            var first = ground.Interact(new Vector3(0.2f, 0f, -4f), new Vector3(0f, 0f, 1f));
            var second = ground.Interact(new Vector3(-0.2f, 0f, -4f), new Vector3(0f, 0f, 1f));
            Assert.NotSame(first, second);
        }
    }
}
