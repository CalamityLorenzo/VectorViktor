using Microsoft.Xna.Framework;
using System;
using World.Core.Characters;
using World.Core.Movement;
using World.Core.Physics;
using Xunit;

namespace World.Core.Tests
{
    public class PhysicsTests
    {
        private static readonly Vector3 Crate = new Vector3(0.8f, 0.8f, 0.8f);

        private static void Run(PhysicsWorld world, float seconds, Player player = null, MoveInput input = default)
        {
            for (var t = 0; t < (int)MathF.Round(seconds / Grounds.Tick); t++)
            {
                player?.Step(input, Grounds.Tick, world);
                world.Step(Grounds.Tick);
            }
        }

        // Stood a little way west of the crate at (0, 0), facing it (east)
        private static Player WalkerFacing(PhysicsWorld world, float x = -2f) => new Player(new Vector3(x, 0f, 0f), Grounds.East, world);

        [Fact]
        public void ADroppedBoxFallsAndComesToRestOnTheGround()
        {
            var world = new PhysicsWorld(Grounds.Flat());
            var box = world.Add(new Body("box", Crate, 10f, new Vector3(0f, 5f, 0f)));
            Run(world, 2f);
            Assert.True(box.Resting);
            Assert.Equal(0f, box.Bottom, 4);
            Assert.Equal(Vector3.Zero, box.Velocity);
        }

        [Fact]
        public void BoxesDroppedOnEachOtherStack()
        {
            var world = new PhysicsWorld(Grounds.Flat());
            var bottom = world.Add(new Body("bottom", Crate, 20f, new Vector3(0f, 1f, 0f)));
            var middle = world.Add(new Body("middle", Crate, 20f, new Vector3(0.1f, 3f, 0f)));
            var top = world.Add(new Body("top", Crate, 20f, new Vector3(-0.1f, 5f, 0.1f)));
            Run(world, 3f);
            Assert.Equal(0f, bottom.Bottom, 3);
            Assert.Equal(Crate.Y, middle.Bottom, 3);
            Assert.Equal(2f * Crate.Y, top.Bottom, 3);
            Assert.Same(bottom, middle.Support);
            Assert.Same(middle, top.Support);
            Assert.True(top.Velocity.Length() < 1e-3f);
        }

        [Fact]
        public void ASlidingBoxStopsWhereFrictionSaysItShould()
        {
            var world = new PhysicsWorld(Grounds.Flat());
            var box = world.Add(new Body("box", Crate, 10f, Vector3.Zero));
            Run(world, 0.1f);                        // settle it on the ground first
            box.Velocity = new Vector3(3f, 0f, 0f);
            Run(world, 3f);
            var expected = 3f * 3f / (2f * PhysicsWorld.Friction * PhysicsWorld.Gravity);   // v^2 / 2a
            Assert.Equal(Vector3.Zero, box.Velocity);
            Assert.InRange(box.Position.X, expected - 0.1f, expected + 0.1f);
        }

        [Fact]
        public void YouPushALightBoxAlongAtAboutWalkingSpeed()
        {
            var world = new PhysicsWorld(Grounds.Flat());
            var box = world.Add(new Body("light", Crate, 5f, Vector3.Zero));
            var player = WalkerFacing(world);
            Run(world, 4f, player, Grounds.Forward());
            // 4 s at 2.5 m/s is 10 m, less the walk up to it
            Assert.True(box.Position.X > 6f, $"only got it to x = {box.Position.X}");
            Assert.True(player.Body.Position.X < box.Position.X - Crate.X / 2f, "walked through it");
        }

        [Fact]
        public void AHeavierBoxSlowsYouDown()
        {
            float PushedFor4s(float mass)
            {
                var world = new PhysicsWorld(Grounds.Flat());
                var box = world.Add(new Body("box", Crate, mass, Vector3.Zero));
                Run(world, 4f, WalkerFacing(world), Grounds.Forward());
                return box.Position.X;
            }
            var light = PushedFor4s(5f);
            var heavy = PushedFor4s(60f);
            Assert.True(heavy > 1f, "a 60 kg box ought to move");
            Assert.True(heavy < light / 2f, $"60 kg went {heavy} m, 5 kg {light} m");
        }

