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
            foreach (var name in new[] { "trailhead", "firstpass", "lake", "secondpass", "town", "ford", "jump" })
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

        // The road's pieces, one after another, from the trailhead into the basin, with no gaps between them but the
        // jump's pit: none of it over the pit, and stopping square at its lip and starting again at the wall's top
        [Fact]
        public void TheRoadIsLaidAllTheWayFromTheTrailheadToTheBasinButOverThePit()
        {
            var pieces = Road.Pieces;
            Assert.Equal(0, pieces[0].from);
            Assert.Equal(Road.Path.Count - 1, pieces[^1].to);
            var gaps = 0;
            for (var k = 1; k < pieces.Count; k++)
                if (pieces[k - 1].to != pieces[k].from)
                {
                    gaps++;
                    Assert.Equal(Crossings.JumpAt, Road.Path[pieces[k - 1].to].s);
                    Assert.Equal(Crossings.JumpFar, Road.Path[pieces[k].from].s);
                }
            Assert.Equal(1, gaps);
            Assert.DoesNotContain(Road.Path, p => Crossings.InThePit(p.s));
            Assert.Equal(PassRoute.Length, Road.Path[^1].s);
            var end = PassRoute.Points[^1];
            Assert.True(Basin.Outside(end.X, end.Y) < 0f, "the road stops short of the basin");
            Assert.Equal(pieces.Count, new Road().Fixtures(Built.Value.Terrain).Count());   // a fixture each (the map has others: the robots)
        }

        // Every metre along it: the ground's the road's surface (its height, but down through the ford, up the ramp and
        // down into the pit: see Crossings), and level across it (clear of the cells over its edge, and of the lip and
        // the wall, where it's sheer)
        [Fact]
        public void TheGroundAlongTheRoadIsItsSurfaceAndLevelAcrossIt()
        {
            var terrain = Pass;
            for (var s = 0f; s < PassRoute.Length; s += 1f)
            {
                if (MathF.Abs(s - Crossings.JumpAt) < 1.5f || MathF.Abs(s - Crossings.JumpFar) < 1.5f)
                    continue;
                var (at, _, heading) = PassRoute.At(s);
                var surface = Crossings.Surface(s);
                var across = new Vector2(-heading.Y, heading.X);
                foreach (var offset in new[] { -3.5f, 0f, 3.5f })
                {
                    var p = at + across * offset;
                    Assert.True(MathF.Abs(terrain.HeightAt(p.X, p.Y) - surface) < 0.1f,
                                $"the ground's at {terrain.HeightAt(p.X, p.Y):F2}, not {surface:F2}, {offset} m across the road {s} m along");
                }
            }
        }

        // Every triangle of ground a walker could reach from `from` without ever standing on one too steep to walk: all on
        // the road's strip (or a cell over its edge), at the trailhead or in the basin. How far along the road that goes,
        // and whether it gets to the town's lake.
        private static (float furthest, bool reachedTheTown) OnFootFrom(Vector2 from)
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

            int i0 = (int)((from.X - terrain.OriginX) / terrain.CellSize), j0 = (int)((from.Y - terrain.OriginZ) / terrain.CellSize);
            Visit(i0, j0, false);
            var reachedTheTown = false;
            var furthest = 0f;
            while (open.Count > 0)
            {
                var (i, j, southWest) = open.Pop();
                var (a, b, c) = terrain.Triangle(i, j, southWest);
                var middle = new Vector2(a.X + b.X + c.X, a.Z + b.Z + c.Z) / 3f;
                Assert.True(OffTheRoad(middle) <= 1.5f || InAFlat(middle, 1.5f), $"walkable ground off the road at {middle}");
                reachedTheTown |= Vector2.Distance(middle, TownLakeCentre) < TownLakeRadius + TownLakeShore;
                if (OffTheRoad(middle) <= 1.5f)
                    furthest = MathF.Max(furthest, PassRoute.Nearest(middle).s);
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
            return (furthest, reachedTheTown);
        }

        // On foot from the trailhead, the road's the only way to go, and it goes no further than the jump's pit: its
        // wall's too steep to climb, so the town can't be walked to
        [Fact]
        public void OnFootTheRoadIsTheOnlyWayAndTheJumpsPitStopsIt()
        {
            var (furthest, reachedTheTown) = OnFootFrom(PassRoute.Controls[PassRoute.Trailhead].at);
            Assert.False(reachedTheTown, "the town can be walked to");
            Assert.True(furthest > Crossings.JumpAt && furthest < Crossings.JumpFar, $"on foot as far as {furthest:F0} m along");
        }

        // From the top of the pit's wall, though, the road goes on all the way to the town
        [Fact]
        public void PastTheJumpTheRoadGoesOnToTheTown()
        {
            var (_, reachedTheTown) = OnFootFrom(PassRoute.At(Crossings.JumpFar + 5f).at);
            Assert.True(reachedTheTown, "the road doesn't lead to the town's lake");
        }

        // Running along the road (steering for a point a little ahead on it), from `from` metres along, for `seconds`:
        // how far along it you ever were, on it, and where you ended up
        private static (float furthest, CharacterController walker) RunTheRoad(float from, float seconds, bool jumping = false)
        {
            var (at, _, heading) = PassRoute.At(from);
            var walker = new CharacterController(new Vector3(at.X, Crossings.Surface(from) + 0.5f, at.Y), MathF.Atan2(heading.X, -heading.Y));
            walker.SnapToGround(Ground);
            var running = new MoveInput(new Vector2(0f, 1f), Run: true, Jump: jumping);
            var furthest = from;
            for (var t = 0f; t < seconds; t += Grounds.Tick)
            {
                var here = new Vector2(walker.Position.X, walker.Position.Z);
                var (s, distance, _) = PassRoute.Nearest(here);
                if (distance <= PassRoute.HalfWidth && MathF.Abs(walker.Position.Y - Crossings.Surface(s)) < 0.5f)
                    furthest = MathF.Max(furthest, s);
                var ahead = PassRoute.At(s + 6f).at - here;
                walker.Yaw = MathF.Atan2(ahead.X, -ahead.Y);
                walker.Step(running, Grounds.Tick, Ground);
            }
            return (furthest, walker);
        }

        // Running at the ford: the river carries you off the road before you're across, over the edge and down into the lake
        [Fact]
        public void RunningIntoTheFordTheRiverCarriesYouOffIntoTheLake()
        {
            var (furthest, walker) = RunTheRoad(Crossings.FordAt - 40f, 30f);
            Assert.True(furthest < Crossings.FordAt + Crossings.BedHalf, $"got across, to {furthest:F0} m along");
            Assert.True(walker.Position.Y < CliffLake.Level + 2f, $"ended up at {walker.Position}, not down at the lake");
        }

        // From the lake, the stair (see Crossings.Stair): swimming to its foot, up it, and off the landing onto the road,
        // before the ford
        [Fact]
        public void FromTheLakeTheStairClimbsBackToTheRoad()
        {
            var stair = Crossings.Stair;
            var foot = (stair[0].inner + stair[0].outer) / 2f;
            var (_, _, heading) = PassRoute.At(Crossings.StairFrom);
            var offFoot = foot - heading * 4f + new Vector2(-heading.Y, heading.X) * 3f;   // out in the lake, off the stair's foot
            var walker = new CharacterController(new Vector3(offFoot.X, CliffLake.Level + 1f, offFoot.Y));
            Grounds.Run(walker, MoveInput.None, 2f, Ground);
            Assert.True(walker.Swimming);
            for (var t = 0f; t < 40f; t += Grounds.Tick)
            {
                var here = new Vector2(walker.Position.X, walker.Position.Z);
                var (s, distance, _) = PassRoute.Nearest(here);
                if (distance < PassRoute.HalfWidth - 1f && MathF.Abs(walker.Position.Y - Crossings.Surface(s)) < 0.1f)
                {
                    Assert.True(s < Crossings.FordAt - Crossings.BedHalf - Crossings.Bank, $"back on the road {s:F0} m along");
                    return;
                }
                Vector2 to;
                if (walker.Position.Y > stair[^1].top - 0.05f)
                    to = PassRoute.At(s).at;   // on the landing: onto the road
                else if (walker.Swimming || walker.Position.Y < stair[0].top - 0.01f)
                    to = foot;
                else
                {
                    var (at, _, h) = PassRoute.At(s + 1.5f);
                    to = at + new Vector2(-h.Y, h.X) * (Crossings.StairInner + Crossings.StairOuter) / 2f;   // up the stair
                }
                var way = to - here;
                walker.Yaw = MathF.Atan2(way.X, -way.Y);
                walker.Step(Grounds.Forward(), Grounds.Tick, Ground);
            }
            Assert.Fail($"never got back onto the road: at {walker.Position}");
        }

        // Running at the jump and jumping all the way: up the ramp, off the lip and down into the pit, short of the wall
        [Fact]
        public void OnFootYouCantJumpThePit()
        {
            var (furthest, walker) = RunTheRoad(Crossings.JumpAt - 40f, 30f, jumping: true);
            Assert.True(furthest < Crossings.JumpFar + 1f, $"got over, to {furthest:F0} m along");   // no further than the wall's foot
            var s = PassRoute.Nearest(new Vector2(walker.Position.X, walker.Position.Z)).s;
            Assert.True(s > Crossings.JumpAt && s < Crossings.JumpFar, $"ended up {s:F0} m along, not in the pit");
        }

        // Down in the pit, you can walk back out, up its floor and over the lip, the way you came
        [Fact]
        public void YouCanWalkBackOutOfThePit()
        {
            var (at, _, heading) = PassRoute.At(Crossings.JumpFar - 2f);
            var walker = new CharacterController(new Vector3(at.X, Crossings.Surface(Crossings.JumpFar - 2f) + 0.5f, at.Y), MathF.Atan2(-heading.X, heading.Y));
            walker.SnapToGround(Ground);
            for (var t = 0f; t < 15f; t += Grounds.Tick)
            {
                var here = new Vector2(walker.Position.X, walker.Position.Z);
                var back = PassRoute.At(PassRoute.Nearest(here).s - 6f).at - here;
                walker.Yaw = MathF.Atan2(back.X, -back.Y);
                walker.Step(Grounds.Forward(), Grounds.Tick, Ground);
            }
            var s = PassRoute.Nearest(new Vector2(walker.Position.X, walker.Position.Z)).s;
            Assert.True(s < Crossings.JumpAt - Crossings.RampLength, $"only got back to {s:F0} m along");
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
