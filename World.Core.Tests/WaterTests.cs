using Microsoft.Xna.Framework;
using System;
using World.Core.Characters;
using World.Core.Movement;
using World.Core.Physics;
using Xunit;

namespace World.Core.Tests
{
    // Wading, swimming, getting wet and drying off, and floating (see Pool, CharacterController, PhysicsWorld).
    public class WaterTests
    {
        // Flat ground under water `level` deep, everywhere within 20 m of the origin
        private static Terrain Flooded(float level) => Grounds.Flat().Flood(new Pool(Vector2.Zero, 20f, level));

        private static CharacterController Walker(IGround ground, float x, float z, float yaw = Grounds.East)
        {
            var walker = new CharacterController(new Vector3(x, 0f, z), yaw);
            walker.SnapToGround(ground);
            return walker;
        }

        [Fact]
        public void WaterIsOnlyWhereTheGroundIsBelowIt()
        {
            var ground = Grounds.SlopeFrom(10f).Flood(new Pool(Vector2.Zero, 30f, 1f));
            Assert.Equal(1f, ((IGround)ground).WaterAt(new Vector3(-5f, 0f, 0f)));
            Assert.Null(((IGround)ground).WaterAt(new Vector3(10f, 0f, 0f)));   // the slope's risen out of it
            Assert.Null(((IGround)ground).WaterAt(new Vector3(-5f, 0f, 31f)));  // outside the pool
        }

        [Fact]
        public void WadingSlowsYouDown()
        {
            var ground = Flooded(0.8f);
            var walker = Walker(ground, -10f, 0f);
            Grounds.Run(walker, Grounds.Forward(), 2f, ground);
            Assert.True(walker.Grounded);
            Assert.False(walker.Swimming);
            var expected = CharacterController.WalkSpeed * (1f - CharacterController.WadeSlowing * 0.8f / CharacterController.SwimDepth);
            Assert.Equal(expected, walker.Velocity.Length(), 2);
        }

        [Fact]
        public void DeepWaterHoldsYouUpAndYouSwim()
        {
            var ground = Flooded(3f);
            var walker = Walker(ground, -10f, 0f);
            Grounds.Run(walker, Grounds.Forward(), 3f, ground);
            Assert.True(walker.Swimming);
            Assert.False(walker.Grounded);
            Assert.Equal(3f - CharacterController.SwimDepth, walker.Position.Y, 3);
            Assert.Equal(CharacterController.SwimSpeed, walker.Velocity.Length(), 2);
        }

        [Fact]
        public void YouSwimToTheShoreAndWalkOut()
        {
            // Level ground under 2 m of water for x < 0, rising east out of it at 10 degrees
            var ground = Grounds.SlopeFrom(10f).Flood(new Pool(Vector2.Zero, 60f, 2f));
            var walker = new CharacterController(new Vector3(-5f, 0f, 0f), Grounds.East);
            Grounds.Run(walker, MoveInput.None, 1f, ground);
            Assert.True(walker.Swimming);

            Grounds.Run(walker, Grounds.Forward(), 15f, ground);
            Assert.False(walker.Swimming);
            Assert.True(walker.Grounded);
            Assert.True(walker.Position.Y > 2f, $"still in the water at {walker.Position}");
        }

        [Fact]
        public void JumpingInYouStopAtSwimmingDepth()
        {
            var ground = Grounds.PlateauEdge(4f).Flood(new Pool(new Vector2(10f, 0f), 11f, 2.5f));   // a pool right up to the foot of a 4 m drop
            var walker = Walker(ground, -1f, 0f);
            var lowest = float.MaxValue;
            Grounds.Run(walker, Grounds.Forward(run: true), 3f, ground, w => lowest = MathF.Min(lowest, w.Position.Y));
            Assert.True(walker.Swimming);
            Assert.Equal(2.5f - CharacterController.SwimDepth, lowest, 3);
        }

        [Fact]
        public void YouGetWetAndDryOff()
        {
            var ground = Flooded(0.9f);
            var player = new Player(new Vector3(0f, 0f, 0f), Grounds.East, ground);
            for (var t = 0; t < 10; t++)
                player.Step(MoveInput.None, Grounds.Tick, ground);
            Assert.Equal(0.9f / Player.Height, player.Wetness, 3);

            // Out on dry land, it goes, a little at a time
            var dry = Grounds.Flat();
            player.Body.Position = new Vector3(0f, 0f, 0f);
            for (var t = 0; t < 600; t++)
                player.Step(MoveInput.None, Grounds.Tick, dry);
            Assert.Equal(0.9f / Player.Height - 10f / Player.DryingTime, player.Wetness, 2);
            for (var t = 0; t < 60 * 100; t++)
                player.Step(MoveInput.None, Grounds.Tick, dry);
            Assert.Equal(0f, player.Wetness);
        }

        [Fact]
        public void ALightCrateFloatsAsDeepAsItsDensitySays()
        {
            var world = new PhysicsWorld(Flooded(2f));
            var crate = world.Add(new Body("crate", new Vector3(1f, 1f, 1f), 300f, new Vector3(0f, 3f, 0f)));   // 300 kg per cubic metre
            for (var t = 0; t < 300; t++)
                world.Step(Grounds.Tick);
            Assert.True(crate.Floating);
            Assert.Equal(2f - 0.3f, crate.Bottom, 3);
        }

        [Fact]
        public void AHeavyOneSinks()
        {
            var world = new PhysicsWorld(Flooded(2f));
            var block = world.Add(new Body("block", new Vector3(0.5f, 0.5f, 0.5f), 400f, new Vector3(0f, 3f, 0f)));   // 3200 kg per cubic metre
            for (var t = 0; t < 600; t++)
                world.Step(Grounds.Tick);
            Assert.False(block.Floating);
            Assert.Equal(0f, block.Bottom, 3);
        }

        [Fact]
        public void AFloatingCrateDriftsOnWhenPushed()
        {
            var world = new PhysicsWorld(Flooded(2f));
            var crate = world.Add(new Body("crate", new Vector3(1f, 1f, 1f), 300f, new Vector3(0f, 3f, 0f)));
            for (var t = 0; t < 300; t++)
                world.Step(Grounds.Tick);

            // One shove, too weak to have shifted it on land, and it glides off, slowing
            crate.ApplyForce(new Vector3(600f, 0f, 0f), 0.5f);
            world.Step(Grounds.Tick);
            for (var t = 0; t < 60; t++)
                world.Step(Grounds.Tick);
            Assert.True(crate.Position.X > 0.005f);
            Assert.True(crate.Velocity.X > 0f);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        public void TheWorldsLakeAndPondDontSpill(int which)
        {
            var terrain = TerrainGenerator.Create();
            var pool = terrain.Pools[which];
            for (var k = 0; k < 360; k++)
            {
                var angle = MathHelper.ToRadians(k);
                var edge = pool.Centre + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * pool.Radius;
                Assert.True(terrain.HeightAt(edge.X, edge.Y) > pool.Level, $"it spills at {edge}");
            }
            Assert.NotNull(terrain.WaterLevelAt(pool.Centre.X, pool.Centre.Y));
        }
    }
}
