using Maps.Coast;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using World.Core.Movement;
using World.Maps;
using Xunit;
using static Maps.Coast.CoastTerrain;

namespace World.Core.Tests
{
    public class CoastTests
    {
        // The whole map: its terrain with the railway's ground levelled into it, and the ground a walker's on - that,
        // the bridge's walls and the platform's blocks and all
        private static readonly Lazy<BuiltWorld> Built = new Lazy<BuiltWorld>(() => WorldBuilder.Build(CoastMap.Map));
        private static Terrain Coast => Built.Value.Terrain;
        private static IGround Ground => Built.Value.Physics;
        private static Start StartAt(string name) => Built.Value.Starts[name];
        private static readonly float Half = Size * CellSize / 2f;

        // Every metre from the north edge of the world to the south
        private static IEnumerable<float> AllAlong()
        {
            for (var z = -Half + 0.5f; z < Half; z += 1f)
                yield return z;
        }

        private static CharacterController StandOn(IGround ground, Vector2 at, float yaw)
        {
            var walker = new CharacterController(new Vector3(at.X, 100f, at.Y), yaw);
            walker.SnapToGround(ground);
            return walker;
        }

        private static int Column(Terrain terrain, float x) => (int)((x - terrain.OriginX) / terrain.CellSize);

        private static IEnumerable<(int i, int j, bool southWest)> WholeColumn(Terrain terrain, int i)
        {
            for (var j = 0; j < terrain.Depth; j++)
            {
                yield return (i, j, false);
                yield return (i, j, true);
            }
        }

