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
