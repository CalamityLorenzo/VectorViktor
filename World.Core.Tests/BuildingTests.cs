using Microsoft.Xna.Framework;
using System;
using World.Buildings;
using World.Core.Movement;
using World.Core.Physics;
using Xunit;

namespace World.Core.Tests
{
    // Walking in and round buildings standing on flat ground (see BuildingGround).
    public class BuildingTests
    {
        private const float Floor = 0.15f;         // a step up from the ground outside
        private const float Height = 2.6f;
        private const float Slab = 0.3f;
        private const float North = 0f, South = MathHelper.Pi;

        private static RoomSpec Room(string id, float size, Vector3 at, float height = Height, OpeningSpec[] openings = null,
                                     RampSpec[] ramps = null, HatchSpec[] ceilingHatches = null, HatchSpec[] floorHatches = null) => new RoomSpec
        {
            Id = id, Name = id,
            Outline = RoomSpec.Rectangle(size, size), Height = height,
            Floor = Color.Gray, WallA = Color.White, WallB = Color.LightGray, Ceiling = Color.DarkGray,
            WorldOffset = at,
            Openings = openings ?? Array.Empty<OpeningSpec>(),
            Ramps = ramps ?? Array.Empty<RampSpec>(),
            CeilingHatches = ceilingHatches ?? Array.Empty<HatchSpec>(),
            FloorHatches = floorHatches ?? Array.Empty<HatchSpec>(),
        };

        // A 6 x 6 room at the origin with a doorway in the middle of its north wall
        private static RoomSpec Hut(float height = Height, RampSpec[] ramps = null) =>
            Room("hut", 6f, new Vector3(0f, Floor, 0f), height,
                 new[] { new OpeningSpec(Walls.North, 0f, 1.0f, 2.1f, null) }, ramps);

        private static BuildingGround On(params Building[] buildings) => new BuildingGround(Grounds.Flat(), buildings);

        private static CharacterController Walker(IGround ground, Vector3 at, float yaw)
        {
            var walker = new CharacterController(at, yaw);
            walker.SnapToGround(ground);
            return walker;
        }

        private static float YawTowards(Vector3 from, Vector3 to) => MathF.Atan2(to.X - from.X, -(to.Z - from.Z));

        // Walks at `target` (turning to face it each tick) until within `near` of it, or out of time.
        private static void WalkTo(CharacterController walker, Vector3 target, IGround ground, float near = 0.15f, float seconds = 10f)
        {
            for (var t = 0; t < (int)(seconds / Grounds.Tick); t++)
            {
                var gap = new Vector2(target.X - walker.Position.X, target.Z - walker.Position.Z);
                if (gap.Length() < near)
                    return;
                walker.Yaw = YawTowards(walker.Position, target);
                walker.Step(Grounds.Forward(), Grounds.Tick, ground);
            }
        }

        [Fact]
        public void TheWallsStopAWalkerOutside()
        {
            var hut = Room("hut", 6f, new Vector3(0f, Floor, 0f), openings: new[] { new OpeningSpec(Walls.South, 0f, 1.0f, 2.1f, null) });
            var building = new Building("hut", hut);
            var ground = On(building);
            var walker = Walker(ground, new Vector3(0f, 0f, -8f), South);
            Grounds.Run(walker, Grounds.Forward(), 5f, ground);

            // Stopped a body's radius short of the outside of the north wall
            var outside = -3f - building.WallThickness - CharacterController.Radius;
            Assert.InRange(walker.Position.Z, outside - 0.05f, outside + 0.01f);
            Assert.Equal(0f, walker.Position.Y);
        }

        [Fact]
        public void ADoorwayLetsAWalkerIn()
        {
            var hut = Hut();
            var ground = On(new Building("hut", hut));
            var walker = Walker(ground, new Vector3(0f, 0f, -8f), South);
            Grounds.Run(walker, Grounds.Forward(), 5f, ground);

            // Up the step and in, as far as the far wall
            Assert.True(hut.Contains(walker.Position - hut.WorldOffset));
            Assert.Equal(Floor, walker.Position.Y, 3);
            Assert.InRange(walker.Position.Z, 3f - CharacterController.Radius - 0.05f, 3f - CharacterController.Radius + 0.01f);
        }