        [Fact]
        public void ABoxTooHeavyForYouWontBudge()
        {
            var world = new PhysicsWorld(Grounds.Flat());
            var box = world.Add(new Body("steel", Crate, 200f, Vector3.Zero));
            var player = WalkerFacing(world);
            Run(world, 4f, player, Grounds.Forward(run: true));
            Assert.Equal(0f, box.Position.X, 3);
            Assert.True(player.Body.Position.X < -Crate.X / 2f);
        }

        [Fact]
        public void ALightBoxRunningIntoAHeavyOneStops()
        {
            var world = new PhysicsWorld(Grounds.Flat());
            var light = world.Add(new Body("light", Crate, 5f, new Vector3(-2f, 0f, 0f)));
            var heavy = world.Add(new Body("heavy", Crate, 100f, new Vector3(0f, 0f, 0f)));
            Run(world, 0.1f);
            light.Velocity = new Vector3(4f, 0f, 0f);
            Run(world, 2f);
            Assert.True(heavy.Position.X < 0.05f, $"the heavy one was shoved to x = {heavy.Position.X}");
            Assert.True(light.Position.X < heavy.Position.X - Crate.X + 0.01f, "they overlap");
            Assert.Equal(Vector3.Zero, light.Velocity);
        }

        [Fact]
        public void AHeavyBoxBargesALightOneOutOfItsWay()
        {
            var world = new PhysicsWorld(Grounds.Flat());
            var heavy = world.Add(new Body("heavy", Crate, 100f, new Vector3(-0.85f, 0f, 0f)));   // almost touching, so friction hasn't slowed it first
            var light = world.Add(new Body("light", Crate, 5f, new Vector3(0f, 0f, 0f)));
            Run(world, 0.1f);
            heavy.Velocity = new Vector3(3f, 0f, 0f);
            var fastest = 0f;
            var heavyAtImpact = 0f;
            for (var t = 0; t < 60; t++)
            {
                var heavyBefore = heavy.Velocity.X;
                world.Step(Grounds.Tick);
                if (light.Velocity.X > 0f && heavyAtImpact == 0f)
                    heavyAtImpact = heavyBefore;
                fastest = MathF.Max(fastest, light.Velocity.X);
            }
            // Sent off faster than the heavy one hit it, which barely slows
            Assert.True(fastest > heavyAtImpact, $"the light one only reached {fastest} m/s, hit at {heavyAtImpact}");
            Assert.True(heavy.Velocity.X >= 0f);
        }

        [Fact]
        public void PushingTheBottomBoxCarriesTheOneOnTopAlong()
        {
            var world = new PhysicsWorld(Grounds.Flat());
            var bottom = world.Add(new Body("bottom", Crate, 10f, Vector3.Zero));
            var top = world.Add(new Body("top", new Vector3(0.6f, 0.4f, 0.6f), 3f, new Vector3(0f, Crate.Y, 0f)));
            Run(world, 0.1f);
            Run(world, 2f, WalkerFacing(world), Grounds.Forward());
            Assert.True(bottom.Position.X > 1.5f);
            Assert.Same(bottom, top.Support);
            Assert.Equal(bottom.Position.X, top.Position.X, 1);
        }

        [Fact]
        public void ABoxPushedIntoACliffFaceStopsThere()
        {
            var face = Grounds.SlopeFrom(70f);
            var world = new PhysicsWorld(face);
            var box = world.Add(new Body("box", Crate, 5f, new Vector3(-3f, 0f, 0f)));
            var player = WalkerFacing(world, x: -6f);
            Run(world, 5f, player, Grounds.Forward());
            Assert.True(box.Position.X + Crate.X / 2f < 0.2f, $"pushed into the face, to x = {box.Position.X}");
            Assert.True(box.Bottom < 0.3f);
        }

