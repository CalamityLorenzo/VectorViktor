using Maps.Home;
using Microsoft.Xna.Framework;
using System;
using System.Linq;
using World.Core.Characters;
using World.Core.Movement;
using World.Maps;
using Xunit;

namespace World.Core.Tests
{
    // Basic.Levels' house in the home map (see HouseRooms), reached by the front door of the cottage whose window
    // looks into the lane's hangar
    public class HouseRoomsTests
    {
        private static readonly Lazy<BuiltWorld> Home = new Lazy<BuiltWorld>(() => WorldBuilder.Build(HomeMap.Map));

        // Walks `body` on for up to `seconds`, until it walks into a portal, and through it; whether it did
        private static bool WalkOn(CharacterController body, float seconds)
        {
            var world = Home.Value;
            for (var t = 0; t < (int)MathF.Round(seconds / Grounds.Tick); t++)
            {
                body.Step(Grounds.Forward(), Grounds.Tick, world.Ground);
                foreach (var portal in world.Portals)
                    if (portal.WalkedInto(body.Position, body.Gait.Radius))
                    {
                        body.Position = portal.To;
                        body.Yaw = portal.Yaw;
                        body.SnapToGround(world.Ground);
                        return true;
                    }
            }
            return false;
        }

        [Fact]
        public void EveryRoomOfTheLevelIsInTheMap()
        {
            var rooms = Home.Value.Buildings.SelectMany(b => b.Rooms).Select(r => r.Id).ToHashSet();
            foreach (var id in HouseLevel.Create().Rooms.Keys)
                Assert.Contains("level." + id, rooms);
        }

        // Every door in the level takes you onto the floor of the room it leads to
        [Fact]
        public void EachOfTheLevelsDoorsTakesYouOntoAFloor()
        {
            var world = Home.Value;
            var doors = HouseRooms.Rooms.Sum(room => room.Doors.Count(d => !string.IsNullOrEmpty(d.TargetRoom)));
            var into = world.Portals.Where(p => HouseRooms.Rooms.Any(room => Vector2.Distance(p.A, new Vector2(room.WorldOffset.X, room.WorldOffset.Z)) < 100f)
                                                && p.To.Z > 1900f).ToArray();
            Assert.True(into.Length >= doors, $"{into.Length} portals for {doors} doors");
            foreach (var portal in into)
                Assert.Equal(portal.To.Y, world.Ground.GroundBelow(portal.To + Vector3.Up * 0.1f, 0.3f) ?? float.NaN, 0.01f);
        }

        // A flat roof is over every room's ceiling, where it rises over a stair too (the level's stairwell's does, 3 m)
        [Fact]
        public void NoFlatRoofCutsThroughARoomUnderIt()
        {
            foreach (var building in Home.Value.Buildings)
                foreach (var room in building.Rooms)
                {
                    var (_, top, roofed) = building.ShellSpan(room);
                    if (!roofed || building.RoofOf(room) != null)
                        continue;
                    var min = room.Outline.Aggregate(Vector2.Min);
                    var max = room.Outline.Aggregate(Vector2.Max);
                    for (var x = min.X; x <= max.X; x += 0.25f)
                        for (var z = min.Y; z <= max.Y; z += 0.25f)
                        {
                            var ceiling = room.WorldOffset.Y + room.CeilingHeightAt(new Vector3(x, 0f, z));
                            Assert.True(ceiling <= top - building.RoofThickness + 0.001f, $"{room.Id}'s ceiling at {x}, {z} is {ceiling}, its roof's underside {top - building.RoofThickness}");
                        }
                }
        }

        [Theory]
        [InlineData(null)]
        [InlineData(Locomotion.Segway)]
        public void TheHangarCottagesFrontDoorTakesYouIntoTheCorridorAndBack(Locomotion? locomotion)
        {
            var world = Home.Value;
            var gait = locomotion is { } l ? Locomotions.GaitOf(l) : Gait.Walker;
            var corridor = HouseRooms.Corridor;
            var arrival = RoomPortals.ArrivalBy(corridor, HouseRooms.FrontDoor);
            // The portal in the cottage's doorway, and the side of it the lane's on: where the "hangar" start is
            var door = world.Portals.Single(p => Vector3.Distance(p.To, arrival) < 0.01f);
            var outside = world.Starts["hangar"].At;
            var middle = (door.A + door.B) / 2f;
            var normal = Vector2.Normalize(new Vector2(-(door.B - door.A).Y, (door.B - door.A).X));
            if (Vector2.Dot(outside - middle, normal) < 0f)
                normal = -normal;
            var start = middle + normal * 2f;
            var body = new CharacterController(new Vector3(start.X, door.Floor + 1f, start.Y), MathF.Atan2(-normal.X, normal.Y)) { Gait = gait };
            body.SnapToGround(world.Ground);

            Assert.True(WalkOn(body, 3f), $"never went in, stopped at {body.Position}");
            Assert.Equal(corridor.WorldOffset.Y, body.Position.Y, 0.01f);
            Assert.Equal(HouseRooms.Start.Yaw, body.Yaw);   // looking up the corridor

            // Turned to the front door, beside you in the west wall, and through it: out in front of the cottage
            body.Yaw = -MathHelper.PiOver2;
            Assert.True(WalkOn(body, 3f), $"never went out, stopped at {body.Position}");
            Assert.True(Vector2.Distance(new Vector2(body.Position.X, body.Position.Z), middle) < 2f, $"came out at {body.Position}");
        }
    }
}