        [Fact]
        public void AWalkerSlidesAlongAWallWalkedIntoAtAnAngle()
        {
            var hut = Hut();
            var ground = On(new Building("hut", hut));
            var walker = Walker(ground, new Vector3(0f, Floor, 0f), South + 0.5f);   // towards the south wall, turned towards -X
            Grounds.Run(walker, Grounds.Forward(), 1.5f, ground);
            var before = walker.Position.X;
            Grounds.Run(walker, Grounds.Forward(), 0.5f, ground);
            Assert.True(walker.Position.X < before - 0.3f, $"stuck at x = {walker.Position.X}");
        }

        [Fact]
        public void ACeilingStopsAJump()
        {
            var hut = Hut(height: 2.2f);
            var ground = On(new Building("hut", hut));
            var walker = Walker(ground, new Vector3(0f, Floor, 0f), North);
            walker.Step(new MoveInput(Vector2.Zero, Jump: true), Grounds.Tick, ground);
            var top = 0f;
            Grounds.Run(walker, MoveInput.None, 1f, ground, w => top = MathF.Max(top, w.Position.Y));
            Assert.InRange(top + CharacterController.Height, Floor + 2.2f - 0.01f, Floor + 2.2f + 0.001f);
            Assert.True(walker.Grounded);
        }

        // Two storeys, 7 x 7, the stair climbing against the east wall then the south one, up through a
        // hatch over its last flight.
        private static (Building building, RoomSpec lower, RoomSpec upper, WallStair stair) House()
        {
            var outline = RoomSpec.Rectangle(7f, 7f);
            var stair = new WallStair(outline, firstWall: Walls.East, steps: new[] { 4, 12 }, stepsPerWall: 12, height: Height + Slab, width: 1f);
            var hatch = stair.Hatch(margin: 0.05f);
            var lower = Room("down", 7f, new Vector3(0f, Floor, 0f),
                             openings: new[] { new OpeningSpec(Walls.North, 0f, 1.0f, 2.1f, null) },
                             ramps: stair.Ramps(), ceilingHatches: new[] { new HatchSpec(hatch, "up", Slab) });
            var upper = Room("up", 7f, new Vector3(0f, Floor + Height + Slab, 0f), floorHatches: new[] { new HatchSpec(hatch, "down") });
            return (new Building("house", lower, upper), lower, upper, stair);
        }

        [Fact]
        public void TheStairClimbsUpThroughTheHatchOntoTheUpperFloor()
        {
            var (building, _, upper, stair) = House();
            var ground = On(building);
            var ramps = stair.Ramps();
            var offset = new Vector3(0f, Floor, 0f);

            // To the foot of the first flight, up it, round the landing and up the second
            var walker = Walker(ground, new Vector3(0f, Floor, 0f), North);
            WalkTo(walker, (ramps[0].Start + offset - Vector3.Normalize(ramps[0].End - ramps[0].Start) * 0.5f) with { Y = 0f }, ground);
            WalkTo(walker, ramps[0].End + offset, ground);
            WalkTo(walker, ramps[1].Start + offset, ground);
            WalkTo(walker, ramps[1].End + offset, ground);
            Assert.InRange(walker.Position.Y, upper.WorldOffset.Y - 0.1f, upper.WorldOffset.Y);   // nearly at the top

            // Off the top and across the upper floor
            WalkTo(walker, new Vector3(-3f, 0f, 3f) + offset, ground);
            WalkTo(walker, new Vector3(-2f, 0f, -2f) + offset, ground);
            Assert.True(walker.Grounded);
            Assert.Equal(upper.WorldOffset.Y, walker.Position.Y, 3);
            Assert.InRange(walker.Position.Z, -2.2f, -1.8f);
        }

        [Fact]
        public void TheStairsSideStopsAWalkerOnTheFloor()
        {
            var (building, lower, _, stair) = House();
            var ground = On(building);
            var flight = stair.Ramps()[1];   // along the south wall, climbing west

            // Walking south at the middle of it, where it's more than a step up and its underside is below your head
            var middle = (flight.Start + flight.End) / 2f + lower.WorldOffset;
            var walker = Walker(ground, new Vector3(middle.X, Floor, 0f), South);
            Grounds.Run(walker, Grounds.Forward(), 3f, ground);

            var side = middle.Z - flight.Width / 2f;
            Assert.InRange(walker.Position.Z, side - CharacterController.Radius - 0.05f, side - CharacterController.Radius + 0.01f);
            Assert.Equal(Floor, walker.Position.Y, 3);
        }

