using Maps.Pass;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using World.Core.Movement;
using World.Maps;
using Xunit;
using static Maps.Pass.PassTerrain;

namespace World.Core.Tests
{
    public class PassTests
    {
        private static readonly Lazy<BuiltWorld> Built = new Lazy<BuiltWorld>(() => WorldBuilder.Build(PassMap.Map));
        private static Terrain Pass => Built.Value.Terrain;
        private static IGround Ground => Built.Value.Physics;

        // Put there as the game puts you at a start (see Start.Above), and left to settle
        private static CharacterController StandAt(string start)
        {
            var at = Built.Value.Starts[start];
            var walker = new CharacterController(new Vector3(at.At.X, Pass.HeightAt(at.At.X, at.At.Y) + at.Above, at.At.Y), at.Yaw);
            walker.SnapToGround(Ground);
            Grounds.Run(walker, MoveInput.None, 2f, Ground);
            return walker;
        }

        private static float OffTheRoad(Vector2 p) => PassRoute.Nearest(p).distance - PassRoute.HalfWidth;

        private static bool InAFlat(Vector2 p, float margin) => Flats.Any(f => f.Outside(p.X, p.Y) <= margin);

        [Fact]
        public void BuildsWithEveryStart()
        {
            Assert.Contains(PassMap.DefaultStart, Built.Value.Starts.Keys);
            foreach (var name in new[] { "trailhead", "firstpass", "lake", "secondpass", "town" })
                Assert.Contains(name, Built.Value.Starts.Keys);
        }

        [Fact]
        public void TheRoadIsNeverSteeperThanItsMaxGrade()
        {
            for (var k = 1; k < PassRoute.Points.Count; k++)
            {
                var run = PassRoute.Along[k] - PassRoute.Along[k - 1];
                var grade = MathF.Abs(PassRoute.Heights[k] - PassRoute.Heights[k - 1]) / run;
                Assert.True(grade <= PassRoute.MaxGrade, $"{grade:P1} at {PassRoute.Along[k]:F0} m along");
            }
        }

        // Anywhere two stretches of the road come near each other, they're the same stretch, not two legs of it
        // doubling back: so no leg's strip is cut into another's, and there's no dropping from one onto another.
        [Fact]
        public void TheRoadsLegsKeepWellApart()
        {
            var points = PassRoute.Points;
            var apart = 2f * PassRoute.HalfWidth + 30f;
            for (var a = 0; a < points.Count; a++)
                for (var b = a + 1; b < points.Count; b++)
                {
                    var distance = Vector2.Distance(points[a], points[b]);
                    if (distance < apart)
                        Assert.True(PassRoute.Along[b] - PassRoute.Along[a] < 1.6f * apart,
                                    $"the road at {PassRoute.Along[a]:F0} m and at {PassRoute.Along[b]:F0} m along is only {distance:F1} m apart");
                }
        }

        // The road's pieces, one after another, from the trailhead into the basin, with no gaps between them
        [Fact]
        public void TheRoadIsLaidAllTheWayFromTheTrailheadToTheBasin()
        {
            var pieces = Road.Pieces;
            Assert.Equal(0, pieces[0].from);
            Assert.Equal(PassRoute.Points.Count - 1, pieces[^1].to);
            for (var k = 1; k < pieces.Count; k++)
                Assert.Equal(pieces[k - 1].to, pieces[k].from);
            var end = PassRoute.Points[^1];
            Assert.True(Basin.Outside(end.X, end.Y) < 0f, "the road stops short of the basin");
            Assert.Equal(pieces.Count, Built.Value.Fixtures.Count);
        }

        // Every metre along it: the ground's the road's height, and level across it (clear of the cells over its edge)
        [Fact]
        public void TheGroundAlongTheRoadIsItsHeightAndLevelAcrossIt()
        {
            var terrain = Pass;
            for (var s = 0f; s < PassRoute.Length; s += 1f)
            {
                var (at, height, heading) = PassRoute.At(s);
                var across = new Vector2(-heading.Y, heading.X);
                foreach (var offset in new[] { -3.5f, 0f, 3.5f })
                {
                    var p = at + across * offset;
                    Assert.True(MathF.Abs(terrain.HeightAt(p.X, p.Y) - height) < 0.1f,
                                $"the ground's at {terrain.HeightAt(p.X, p.Y):F2}, not {height:F2}, {offset} m across the road {s} m along");
                }
            }
        }

