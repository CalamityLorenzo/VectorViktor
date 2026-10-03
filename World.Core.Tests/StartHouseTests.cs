using Maps.Home;
using Microsoft.Xna.Framework;
using System.Linq;
using World.Buildings;
using World.Core.Movement;
using World.Maps;
using Xunit;

namespace World.Core.Tests
{
    // The house the game starts in (see StartHouse): its rooms partitions apart and walked through by their doorways,
    // its windows cut through its walls, the fence round its stairwell, and its front door onto another map.
    public class StartHouseTests
    {
        private static readonly Vector3 At = new Vector3(0f, 0.15f, 0f);

        private static Building House(bool atticOpen = false) => StartHouse.Build(At, atticOpen);

        private static RoomSpec Room(Building house, string name) => house.Rooms.Single(r => r.Id == "house." + name);

        // Its own map builds: its terrain, its house, its windows and its front door
        [Fact]
        public void ItsMapBuilds()
        {
            var world = WorldBuilder.Build(StartHouse.Map);
            Assert.Single(world.Buildings);
            Assert.Equal(8, world.Windows.Count);   // every glazed one: all but bedroom 1's open gap
            var door = Assert.Single(world.Portals);
            Assert.Equal(StartHouse.FrontDoorLeadsTo, door.ToMap);
            Assert.Contains(StartHouse.DefaultStart, world.Starts.Keys);
        }

