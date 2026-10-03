using Maps.Home;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using World.Buildings;
using World.Core.Movement;
using Xunit;

namespace World.Core.Tests
{
    // The English house (see EnglishHouse): its rooms side by side, partitions apart (see RoomSpec.InnerWalls), walked
    // through by their doorways, upstairs and down, and its roofs - pitched over the front, flat over the wing at the back.
    public class EnglishHouseTests
    {
        private const float Floor = 0.15f;   // a step up from the ground outside
        private static readonly float Upstairs = Floor + EnglishHouse.DownHeight + Houses.Slab;

        private static Building House() =>
            EnglishHouse.Build("e", "English house", new Vector3(0f, Floor, 0f), Color.IndianRed, Color.SaddleBrown, Gable.Pitched(35f, alongX: true));

        private static RoomSpec Room(Building house, string name) => house.Rooms.Single(r => r.Id == "e." + name);

        // Every doorway through a partition has its twin in the room on the other side, the two linings meeting in the
        // partition's middle, gap for gap
        [Fact]
        public void EveryDoorwayThroughAPartitionMeetsItsTwinInTheMiddle()
        {
            var house = House();
            var through = 0;
            foreach (var room in house.Rooms)
                foreach (var opening in room.Openings.Where(o => Building.IsThrough(room, o)))
                {
                    through++;
                    var other = house.Rooms.Single(r => r.Id == opening.TargetRoom);
                    var twin = other.Openings.Single(o => o.TargetRoom == room.Id);
                    Assert.True(Building.IsThrough(other, twin), $"{other.Id}'s doorway to {room.Id} isn't through a partition");
                    var (_, _, ml, mr) = house.Through(room, opening);
                    var (_, _, tl, tr) = house.Through(other, twin);
                    // Facing each other, each one's left is the other's right
                    Assert.True(Vector2.Distance(ml, tr) < 0.01f && Vector2.Distance(mr, tl) < 0.01f,
                        $"{room.Id}'s doorway to {other.Id} ({ml}, {mr}) doesn't meet its twin ({tr}, {tl})");
                }
            Assert.Equal(12, through);   // six doorways between rooms, lined from both sides
        }

        // The whole of the upper storey is under one roof; the kitchen and dining room, with nothing on them, under flat ones
        [Fact]
        public void TheFrontIsUnderOnePitchedRoofAndTheWingUnderAFlatOne()
        {
            var house = House();
            var frames = new[] { "landing", "bedroom", "bathroom" }.Select(r => house.RoofOver(Room(house, r))).ToArray();
            Assert.All(frames, f => Assert.NotNull(f));
            Assert.All(frames, f => Assert.Equal(frames[0], f));
            Assert.Equal(EnglishHouse.Width / 2f, frames[0].Value.HalfAlong, 3);
            foreach (var room in new[] { "kitchen", "dining" })
            {
                Assert.Null(house.RoofOf(Room(house, room)));
                Assert.True(house.ShellSpan(Room(house, room)).roofed);
            }
            foreach (var room in new[] { "hall", "living" })
                Assert.False(house.ShellSpan(Room(house, room)).roofed);
        }

        // The shells of two rooms either side of a partition meet in its middle, so the outside walls run on unbroken
        [Fact]
        public void TheShellRoundTwoRoomsMeetsInThePartitionsMiddle()
        {
            var house = House();
            var bedroom = house.OuterOutline(Room(house, "bedroom"));
            var bathroom = house.OuterOutline(Room(house, "bathroom"));
            // The bedroom's south-west corner and the bathroom's north-west, both out at the west wall's outside face
            Assert.Equal(new Vector2(-EnglishHouse.Width / 2f - house.WallThickness, -1.35f), bedroom[3]);
            Assert.True(Vector2.Distance(bedroom[3], bathroom[0]) < 0.001f, $"{bedroom[3]} and {bathroom[0]}");
        }

        // Every door opened, round the ground floor through each doorway, then up the stairs and into the rooms upstairs
        [Fact]
        public void YouCanWalkFromRoomToRoomUpstairsAndDown()
        {
            var house = House();
            var ground = new BuildingGround(Grounds.Flat(), new[] { house });
            foreach (var door in ground.Doors)
                door.Toggle();
            for (var t = 0; t < 60; t++)
                ground.StepDoors(Grounds.Tick, Array.Empty<Physics.Body>(), Array.Empty<(Vector3, float, float)>());
            Assert.All(ground.Doors, d => Assert.Equal(Door.MaxOpen, d.Angle));

            var walker = new CharacterController(new Vector3(2.95f, 0f, -6.5f), 0f);
            walker.SnapToGround(ground);
            void Through(string room, float floor, params (float x, float z)[] route)
            {
                foreach (var (x, z) in route)
                    WalkTo(walker, new Vector3(x, 0f, z), ground);
                var into = Room(house, room);
                Assert.True(into.Contains(walker.Position - into.WorldOffset), $"not in the {room}, at {walker.Position}");
                Assert.Equal(floor, walker.Position.Y, 2);
            }

            Through("hall", Floor, (2.95f, -3.9f));
            Through("living", Floor, (2.9f, -3.0f), (1.5f, -3.0f));
            Through("dining", Floor, (-2.25f, 0.3f), (-2.25f, 1.8f));
            Through("kitchen", Floor, (-1.2f, 1.7f), (-0.6f, 2.2f), (0.8f, 2.2f));
            Through("hall", Floor, (3.0f, 2.4f), (3.0f, 0.4f));

            // To the foot of the stairs, up them, and off the top onto the landing
            Through("landing", Upstairs, (3.0f, -4.0f), (4.05f, -4.1f), (4.05f, -0.1f), (4.05f, 0.5f), (2.9f, 0.5f));
            Through("bathroom", Upstairs, (2.7f, -0.15f), (1.4f, -0.15f));
            Through("landing", Upstairs, (2.7f, -0.15f), (2.9f, -3.0f));
            Through("bedroom", Upstairs, (1.4f, -3.0f));
        }

        // A shut door between rooms stops you, as one out of a building does
        [Fact]
        public void AShutDoorBetweenRoomsStopsYou()
        {
            var house = House();
            var ground = new BuildingGround(Grounds.Flat(), new[] { house });
            var walker = new CharacterController(new Vector3(2.9f, 0f, -3.0f), -MathHelper.PiOver2);   // in the hall, facing the living room
            walker.SnapToGround(ground);
            Grounds.Run(walker, Grounds.Forward(), 2f, ground);
            Assert.True(Room(house, "hall").Contains(walker.Position - Room(house, "hall").WorldOffset), $"at {walker.Position}");
        }

        private static void WalkTo(CharacterController walker, Vector3 target, IGround ground, float near = 0.15f, float seconds = 10f)
        {
            for (var t = 0; t < (int)(seconds / Grounds.Tick); t++)
            {
                var gap = new Vector2(target.X - walker.Position.X, target.Z - walker.Position.Z);
                if (gap.Length() < near)
                    return;
                walker.Yaw = MathF.Atan2(target.X - walker.Position.X, -(target.Z - walker.Position.Z));
                walker.Step(Grounds.Forward(), Grounds.Tick, ground);
            }
            throw new Xunit.Sdk.XunitException($"didn't get to {target}: stuck at {walker.Position}");
        }
    }
}
