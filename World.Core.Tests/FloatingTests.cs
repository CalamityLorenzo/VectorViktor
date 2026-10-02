using Maps.Home;
using Microsoft.Xna.Framework;
using System;
using System.Linq;
using World.Core.Characters;
using World.Core.Physics;
using World.Maps;
using Xunit;

namespace World.Core.Tests
{
    // What floats is easy to push (see PhysicsWorld): nothing grips it, only the water drags on it. Run aground, the water
    // still holds up most of it.
    public class FloatingTests
    {
        private static readonly Vector3 Crate = new Vector3(0.8f, 0.8f, 0.8f);

        private static void Run(PhysicsWorld world, float seconds, Body pushed = null, Vector3 push = default)
        {
            for (var t = 0; t < (int)MathF.Round(seconds / Grounds.Tick); t++)
            {
                if (pushed != null)
                    pushed.ApplyForce(push, 0.2f);
                world.Step(Grounds.Tick);
            }
        }

        [Fact]
        public void AFloatingCrateTooHeavyToShiftOnLandGoesWithAPushTheWeakestDroidCanGive()
        {
            var push = new Vector3(Locomotions.GaitOf(Locomotion.Segway).PushForce * 0.5f, 0f, 0f);
            var onLand = new PhysicsWorld(Grounds.Flat());
            var dry = onLand.Add(new Body("dry", Crate, 300f, new Vector3(0f, 0.02f, 0f)));
            Run(onLand, 0.5f);
            Run(onLand, 3f, dry, push);
            Assert.Equal(0f, dry.Position.X, 3);

            var pond = new PhysicsWorld(Grounds.Flat().Flood(new Pool(Vector2.Zero, 20f, 1.5f)));
            var afloat = pond.Add(new Body("afloat", Crate, 300f, new Vector3(0f, 1.5f, 0f)));
            Run(pond, 0.5f);
            Assert.True(afloat.Floating);
            Run(pond, 3f, afloat, push);
            Assert.True(afloat.Position.X > 0.5f, $"only went {afloat.Position.X} m");
        }

        [Fact]
        public void AgroundInTheShallowsTheWaterStillHoldsMostOfItUp()
        {
            // Water a little shallower than it floats in: it's on the bottom, but a push it wouldn't feel on land moves it
            var push = new Vector3(400f, 0f, 0f);
            var shallows = new PhysicsWorld(Grounds.Flat().Flood(new Pool(Vector2.Zero, 20f, 0.4f)));
            var crate = shallows.Add(new Body("crate", Crate, 300f, new Vector3(0f, 0.02f, 0f)));
            Run(shallows, 0.5f);
            Assert.False(crate.Floating);
            Run(shallows, 2f, crate, push);
            Assert.True(crate.Position.X > 0.3f, $"only went {crate.Position.X} m");
        }

        public static TheoryData<Locomotion?> Pushers() => new TheoryData<Locomotion?> { null, Locomotion.Segway, Locomotion.TriStar, Locomotion.Tracks };

        // The big crate afloat on the home map's pond (see Countryside): too heavy for any of them on land, but any of
        // them can push it across the pond
        [Theory]
        [MemberData(nameof(Pushers))]
        public void AnyDroidPushesThePondCrateAcrossThePond(Locomotion? on)
        {
            var world = WorldBuilder.Build(HomeMap.Map);
            var crate = world.Physics.Bodies.First(b => b.Name == "pond crate");
            Run(world.Physics, 2f);
            var start = crate.Position;
            var from = start - new Vector3(2.5f, 0f, 0f);   // west of it, pushing east over the deep middle
            var droid = new Player(new Vector3(from.X, world.Terrain.HeightAt(from.X, from.Z) + 0.5f, from.Z), MathHelper.PiOver2, world.Physics);
            if (on is { } locomotion)
                droid.Body.Gait = Locomotions.GaitOf(locomotion);
            for (var t = 0; t < 8f / Grounds.Tick; t++)
            {
                droid.Step(Grounds.Forward(), Grounds.Tick, world.Physics);
                world.Physics.Step(Grounds.Tick);
            }
            Assert.True(crate.Position.X - start.X > 2.5f, $"only pushed it {crate.Position.X - start.X:F2} m");
        }
    }
}
