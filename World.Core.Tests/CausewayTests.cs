using Maps.Home;
using Microsoft.Xna.Framework;
using System;
using World.Core.Characters;
using World.Core.Physics;
using World.Maps;
using Xunit;

namespace World.Core.Tests
{
    // The causeway up the home map's plateau (see TerrainGenerator): an even climb all the way to the top, and up it a
    // box can be pushed, by the walker or a droid on any of its bases.
    public class CausewayTests
    {
        private static readonly float Line = TerrainGenerator.PlateauCentre.Y;   // its middle
        private static readonly float Rim = TerrainGenerator.PlateauCentre.X - TerrainGenerator.PlateauRadius;

        [Fact]
        public void ItClimbsEvenlyAllTheWayToTheTop()
        {
            var terrain = WorldBuilder.Build(HomeMap.Map).Terrain;
            for (var x = Rim - 12f; x < Rim + 1f; x += 0.25f)
            {
                var rise = (terrain.HeightAt(x + 0.25f, Line) - terrain.HeightAt(x, Line)) / 0.25f;
                // Nowhere so steep a box slides back down it by itself (see PhysicsWorld.Friction): about 27 degrees
                Assert.True(rise < PhysicsWorld.Friction, $"a step up at x = {x}: {rise:F2} m a metre");
            }
            Assert.Equal(TerrainGenerator.PlateauHeight, terrain.HeightAt(Rim + 0.5f, Line), 2);
        }

        public static TheoryData<Locomotion?> Pushers() => new TheoryData<Locomotion?> { null, Locomotion.Segway, Locomotion.TriStar, Locomotion.Tracks };

        [Theory]
        [MemberData(nameof(Pushers))]
        public void ASmallBoxCanBePushedUpItOntoThePlateau(Locomotion? on)
        {
            var world = WorldBuilder.Build(HomeMap.Map);
            var (x, z) = (Rim - 8f, Line + 0.2f);   // two-thirds of the way up, a little off its middle
            var box = world.Physics.Add(new Body("small box", new Vector3(0.5f), 4f, new Vector3(x, world.Terrain.HeightAt(x + 0.25f, z) + 0.02f, z)));
            var pusher = new Player(new Vector3(x - 1.2f, world.Terrain.HeightAt(x - 1.2f, Line) + 0.3f, Line), MathHelper.PiOver2, world.Physics);
            if (on is { } locomotion)
                pusher.Body.Gait = Locomotions.GaitOf(locomotion);
            for (var t = 0; t < 10f / Grounds.Tick && box.Position.X < Rim + 3f; t++)
            {
                pusher.Step(Grounds.Forward(), Grounds.Tick, world.Physics);
                world.Physics.Step(Grounds.Tick);
            }
            Assert.True(box.Position.X >= Rim + 3f, $"it got stuck at {box.Position}, the pusher at {pusher.Body.Position}");
            Assert.Equal(TerrainGenerator.PlateauHeight, box.Bottom, 2);
        }
    }
}