        // The alarm clock's never been set: its digits are there half of every second, and gone the other half
        [Fact]
        public void TheClocksDigitsFlash()
        {
            var world = WorldBuilder.Build(StartHouse.Map);
            var digits = Assert.Single(world.Moving, part => part.Mesh.Key.StartsWith("alarmclock:"));
            Assert.NotEqual(World.Rendering.RigScene.OutOfSight, digits.At(0.25f));
            Assert.Equal(World.Rendering.RigScene.OutOfSight, digits.At(0.75f));
            Assert.Equal(digits.At(0.25f), digits.At(3.1f));
        }

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
                    var (_, _, ml, mr) = house.Through(room, opening);
                    var (_, _, tl, tr) = house.Through(other, twin);
                    Assert.True(Vector2.Distance(ml, tr) < 0.01f && Vector2.Distance(mr, tl) < 0.01f,
                        $"{room.Id}'s doorway to {other.Id} ({ml}, {mr}) doesn't meet its twin ({tr}, {tl})");
                }
            Assert.Equal(14, through);   // seven doorways between rooms, lined from both sides
        }

        // A window's a hole through the room's wall and the shell outside it: wall under its sill and over its head, none
        // between, so nothing walks out through it, but it can be seen through
        [Fact]
        public void AWindowIsAHoleWithWallUnderAndOverIt()
        {
            var house = House();
            var bedroom = Room(house, "bedroom1");
            var window = Assert.Single(bedroom.Windows);
            Assert.False(window.Glazed);
            var middle = Building.WallPoint(bedroom, window.WallIndex, window.Offset);
            var floor = bedroom.WorldOffset.Y;
            var across = house.Walls().Where(w => Distance(middle, w) < 0.3f).ToList();
            Assert.NotEmpty(across);
            Assert.Contains(across, w => w.Bottom <= floor && w.Top >= floor + window.Sill - 0.01f && w.Top <= floor + window.Sill + 0.01f);
            Assert.Contains(across, w => w.Bottom >= floor + window.Sill + window.Height - 0.01f);
            Assert.DoesNotContain(across, w => w.Bottom < floor + window.Sill + 0.5f && w.Top > floor + window.Sill + 0.5f);
        }

        // How far a point is from a stretch of wall's line, in plan
        private static float Distance(Vector2 p, WallSegment wall)
        {
            var along = wall.B - wall.A;
            var t = MathHelper.Clamp(Vector2.Dot(p - wall.A, along) / along.LengthSquared(), 0f, 1f);
            return Vector2.Distance(p, wall.A + along * t);
        }

        // The stairs climb from the hall to the landing's floor, through the hole in it, fenced on its open side
        [Fact]
        public void TheStairsClimbToTheLandingThroughAFencedHole()
        {
            var house = House();
            var hall = Room(house, "hall");
            var landing = Room(house, "landing");
            var top = hall.Ramps.Max(r => System.MathF.Max(r.Start.Y, r.End.Y));
            Assert.Equal(landing.WorldOffset.Y - hall.WorldOffset.Y, top, 3);
            var hole = Assert.Single(landing.FloorHatches);
            Assert.True(hole.Balusters);
            Assert.Equal(hall.Id, hole.TargetRoom);
            Assert.Contains(hall.CeilingHatches, h => h.TargetRoom == landing.Id && h.Balusters);
        }

        // Open, the attic's under the pitched roof, over the whole house, reached from the landing by a hatch in its ceiling
        [Fact]
        public void OpenTheAtticIsReachedThroughAHatchInTheLandingCeiling()
        {
            var house = House(atticOpen: true);
            var landing = Room(house, "landing");
            var attic = Room(house, "attic");
            Assert.NotNull(attic.Pitched);
            Assert.Contains(landing.CeilingHatches, h => h.TargetRoom == attic.Id);
            Assert.Contains(attic.FloorHatches, h => h.TargetRoom == landing.Id);
            Assert.Equal(landing.WorldOffset.Y + landing.Height + Houses.Slab, attic.WorldOffset.Y, 3);
        }

        // Every piece of furniture you can walk into stands inside its room, clear of every other one
        [Fact]
        public void FurnitureStandsInsideItsRoomAndClearOfEachOther()
        {
            foreach (var room in House().Rooms)
            {
                var blocking = room.Props.Where(p => p.Blocks).ToList();
                foreach (var prop in blocking)
                {
                    var (min, max) = Box(prop);
                    foreach (var corner in new[] { min, max, new Vector2(min.X, max.Y), new Vector2(max.X, min.Y) })
                        Assert.True(Geometry2D.InPolygon(room.Outline, corner), $"{room.Id}: {prop.Mesh.Key} at {prop.Position} pokes out of the room");
                }
                for (var i = 0; i < blocking.Count; i++)
                    for (var j = i + 1; j < blocking.Count; j++)
                    {
                        var (a0, a1) = Box(blocking[i]);
                        var (b0, b1) = Box(blocking[j]);
                        var overlap = a0.X < b1.X && b0.X < a1.X && a0.Y < b1.Y && b0.Y < a1.Y;
                        Assert.False(overlap, $"{room.Id}: {blocking[i].Mesh.Key} at {blocking[i].Position} overlaps {blocking[j].Mesh.Key} at {blocking[j].Position}");
                    }
            }
        }

        // The droid in its churn stands in bedroom 1, clear of the furniture and of the wardrobe beside it
        [Fact]
        public void TheDroidStandsInBedroom1ClearOfTheFurniture()
        {
            var bedroom = Room(House(), "bedroom1");
            var at = StartHouse.DroidAt;
            var r = World.Core.Characters.DroidBases.ChurnRadius + 0.05f;
            Assert.True(Geometry2D.InPolygon(bedroom.Outline, at));
            foreach (var prop in bedroom.Props.Where(p => p.Blocks))
            {
                var (min, max) = Box(prop);
                Assert.False(at.X + r > min.X && at.X - r < max.X && at.Y + r > min.Y && at.Y - r < max.Y, $"the droid's churn is in {prop.Mesh.Key}");
            }
            var wardrobe = Assert.Single(bedroom.Cabinets);
            var half = new Vector2(wardrobe.Depth, wardrobe.Width) / 2f;   // turned a quarter: its depth across X
            var middle = new Vector2(wardrobe.Position.X, wardrobe.Position.Z);
            Assert.True(System.MathF.Abs(at.Y - middle.Y) > half.Y + r || System.MathF.Abs(at.X - middle.X) > half.X + r);
        }

        private static (Vector2 min, Vector2 max) Box(PropSpec prop)
        {
            var at = new Vector2(prop.Position.X, prop.Position.Z);
            return (at - prop.Half, at + prop.Half);
        }

        // Open, walking up the ladder from the landing, you come up through the hatch and stand in the attic
        [Fact]
        public void OpenTheLadderClimbsIntoTheAttic()
        {
            var house = House(atticOpen: true);
            var landing = Room(house, "landing");
            var attic = Room(house, "attic");
            var ladder = landing.Ramps.First();   // foot to head
            var ground = new BuildingGround(Grounds.Flat(), new[] { house });
            var foot = landing.WorldOffset + ladder.Start;
            var head = landing.WorldOffset + ladder.End;
            var back = Vector3.Normalize(new Vector3(foot.X - head.X, 0f, foot.Z - head.Z));
            var walker = new CharacterController(foot + back * 0.6f, System.MathF.Atan2(-back.X, back.Z));   // facing up the ladder
            walker.SnapToGround(ground);
            Assert.Equal(landing.WorldOffset.Y, walker.Position.Y, 2);
            Grounds.Run(walker, Grounds.Forward(), 3f, ground);
            Assert.True(walker.Grounded);
            Assert.Equal(attic.WorldOffset.Y, walker.Position.Y, 2);
        }

        // For now the attic's shut: no way up from the landing, nothing to climb, but it's still there under the roof
        [Fact]
        public void TheAtticIsShutForNow()
        {
            Assert.False(StartHouse.AtticOpen);
            var house = StartHouse.Build(At);
            var landing = Room(house, "landing");
            var attic = Room(house, "attic");
            Assert.Empty(landing.CeilingHatches);
            Assert.Empty(attic.FloorHatches);
            Assert.Empty(landing.Ramps);
            Assert.NotNull(attic.Pitched);
        }

        // The locked room has no way in
        [Fact]
        public void TheLockedRoomHasNoWayIn()
        {
            var house = House();
            var locked = Room(house, "locked");
            Assert.Empty(locked.Openings);
            Assert.DoesNotContain(house.Rooms, r => r.Openings.Any(o => o.TargetRoom == locked.Id));
        }
    }
}
