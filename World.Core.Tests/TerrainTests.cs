using Microsoft.Xna.Framework;
using System;
using Xunit;

namespace World.Core.Tests
{
    public class TerrainTests
    {
        private static readonly Terrain Bumpy = Terrain.FromFunction(16, 12, 2f, (x, z) => 0.3f * x + 0.1f * z * z - MathF.Sin(x));

        [Fact]
        public void HeightAtACornerIsThatCornersHeight()
        {
            for (var j = 0; j <= Bumpy.Depth; j++)
                for (var i = 0; i <= Bumpy.Width; i++)
                {
                    var corner = Bumpy.Corner(i, j);
                    Assert.Equal(Bumpy.CornerHeight(i, j), Bumpy.HeightAt(corner.X, corner.Z), 4);
                }
        }

        [Fact]
        public void HeightIsLinearAlongEveryTriangleEdge()
        {
            for (var j = 0; j < Bumpy.Depth; j++)
                for (var i = 0; i < Bumpy.Width; i++)
                    foreach (var southWest in new[] { false, true })
                    {
                        var (a, b, c) = Bumpy.Triangle(i, j, southWest);
                        foreach (var (p, q) in new[] { (a, b), (b, c), (c, a) })
                        {
                            var mid = (p + q) / 2f;
                            Assert.Equal(mid.Y, Bumpy.HeightAt(mid.X, mid.Z), 4);
                        }
                    }
        }

        [Fact]
        public void NormalOfAPlaneIsThePlanesNormal()
        {
            var plane = Terrain.FromFunction(8, 8, 1f, (x, z) => 0.5f * x - 0.25f * z);
            var expected = Vector3.Normalize(new Vector3(-0.5f, 1f, 0.25f));
            foreach (var (x, z) in new[] { (0.2f, 0.7f), (0.7f, 0.2f), (-2.5f, 1.1f) })
            {
                var n = plane.NormalAt(x, z);
                Assert.Equal(expected.X, n.X, 4);
                Assert.Equal(expected.Y, n.Y, 4);
                Assert.Equal(expected.Z, n.Z, 4);
            }
        }

        [Theory]
        [InlineData(30f, true)]
        [InlineData(44f, true)]
        [InlineData(46f, false)]
        [InlineData(60f, false)]
        public void SlopesSteeperThan45DegreesAreNotWalkable(float degrees, bool walkable)
        {
            var slope = Grounds.SlopeFrom(degrees);
            Assert.Equal(walkable, slope.IsWalkable(3.5f, 0.5f));
            Assert.True(slope.IsWalkable(-3.5f, 0.5f));   // the level ground before it
        }

        [Fact]
        public void WorkedOutAChunkAtATimeItMatchesTheFunctionEverywhere()
        {
            // Sizes that aren't whole chunks, so the last chunks each way are narrower
            static float Height(float x, float z) => MathF.Sin(x * 0.3f) * 2f + z * 0.1f;
            var terrain = Terrain.FromFunction(70, 50, 0.5f, Height);
            Assert.Equal(3, terrain.ChunksX);
            Assert.Equal(2, terrain.ChunksZ);
            for (var j = 0; j <= terrain.Depth; j++)
                for (var i = 0; i <= terrain.Width; i++)
                    Assert.Equal(Height(terrain.OriginX + i * 0.5f, terrain.OriginZ + j * 0.5f), terrain.CornerHeight(i, j));
        }

        [Fact]
        public void OnlyTheChunksAskedAboutAreWorkedOut()
        {
            var calls = 0;
            var terrain = Terrain.FromFunction(1024, 1024, 1f, (x, z) => { calls++; return 0f; });
            Assert.Equal(0, terrain.ChunksMade);
            terrain.HeightAt(5f, 5f);
            terrain.HeightAt(6f, 7f);
            Assert.Equal(1, terrain.ChunksMade);
            Assert.Equal((Terrain.ChunkCells + 1) * (Terrain.ChunkCells + 1), calls);
        }

        [Fact]
        public void AChunksBoundsHoldItsGround()
        {
            var terrain = Terrain.FromFunction(64, 64, 1f, (x, z) => x * 0.5f);
            var bounds = terrain.ChunkBounds(1, 0);   // x from 0 to 32
            Assert.Equal(new Vector3(0f, 0f, -32f), bounds.Min);
            Assert.Equal(new Vector3(32f, 16f, 0f), bounds.Max);
        }