        [Fact]
        public void ABoxSlidingDownAGentleSlopeKeepsItsGrip()
        {
            // Downhill eastwards at 15 degrees
            var slope = Terrain.FromFunction(64, 64, 1f, (x, z) => -MathF.Max(0f, x) * MathF.Tan(MathHelper.ToRadians(15f)));
            var world = new PhysicsWorld(slope);
            var box = world.Add(new Body("box", Crate, 10f, new Vector3(2f, 0f, 0f)));
            Run(world, 0.5f);
            box.Velocity = new Vector3(2f, 0f, 0f);
            var airborne = 0;
            for (var t = 0; t < 60; t++)
            {
                world.Step(Grounds.Tick);
                if (!box.Resting)
                    airborne++;
            }
            Assert.Equal(0, airborne);
            Assert.Equal(Vector3.Zero, box.Velocity);   // friction stopped it, slope and all
        }

        [Fact]
        public void ABoxPushedOffAPlateauFallsToTheGroundBelow()
        {
            var world = new PhysicsWorld(Grounds.PlateauEdge(4f));
            var box = world.Add(new Body("box", Crate, 5f, new Vector3(-2f, 4f, 0f)));
            var player = WalkerFacing(world, x: -5f);
            Run(world, 4f, player, Grounds.Forward());
            Run(world, 2f);
            Assert.True(box.Position.X > 0f);
            Assert.Equal(0f, box.Bottom, 3);
            Assert.True(box.Resting);
        }

        [Fact]
        public void YouStepOntoALowBoxAndJumpOntoATallerOne()
        {
            var world = new PhysicsWorld(Grounds.Flat());
            world.Add(new Body("low", new Vector3(1.5f, 0.25f, 1.5f), 200f, Vector3.Zero));
            var player = WalkerFacing(world);
            Run(world, 0.8f, player, Grounds.Forward());   // to about its middle
            Assert.InRange(player.Body.Position.X, -0.6f, 0.6f);
            Assert.Equal(0.25f, player.Body.Position.Y, 3);

            var world2 = new PhysicsWorld(Grounds.Flat());
            world2.Add(new Body("tall", new Vector3(1.5f, 0.8f, 1.5f), 200f, Vector3.Zero));
            var jumper = WalkerFacing(world2, x: -1.3f);
            Run(world2, 0.2f, jumper, Grounds.Forward());
            jumper.Step(new MoveInput(new Vector2(0f, 1f), Jump: true), Grounds.Tick, world2);
            Run(world2, 0.7f, jumper, Grounds.Forward());   // up and on, then stop before walking off the far side
            Run(world2, 1f, jumper);
            Assert.Equal(0.8f, jumper.Body.Position.Y, 3);
            Assert.True(jumper.Body.Grounded);
        }

        [Fact]
        public void StoodOnAMovingBoxYouAreCarriedWithIt()
        {
            var world = new PhysicsWorld(Grounds.Flat());
            var raft = world.Add(new Body("raft", new Vector3(2f, 0.2f, 2f), 50f, Vector3.Zero));
            var rider = new Player(new Vector3(0f, 0.2f, 0f), 0f, world);
            Run(world, 0.1f, rider);
            raft.Velocity = new Vector3(2f, 0f, 0f);
            Run(world, 0.5f, rider);
            Assert.Equal(raft.Position.X, rider.Body.Position.X, 1);
            Assert.Equal(0.2f, rider.Body.Position.Y, 3);
        }

        [Fact]
        public void ACrateSlidingIntoYouKnocksYouBack()
        {
            var world = new PhysicsWorld(Grounds.Flat());
            var crate = world.Add(new Body("crate", Crate, 60f, new Vector3(-1.2f, 0f, 0f)));   // half a metre off, so friction hasn't stopped it first
            var player = new Player(new Vector3(0f, 0f, 0f), 0f, world);   // side on to it
            Run(world, 0.1f, player);
            crate.Velocity = new Vector3(5f, 0f, 0f);
            Run(world, 0.8f, player);
            // It knocks you off your feet, so you slide rather than brace; and it loses speed doing it
            Assert.True(player.Body.Position.X > 0.4f, $"you only moved to x = {player.Body.Position.X}");
            Assert.True(player.Body.Position.X >= crate.Position.X + Crate.X / 2f + CharacterController.Radius - 0.01f, "it's in you");
            Assert.True(crate.Velocity.X < 1f);
        }
    }
}