        // Every triangle of columns i0 to i1 (not including i1) a walker could reach from `from` without ever standing
        // on one too steep to walk, crossing from one to the next over the edge they share.
        private static List<(Vector3 a, Vector3 b, Vector3 c)> Reachable(Terrain terrain, int i0, int i1,
                                                                          IEnumerable<(int i, int j, bool southWest)> from)
        {
            var seen = new HashSet<(int i, int j, bool southWest)>();
            var open = new Stack<(int i, int j, bool southWest)>();
            var reached = new List<(Vector3, Vector3, Vector3)>();

            void Visit(int i, int j, bool southWest)
            {
                if (i < i0 || i >= i1 || j < 0 || j >= terrain.Depth || !seen.Add((i, j, southWest)))
                    return;
                if (Terrain.IsWalkableNormal(terrain.TriangleNormal(i, j, southWest)))
                    open.Push((i, j, southWest));
            }

            foreach (var (i, j, southWest) in from)
                Visit(i, j, southWest);
            while (open.Count > 0)
            {
                var (i, j, southWest) = open.Pop();
                reached.Add(terrain.Triangle(i, j, southWest));
                // Across each of its edges into the triangle on the other side (see Terrain.Triangle)
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
            return reached;
        }

        [Fact]
        public void BuildsWithEveryStart()
        {
            var built = WorldBuilder.Build(CoastMap.Map);
            Assert.Contains(CoastMap.DefaultStart, built.Starts.Keys);
            foreach (var name in new[] { "station", "hall", "river", "clifftop", "beach", "bridge", "coastline" })
                Assert.Contains(name, built.Starts.Keys);
        }

        [Fact]
        public void ThePlateauIsFlatAtTheLandsHeight()
        {
            var bare = Create();   // without the railway's formation levelled into it
            // Not quite out to its edges, where the cells take in some of the rolling land beyond
            for (var dx = -0.95f; dx <= 0.95f; dx += 0.19f)
                for (var dz = -0.95f; dz <= 0.95f; dz += 0.19f)
                {
                    var x = PlateauCentre.X + dx * PlateauHalf.X;
                    var z = PlateauCentre.Y + dz * PlateauHalf.Y;
                    Assert.Equal(LandHeight, bare.HeightAt(x, z), 3);
                    Assert.Equal(Vector3.Up, bare.NormalAt(x, z));
                }
        }

        [Fact]
        public void TheRiverRunsTheWholeWidthOfTheWorldAndStaysInItsChannel()
        {
            var terrain = Coast;
            foreach (var z in AllAlong())
            {
                var middle = RiverAt(z);
                Assert.Equal(RiverLevel, terrain.WaterLevelAt(middle, z));
                Assert.True(RiverLevel - terrain.HeightAt(middle, z) > 2f, $"too shallow to swim at z = {z}");
                // Dry up its gorge's walls, and the land high above it both sides
                Assert.Null(terrain.WaterLevelAt(middle - RiverHalfWidth - 1f, z));
                Assert.Null(terrain.WaterLevelAt(middle + RiverHalfWidth + 1f, z));
                Assert.True(terrain.HeightAt(middle + GorgeHalfWidth, z) > RiverLevel + 6f);
                Assert.True(terrain.HeightAt(middle - GorgeHalfWidth, z) > RiverLevel + 6f);
            }
        }

        // Every triangle of ground a walker could reach from the land west of the gorge without ever standing on one
        // too steep to walk: none of them reaches the river, let alone the far side of it.
        [Fact]
        public void NoWalkableGroundLeadsAcrossTheRiver()
        {
            var terrain = Coast;
            var i0 = Column(terrain, RiverX - RiverWander - GorgeHalfWidth - 10f);
            var i1 = Column(terrain, RiverX + RiverWander + GorgeHalfWidth + 10f);
            var reached = Reachable(terrain, i0, i1, WholeColumn(terrain, i0));
            foreach (var (a, b, c) in reached)
                foreach (var corner in new[] { a, b, c })
                    Assert.True(corner.X < RiverAt(corner.Z) - RiverHalfWidth, $"walkable ground from the west reaches the water at {corner}");
        }

        // And from the water: nothing walkable leads more than a metre or so up the gorge's walls (where a cell takes
        // in a little of one), on either side.
        [Fact]
        public void NothingWalkableLeadsOutOfTheRiver()
        {
            var terrain = Coast;
            var i0 = Column(terrain, RiverX - RiverWander - GorgeHalfWidth - 10f);
            var i1 = Column(terrain, RiverX + RiverWander + GorgeHalfWidth + 10f);
            var riverbed = new List<(int, int, bool)>();
            for (var j = 0; j < terrain.Depth; j++)
            {
                var middle = Column(terrain, RiverAt(terrain.OriginZ + (j + 0.5f) * CellSize));
                riverbed.Add((middle, j, false));
                riverbed.Add((middle, j, true));
            }
            var highest = float.MinValue;
            foreach (var (a, b, c) in Reachable(terrain, i0, i1, riverbed))
                highest = MathF.Max(highest, MathF.Max(a.Y, MathF.Max(b.Y, c.Y)));
            Assert.True(highest <= RiverLevel + 2f, $"walkable ground reaches {highest - RiverLevel} m up out of the river");
        }

        // Running and jumping at it the whole time: over the edge, into the water, across, and at the far wall, where
        // it can hop about in the shallows at the wall's foot but gets no higher.
        [Fact]
        public void RunningAndJumpingAtTheGorgeOnlyGetsYouIntoTheRiver()
        {
            var start = StartAt("river");
            var walker = StandOn(Ground, start.At, start.Yaw);
            var jumpEveryTick = new MoveInput(new Vector2(0f, 1f), Run: true, Jump: true);
            var swam = false;
            var highestAfter = float.MinValue;
            Grounds.Run(walker, jumpEveryTick, 20f, Ground, w =>
            {
                swam |= w.Swimming;
                if (swam)
                    highestAfter = MathF.Max(highestAfter, w.Position.Y);
            });

            Assert.True(swam, "never got into the river");
            Assert.True(highestAfter < RiverLevel + 2f, $"got {highestAfter - RiverLevel} m up out of the river");
            Assert.True(walker.Position.X < RiverAt(walker.Position.Z) + GorgeHalfWidth, $"got out on the far side, at x = {walker.Position.X}");
        }

        [Fact]
        public void TheBeachIsDrySandAndTheSeaGoesOnPastTheEdgeOfTheWorld()
        {
            var terrain = Coast;
            var sea = terrain.Pools[^1];
            foreach (var z in AllAlong())
            {
                var foot = CliffAt(z) + CliffFace;
                var beach = (foot + ShoreAt(z)) / 2f;
                Assert.Null(terrain.WaterLevelAt(beach, z));
                // A cell and a half out from the cliff's foot, clear of its face's corners
                Assert.True(sea.Covers(foot + 1.5f, z) && terrain.HeightAt(foot + 1.5f, z) < sea.Level + sea.Shore, $"no sand at the cliff's foot at z = {z}");
                Assert.Equal(0f, terrain.WaterLevelAt(ShoreAt(z) + 5f, z));
                Assert.Equal(0f, terrain.WaterLevelAt(Half + 150f, z));
            }
        }

        // Every triangle of ground a walker could reach from the beach without ever standing on one too steep to
        // walk: none of them is more than a metre or so up the cliff's foot (where a cell takes in a little of the
        // face), let alone near its top. So there's no way up it, and no way down it but to fall, anywhere along it.
        [Fact]
        public void NoWalkableGroundLeadsUpTheCliffAnywhere()
        {
            var terrain = Coast;
            var i0 = Column(terrain, CliffX - 30f);
            var i1 = Column(terrain, CliffX + 40f);
            // From the easternmost column of the band: the beach, clear of the cliff's foot everywhere
            var highest = float.MinValue;
            foreach (var (a, b, c) in Reachable(terrain, i0, i1, WholeColumn(terrain, i1 - 1)))
                highest = MathF.Max(highest, MathF.Max(a.Y, MathF.Max(b.Y, c.Y)));

            Assert.True(highest <= CliffFoot + 2f, $"walkable ground reaches {highest} m up from the beach");
        }

        [Fact]
        public void WalkingOffTheClifftopFallsToTheBeach()
        {
            var start = StartAt("clifftop");
            var walker = StandOn(Ground, start.At, start.Yaw);
            Assert.True(walker.Position.Y > LandHeight - Rolling);

            var wasAirborne = false;
            Grounds.Run(walker, Grounds.Forward(), 8f, Ground, w => wasAirborne |= !w.Grounded);

            Assert.True(wasAirborne);
            Assert.True(walker.Grounded);
            Assert.True(walker.Position.Y <= CliffFoot, $"still up at {walker.Position.Y} m");
        }

        [Fact]
        public void WalkingIntoTheCliffFromTheBeachGetsNoHigher()
        {
            var start = StartAt("beach");
            var walker = StandOn(Ground, start.At, start.Yaw);
            Grounds.Run(walker, Grounds.Forward(run: true), 10f, Ground);
            Assert.True(walker.Position.Y <= CliffFoot + 0.1f, $"climbed to {walker.Position.Y} m");
            Assert.True(walker.Position.X >= CliffAt(walker.Position.Z), "got past the cliff's top edge");
        }
    }
}