        [Fact]
        public void AWalkerFitsUnderTheHighEndOfTheStair()
        {
            var (building, lower, _, stair) = House();
            var ground = On(building);
            var flight = stair.Ramps()[1];

            // Near its top, its underside is well over head height: walk right under it to the wall
            var high = Vector3.Lerp(flight.Start, flight.End, 0.95f) + lower.WorldOffset;
            var walker = Walker(ground, new Vector3(high.X, Floor, 0f), South);
            Grounds.Run(walker, Grounds.Forward(), 3f, ground);
            Assert.InRange(walker.Position.Z, 3.5f - CharacterController.Radius - 0.05f, 3.5f);
            Assert.Equal(Floor, walker.Position.Y, 3);
        }

        [Fact]
        public void WalkingOverTheHatchFromAboveDropsYouOntoTheStair()
        {
            var (building, _, upper, stair) = House();
            var ground = On(building);
            var flight = stair.Ramps()[1];
            var low = Vector3.Lerp(flight.Start, flight.End, 0.3f) + new Vector3(0f, Floor, 0f);

            // From the middle of the upper floor, south onto the hatch over the low part of the flight
            var walker = Walker(ground, new Vector3(low.X, upper.WorldOffset.Y, 0f), South);
            Assert.Equal(upper.WorldOffset.Y, walker.Position.Y, 3);
            Grounds.Run(walker, Grounds.Forward(), 3f, ground);
            Assert.True(walker.Grounded);
            Assert.InRange(walker.Position.Y, Floor + 0.3f, upper.WorldOffset.Y - 1f);
        }

        [Fact]
        public void ALadderClimbsOntoALoft()
        {
            // A deck 3 m up across the south half, and a ladder up to its north edge (see the barn in Basic.World).
            // The deck isn't solid: its underside is well over your head, and a solid edge would stop you at the
            // top of the ladder, head first against it.
            var foot = new Vector3(0f, 0f, -0.8f);
            var head = new Vector3(0f, 3f, 0f);
            var hut = Hut(height: 5f, ramps: new[]
            {
                new RampSpec(new Vector3(-3f, 3f, 1.5f), new Vector3(3f, 3f, 1.5f), 3f),
                new RampSpec(foot, head, 1f, MaxStepUp: 5f),
            });
            var ground = On(new Building("barn", hut));
            var walker = Walker(ground, new Vector3(0f, Floor, -2.5f), South);
            Grounds.Run(walker, Grounds.Forward(), 3f, ground);
            Assert.Equal(Floor + 3f, walker.Position.Y, 2);
            Assert.True(walker.Position.Z > 0.5f);
        }

        [Fact]
        public void ABodyPushedAtAWallStopsAtIt()
        {
            var hut = Hut();
            var world = new PhysicsWorld(On(new Building("hut", hut)));
            var crate = world.Add(new Body("crate", new Vector3(0.6f, 0.6f, 0.6f), 20f, new Vector3(1.5f, Floor, 0f)));
            for (var t = 0; t < 120; t++)
            {
                crate.Velocity = new Vector3(0f, crate.Velocity.Y, 2f);   // shoved south, hard
                world.Step(Grounds.Tick);
            }
            Assert.InRange(crate.Position.Z + 0.3f, 2.5f, 3f);
            Assert.Equal(Floor, crate.Position.Y, 3);
        }

        [Fact]
        public void TheDronesLineOfSightStopsAtTheWalls()
        {
            var hut = Hut();
            var ground = On(new Building("hut", hut));
            var head = new Vector3(0f, Floor + 1.6f, 1f);
            var behind = ((IGround)ground).ClearLine(head, new Vector3(0f, Floor + 3.6f, 6f));   // out through the south wall and the ceiling
            Assert.True(hut.Contains(behind - hut.WorldOffset));
            Assert.True(behind.Y < Floor + Height);

            // Out through the doorway is clear, though
            var outside = new Vector3(0f, Floor + 1.6f, -6f);
            Assert.Equal(outside, ((IGround)ground).ClearLine(new Vector3(0f, Floor + 1.6f, 0f), outside));
        }
    }
}
