using Microsoft.Xna.Framework;
using System;
using World.Core.Characters;
using World.Core.Movement;
using World.Core.Physics;
using Xunit;

namespace World.Core.Tests
{
    public class ToppleTests
    {
        private static readonly Vector3 Tall = new Vector3(0.5f, 1.8f, 0.5f);   // a locker, say
        private static readonly Vector3 Crate = new Vector3(0.8f, 0.8f, 0.8f);

        private static void Run(PhysicsWorld world, float seconds, Player player = null, MoveInput input = default)
        {
            for (var t = 0; t < (int)MathF.Round(seconds / Grounds.Tick); t++)
            {
                player?.Step(input, Grounds.Tick, world);
                world.Step(Grounds.Tick);
            }
        }

        private static Player WalkerFacing(PhysicsWorld world, float x = -2f) => new Player(new Vector3(x, 0f, 0f), Grounds.East, world);

        private static bool Upright(Body body) => body.Rotation == Matrix.Identity && body.Size == body.OriginalSize;

        [Fact]
        public void ATallBoxYouPushTipsOverAwayFromYou()
        {
            var world = new PhysicsWorld(Grounds.Flat());
            var locker = world.Add(new Body("locker", Tall, 20f, Vector3.Zero));
            Run(world, 3f, WalkerFacing(world), Grounds.Forward());
            Run(world, 1f);

            Assert.False(Upright(locker));
            Assert.Equal(Tall.Y, locker.Size.X, 3);   // lying along the way it fell
            Assert.Equal(Tall.X, locker.Size.Y, 3);
            Assert.Equal(0f, locker.Bottom, 3);
            Assert.True(locker.Resting);
            Assert.True(locker.Position.X > 0.5f, "it didn't fall away from you");
            var top = Vector3.TransformNormal(Vector3.Up, locker.Rotation);
            Assert.Equal(1f, top.X, 3);                // its top now faces the way it fell
        }

        [Fact]
        public void ACubeYouPushSlidesAndStaysUpright()
        {
            var world = new PhysicsWorld(Grounds.Flat());
            var crate = world.Add(new Body("crate", Crate, 25f, Vector3.Zero));
            Run(world, 3f, WalkerFacing(world), Grounds.Forward());
            Assert.True(Upright(crate));
            Assert.True(crate.Position.X > 2f);
        }

        [Fact]
        public void WhereItsDrawnIsWhereItIs()
        {
            var world = new PhysicsWorld(Grounds.Flat());
            var locker = world.Add(new Body("locker", Tall, 20f, Vector3.Zero));
            Run(world, 3f, WalkerFacing(world), Grounds.Forward());
            Run(world, 1f);
            Assert.False(Upright(locker));

            // The corners of the shape it was made as, put where Pose puts them, fill exactly the box it is now
            var min = new Vector3(float.MaxValue);
            var max = new Vector3(float.MinValue);
            var h = locker.OriginalSize / 2f;
            for (var i = 0; i < 8; i++)
            {
                var corner = new Vector3((i & 1) == 0 ? -h.X : h.X, (i & 2) == 0 ? 0f : 2f * h.Y, (i & 4) == 0 ? -h.Z : h.Z);
                var placed = Vector3.Transform(corner, locker.Pose);
                min = Vector3.Min(min, placed);
                max = Vector3.Max(max, placed);
            }
            var expectedMin = locker.Position - new Vector3(locker.Size.X / 2f, 0f, locker.Size.Z / 2f);
            var expectedMax = locker.Position + new Vector3(locker.Size.X / 2f, locker.Size.Y, locker.Size.Z / 2f);
            Assert.True(Vector3.Distance(min, expectedMin) < 1e-3f, $"{min} vs {expectedMin}");
            Assert.True(Vector3.Distance(max, expectedMax) < 1e-3f, $"{max} vs {expectedMax}");
        }

        [Fact]
        public void PushingIntoAStackKnocksItDown()
        {
            var world = new PhysicsWorld(Grounds.Flat());
            var crates = new[]
            {
                world.Add(new Body("bottom", Crate, 25f, new Vector3(0f, 0f, 0f))),
                world.Add(new Body("middle", Crate, 25f, new Vector3(0f, 0.8f, 0f))),
                world.Add(new Body("top", Crate, 25f, new Vector3(0f, 1.6f, 0f))),
            };
            Run(world, 0.2f);
            Assert.True(crates[2].Bottom > 1.59f);

            Run(world, 3f, WalkerFacing(world), Grounds.Forward());
            Run(world, 2f);
            Assert.True(crates[2].Bottom < 1f, $"the top crate is still up at {crates[2].Bottom}");
            foreach (var crate in crates)
                Assert.True(crate.Resting, $"{crate.Name} never came to rest");
        }