        // Every triangle of ground a walker could reach from the trailhead without ever standing on one too steep to
        // walk: all on the road's strip (or a cell over its edge), at the trailhead or in the basin - and the basin's
        // among them, so the road goes all the way.
        [Fact]
        public void TheRoadIsTheOnlyWayAndItGoesAllTheWay()
        {
            var terrain = Pass;
            var seen = new HashSet<(int i, int j, bool southWest)>();
            var open = new Stack<(int i, int j, bool southWest)>();
            void Visit(int i, int j, bool southWest)
            {
                if (i < 0 || i >= terrain.Width || j < 0 || j >= terrain.Depth || !seen.Add((i, j, southWest)))
                    return;
                if (Terrain.IsWalkableNormal(terrain.TriangleNormal(i, j, southWest)))
                    open.Push((i, j, southWest));
            }

            var trailhead = PassRoute.Controls[PassRoute.Trailhead].at;
            int i0 = (int)((trailhead.X - terrain.OriginX) / terrain.CellSize), j0 = (int)((trailhead.Y - terrain.OriginZ) / terrain.CellSize);
            Visit(i0, j0, false);
            var reachedTheTown = false;
            while (open.Count > 0)
            {
                var (i, j, southWest) = open.Pop();
                var (a, b, c) = terrain.Triangle(i, j, southWest);
                var middle = new Vector2(a.X + b.X + c.X, a.Z + b.Z + c.Z) / 3f;
                Assert.True(OffTheRoad(middle) <= 1.5f || InAFlat(middle, 1.5f), $"walkable ground off the road at {middle}");
                reachedTheTown |= Vector2.Distance(middle, TownLakeCentre) < TownLakeRadius + TownLakeShore;
                Visit(i, j, !southWest);
                if (southWest)
                {
                    Visit(i, j + 1, false);
                    Visit(i - 1, j, false);
                }
                else
                {
                    Visit(i, j - 1, true);
                    Visit(i + 1, j, true);
                }
            }
            Assert.True(reachedTheTown, "the road doesn't lead to the town's lake");
        }

        // Running the whole way from the trailhead, steering along the road: on it all the way, and into the basin
        [Fact]
        public void YouCanRunTheRoadFromTheTrailheadToTheTown()
        {
            var walker = StandAt("trailhead");
            var running = new MoveInput(new Vector2(0f, 1f), Run: true);
            for (var t = 0; t < (int)(600f / Grounds.Tick); t++)
            {
                var here = new Vector2(walker.Position.X, walker.Position.Z);
                if (Basin.Outside(here.X, here.Y) < -10f)
                    return;
                var (s, distance, height) = PassRoute.Nearest(here);
                Assert.True(distance <= PassRoute.HalfWidth || InAFlat(here, 0f), $"off the road at {here}, {s:F0} m along");
                var ahead = PassRoute.At(s + 6f).at - here;
                walker.Yaw = MathF.Atan2(ahead.X, -ahead.Y);
                walker.Step(running, Grounds.Tick, Ground);
            }
            Assert.Fail($"never got to the town: stopped at {walker.Position}");
        }

        // Turning off the road and running and jumping at the walls either side, at the two passes: you get nowhere
        [Theory]
        [InlineData("firstpass", -1f)]
        [InlineData("firstpass", 1f)]
        [InlineData("secondpass", -1f)]
        [InlineData("secondpass", 1f)]
        public void TheWallsBesideTheRoadCantBeClimbed(string start, float side)
        {
            var walker = StandAt(start);
            walker.Yaw += side * MathHelper.PiOver2;
            var road = walker.Position.Y;
            Grounds.Run(walker, new MoveInput(new Vector2(0f, 1f), Run: true, Jump: true), 10f, Ground);
            Grounds.Run(walker, MoveInput.None, 2f, Ground);
            // No further than the foot of the wall, and no higher than the cell over the strip's edge takes you up it
            Assert.True(OffTheRoad(new Vector2(walker.Position.X, walker.Position.Z)) <= 0.5f, $"got off the road, to {walker.Position}");
            Assert.True(walker.Position.Y - road < 1.2f, $"got up to {walker.Position.Y}, from the road at {road}");
        }

        [Fact]
        public void TheLakesHoldWater()
        {
            var terrain = Pass;
            foreach (var lake in Lakes)
                foreach (var (centre, radius) in lake.Circles)
                {
                    Assert.Equal(lake.Level, terrain.WaterLevelAt(centre.X, centre.Y));
                    Assert.True(lake.Level - terrain.HeightAt(centre.X, centre.Y) > 1f, $"the {lake.Name} is too shallow to swim at {centre}");
                }
            Assert.Equal(TownLakeLevel, terrain.WaterLevelAt(TownLakeCentre.X, TownLakeCentre.Y));
            Assert.Null(terrain.WaterLevelAt(TownLakeCentre.X, TownLakeCentre.Y - TownLakeRadius - TownLakeShore - 10f));
        }

        // From the shelf above the lake, turning off the road towards it: over the edge and into the water
        [Fact]
        public void WalkingOffTheShelfDropsYouIntoTheLake()
        {
            var walker = StandAt("lake");
            walker.Yaw = MathHelper.Pi;   // south, towards it
            var swam = false;
            Grounds.Run(walker, Grounds.Forward(), 15f, Ground, w => swam |= w.Swimming);
            Assert.True(swam, $"never got into the lake: stopped at {walker.Position}");
        }
    }
}