        [Fact]
        public void TheWorldIsAKilometreAcrossButStartingOutWorksOutLittleOfIt()
        {
            var world = TerrainGenerator.Create();
            Assert.Equal(1024f, world.Width * world.CellSize);
            world.HeightAt(0f, 0f);
            Assert.True(world.ChunksMade <= 4, $"{world.ChunksMade} chunks");

            // And far out, the country's bigger than at home
            float Range(float fromX, float fromZ)
            {
                float low = float.MaxValue, high = float.MinValue;
                for (var z = fromZ; z < fromZ + 150f; z += 5f)
                    for (var x = fromX; x < fromX + 150f; x += 5f)
                    {
                        low = MathF.Min(low, world.HeightAt(x, z));
                        high = MathF.Max(high, world.HeightAt(x, z));
                    }
                return high - low;
            }
            Assert.True(Range(300f, 300f) > 2f * Range(-60f, -10f));
        }

        [Fact]
        public void PadsCanBeLevelledWithEachOtherAndRaisedOrDug()
        {
            // Out on open ground, clear of the plateau, the lake and the pond: a road pad, a garden 0.6 m above
            // it, and a pool dug 1 m into the garden, all level with the road
            var road = new Vector2(60f, -60f);
            var terrain = TerrainGenerator.Create(pads: new[]
            {
                new TerrainGenerator.Pad(road + new Vector2(0f, 12f), new Vector2(4f, 4f), Raise: 0.6f, LevelWith: road),
                new TerrainGenerator.Pad(road + new Vector2(0f, 13f), new Vector2(1f, 1f), Apron: 0f, Blend: 0f, Raise: -0.4f, LevelWith: road),
                new TerrainGenerator.Pad(road, new Vector2(10f, 3f)),
            });
            float At(float dx, float dz) => terrain.HeightAt(road.X + dx, road.Y + dz);
            var level = At(0f, 0f);
            Assert.Equal(level + 0.6f, At(-2f, 15f), 3);    // the garden, clear of the road's slope
            Assert.Equal(level - 0.4f, At(0f, 13f), 3);     // the pool's floor
            Assert.Equal(level + 0.6f, At(0f, 15.5f), 3);   // a cell past its edge, the garden again: dug straight down, within a cell
            Assert.InRange(At(0f, 5.5f), level + 0.01f, level + 0.59f);   // the front garden, sloping down to the road
        }

        [Fact]
        public void APadCanSlopeAndPadsLaidEndToEndMeetCleanly()
        {
            // A road down a hillside, clear of the plateau, the lake and the pond: a level square at each end, the
            // far one raised 2 m, and a strip sloping between them. The squares come after the strip, so their
            // blends would cut into it if they could.
            var near = new Vector2(80f, -80f);
            var far = new Vector2(80f, -40f);
            var terrain = TerrainGenerator.Create(pads: new[]
            {
                new TerrainGenerator.Pad(new Vector2(80f, -60f), new Vector2(3f, 15f), Apron: 0.5f, LevelWith: near,
                    Slope: new TerrainGenerator.PadSlope(new Vector2(80f, -75f), new Vector2(80f, -45f), ToLevelWith: far, ToRaise: 2f)),
                new TerrainGenerator.Pad(near, new Vector2(5f, 5f), Apron: 0.5f),
                new TerrainGenerator.Pad(far, new Vector2(5f, 5f), Apron: 0.5f, Raise: 2f),
            });
            var low = terrain.HeightAt(near.X, near.Y);
            var high = terrain.HeightAt(far.X, far.Y);
            Assert.Equal(low + (high - low) * 0.5f, terrain.HeightAt(80f, -60f), 3);            // halfway up
            Assert.Equal(low + (high - low) * (28f / 30f), terrain.HeightAt(80f, -47f), 3);     // beside the far square: still on the slope
            Assert.Equal(low + (high - low) * (28f / 30f), terrain.HeightAt(82f, -47f), 3);     // level across it
        }

        [Fact]
        public void OffTheEdgeIsNotContained()
        {
            var flat = Grounds.Flat();
            Assert.True(flat.Contains(31.9f, -31.9f));
            Assert.False(flat.Contains(32.1f, 0f));
            Assert.Null(((IGround)flat).GroundBelow(new Vector3(0f, 0f, 40f), 1f));
        }

        [Fact]
        public void GeneratorIsDeterministicAndBuildsItsLandmarks()
        {
            var a = TerrainGenerator.Create(seed: 7);
            var b = TerrainGenerator.Create(seed: 7);
            for (var j = 0; j <= a.Depth; j += 9)
                for (var i = 0; i <= a.Width; i += 9)
                    Assert.Equal(a.CornerHeight(i, j), b.CornerHeight(i, j));

            var centre = TerrainGenerator.PlateauCentre;
            Assert.Equal(TerrainGenerator.PlateauHeight, a.HeightAt(centre.X, centre.Y), 3);
            Assert.True(a.HeightAt(TerrainGenerator.BasinCentre.X, TerrainGenerator.BasinCentre.Y) < TerrainGenerator.WaterLevel);

            // Sheer on its north side, where there's no causeway
            var rim = centre.Y - TerrainGenerator.PlateauRadius;
            Assert.False(a.IsWalkable(centre.X, rim - 0.5f));
        }
    }
}