        [Fact]
        public void ABoxLeftOverhangingTipsOffItsSupport()
        {
            var world = new PhysicsWorld(Grounds.Flat());
            world.Add(Body.Fixed("plinth", Crate, Vector3.Zero));
            var hanging = world.Add(new Body("hanging", Crate, 10f, new Vector3(0.55f, 0.8f, 0f)));   // centre 15 cm past the edge
            var safe = world.Add(new Body("safe", new Vector3(0.3f, 0.3f, 0.3f), 2f, new Vector3(-0.2f, 0.8f, 0f)));   // well on
            Run(world, 2f);

            Assert.False(Upright(hanging));
            Assert.Equal(0f, hanging.Bottom, 3);
            Assert.True(hanging.Position.X > 0.4f);
            Assert.True(Upright(safe));
            Assert.Equal(0.8f, safe.Bottom, 3);
        }

        [Fact]
        public void WalkingPastATallBoxDoesntPushItOver()
        {
            // Within arm's reach of your path, but beside it rather than in front
            var world = new PhysicsWorld(Grounds.Flat());
            var locker = world.Add(new Body("locker", Tall, 20f, new Vector3(0f, 0f, 0.8f)));
            Run(world, 3f, WalkerFacing(world), Grounds.Forward());
            Assert.True(Upright(locker));
            Assert.Equal(new Vector3(0f, 0f, 0.8f), locker.Position);
        }

        [Fact]
        public void ItWontTipIntoAWall()
        {
            var world = new PhysicsWorld(Grounds.Flat());
            var locker = world.Add(new Body("locker", Tall, 20f, Vector3.Zero));
            world.Add(Body.Fixed("wall", new Vector3(0.3f, 3f, 4f), new Vector3(0.5f, 0f, 0f)));   // just past it
            Run(world, 3f, WalkerFacing(world), Grounds.Forward());
            Run(world, 1f);
            Assert.True(Upright(locker));
            Assert.True(locker.Position.X < 0.11f);
        }

        [Theory]
        [InlineData(4f, true)]
        [InlineData(0.3f, false)]
        public void AFastBlowHighUpKnocksItOverAndASlowOneDoesnt(float speed, bool knockedOver)
        {
            // A crate sliding along a shelf, into the top half of a light, tall box standing beside the shelf's end
            var world = new PhysicsWorld(Grounds.Flat());
            world.Add(Body.Fixed("shelf", new Vector3(3f, 0.8f, 1f), new Vector3(-1.8f, 0f, 0f)));   // ends at x = -0.3
            var pole = world.Add(new Body("pole", new Vector3(0.4f, 1.6f, 0.4f), 5f, new Vector3(0f, 0f, 0f)));
            var crate = world.Add(new Body("crate", new Vector3(0.6f, 0.6f, 0.6f), 60f, new Vector3(-0.55f, 0.8f, 0f)));
            Run(world, 0.1f);
            crate.Velocity = new Vector3(speed, 0f, 0f);
            Run(world, 2f);
            Assert.Equal(knockedOver, !Upright(pole));
        }

        [Fact]
        public void ABoxOnAGentleSlopeStaysPutAndUpright()
        {
            var slope = Terrain.FromFunction(64, 64, 1f, (x, z) => x * MathF.Tan(MathHelper.ToRadians(15f)));
            var world = new PhysicsWorld(slope);
            var crate = world.Add(new Body("crate", Crate, 25f, new Vector3(2f, 2f, 0f)));
            Run(world, 3f);
            Assert.True(Upright(crate));
            Assert.True(crate.Resting);
            Assert.Equal(2f, crate.Position.X, 2);
        }

        [Fact]
        public void ABoxOnACliffFaceSlidesOffIt()
        {
            // A 70 degree face, 8 m high: dropped onto it, it can't rest there
            var face = Grounds.SlopeFrom(70f, cap: 8f);
            var world = new PhysicsWorld(face);
            var crate = world.Add(new Body("crate", Crate, 25f, new Vector3(1.5f, 6f, 0f)));
            Run(world, 4f);
            Assert.True(crate.Resting);
            Assert.True(crate.Position.X < 0f, $"it's still on the face, at x = {crate.Position.X}");
            Assert.Equal(0f, crate.Bottom, 2);
        }
    }
}
