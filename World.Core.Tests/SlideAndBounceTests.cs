using Microsoft.Xna.Framework;
using System;
using System.Linq;
using World.Buildings;
using World.Core.Physics;
using Xunit;

namespace World.Core.Tests
{
    // Boxes on hills and over edges (see PhysicsWorld): they slide down slopes steep enough, tip over an edge once and
    // go on down whatever's below it, and bounce when they land hard.
    public class SlideAndBounceTests
    {
        private static readonly Vector3 Crate = new Vector3(0.8f, 0.8f, 0.8f);

        // Steps the world `seconds`, pushing the body with `push` low down all the while (as a droid would), and says how
        // many times it began to topple
        private static int Run(PhysicsWorld world, float seconds, Body watched = null, Vector3 push = default)
        {
            var (topples, was) = (0, false);
            for (var t = 0; t < (int)MathF.Round(seconds / Grounds.Tick); t++)
            {
                if (push != Vector3.Zero && watched is { Toppling: false })
                    watched.ApplyForce(push, 0.2f);
                world.Step(Grounds.Tick);
                if (watched != null && watched.Toppling && !was)
                    topples++;
                was = watched?.Toppling ?? false;
            }
            return topples;
        }

        private static Terrain Hill(float degrees) =>
            Terrain.FromFunction(64, 64, 1f, (x, z) => 20f - x * MathF.Tan(MathHelper.ToRadians(degrees)));   // falling away east

        // Set down on the ground there, not dropped (which would bounce it)
        private static Body SetDown(PhysicsWorld world, Terrain terrain, float x, float z = 0f)
        {
            var highest = terrain.HeightAt(x - Crate.X / 2f, z);
            return world.Add(new Body("crate", Crate, 25f, new Vector3(x, highest + 0.02f, z)));
        }

        [Fact]
        public void OnAHillSteeperThanItsGripABoxSlidesDownByItself()
        {
            var hill = Hill(35f);
            var world = new PhysicsWorld(hill);
            var crate = SetDown(world, hill, 5f);
            var topples = Run(world, 2f, crate);
            Assert.True(crate.Position.X > 6f, $"only got to x = {crate.Position.X}");
            Assert.Equal(0, topples);   // a hillside isn't an edge
            Assert.True(crate.Size == crate.OriginalSize);
        }

        [Fact]
        public void PushedDownAGentleHillItGoesFurtherThanOnTheFlat()
        {
            float Slid(Terrain ground)
            {
                var world = new PhysicsWorld(ground);
                var crate = SetDown(world, ground, 5f);
                Run(world, 0.5f);
                crate.Velocity = new Vector3(2f, 0f, 0f);   // shoved east, downhill
                Run(world, 3f);
                return crate.Position.X - 5f;
            }
            var flat = Slid(Grounds.Flat());
            var downhill = Slid(Hill(15f));
            Assert.True(downhill > flat * 1.5f, $"{downhill} m downhill, {flat} m on the flat");
            Assert.True(downhill < 10f, "it ought to stop: the hill's gentler than its grip");
        }

        // A platform 0.72 m high west of x = 0, as the workshop's is: level strips side by side
        private static BuildingGround Platform() =>
            new BuildingGround(Grounds.Flat(), Array.Empty<Building>(), ledges: Enumerable.Range(0, 12)
                .Select(k => new Ledge(new Vector2(-0.25f - 0.5f * k, -3f), new Vector2(-0.25f - 0.5f * k, 3f), 0.26f, -0.2f, 0.72f)));

        [Fact]
        public void PushedOffAPlatformItTipsOverOnceAndLandsBelow()
        {
            var world = new PhysicsWorld(Platform());
            var crate = world.Add(new Body("crate", Crate, 25f, new Vector3(-1f, 0.74f, 0f)));
            Run(world, 0.5f);
            var topples = Run(world, 1.5f, crate, push: new Vector3(200f, 0f, 0f));   // east, towards the edge
            topples += Run(world, 3f, crate);
            Assert.Equal(1, topples);
            Assert.False(crate.Toppling);
            Assert.True(crate.Resting);
            Assert.Equal(0f, crate.Bottom, 2);
            Assert.True(crate.Position.X > 0f, $"still on the platform at x = {crate.Position.X}");
        }

        [Fact]
        public void PushedOffASheerEdgeItTipsOverOnceAndEndsAtTheFoot()
        {
            var cliff = Grounds.PlateauEdge(4f);
            var world = new PhysicsWorld(cliff);
            var crate = world.Add(new Body("crate", Crate, 25f, new Vector3(-1.5f, 4.02f, 0f)));
            Run(world, 0.5f);
            var topples = Run(world, 2f, crate, push: new Vector3(200f, 0f, 0f));
            topples += Run(world, 4f, crate);
            Assert.True(topples <= 1, $"tipped {topples} times");
            Assert.False(crate.Toppling);
            Assert.True(crate.Resting);
            Assert.Equal(0f, crate.Bottom, 2);
        }

        [Fact]
        public void LandingHardItBouncesThenComesToRest()
        {
            var world = new PhysicsWorld(Grounds.Flat());
            var crate = world.Add(new Body("crate", Crate, 25f, new Vector3(0f, 3f, 0f)));
            var (landed, highestAfter) = (false, 0f);
            for (var t = 0; t < 4f / Grounds.Tick; t++)
            {
                world.Step(Grounds.Tick);
                landed |= crate.Bottom < 0.01f;
                if (landed)
                    highestAfter = MathF.Max(highestAfter, crate.Bottom);
            }
            Assert.True(highestAfter > 0.2f, $"it only came back up {highestAfter} m");
            Assert.True(crate.Resting);
            Assert.Equal(0f, crate.Bottom, 3);
        }

        [Fact]
        public void LandingOnAHillItBouncesOnDownIt()
        {
            var hill = Hill(30f);
            var world = new PhysicsWorld(hill);
            var crate = world.Add(new Body("crate", Crate, 25f, new Vector3(5f, hill.HeightAt(4.6f, 0f) + 2f, 0f)));
            var bounced = false;
            for (var t = 0; t < 1.5f / Grounds.Tick; t++)
            {
                world.Step(Grounds.Tick);
                bounced |= crate.Velocity.X > 1f;   // knocked off down the hill as it lands (it fell straight down)
            }
            Assert.True(bounced, "it didn't bounce");
            Assert.True(crate.Position.X > 5.5f, $"it bounced back up the hill, or not at all: x = {crate.Position.X}");
        }

        [Fact]
        public void ASoftLandingDoesntBounce()
        {
            var world = new PhysicsWorld(Grounds.Flat());
            var crate = world.Add(new Body("crate", Crate, 25f, new Vector3(0f, 0.2f, 0f)));
            for (var t = 0; t < 1f / Grounds.Tick; t++)
            {
                world.Step(Grounds.Tick);
                Assert.True(crate.Velocity.Y <= 0f, "it bounced");
            }
            Assert.True(crate.Resting);
        }
    }
}
