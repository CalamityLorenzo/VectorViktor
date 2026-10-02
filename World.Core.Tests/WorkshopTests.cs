using Maps.Home;
using Microsoft.Xna.Framework;
using System;
using World.Core.Characters;
using World.Core.Movement;
using World.Maps;
using Xunit;

namespace World.Core.Tests
{
    // The workshop yard's course (see Workshop) does what it says it does: each way the droid gets about gets up the
    // kerbs, stairs and through the gaps painted or built for it, and no others.
    public class WorkshopTests
    {
        private static readonly Lazy<BuiltWorld> Built = new Lazy<BuiltWorld>(() => WorldBuilder.Build(HomeMap.Map));

        private const float North = 0f, West = -MathHelper.PiOver2;

        // A droid on `locomotion` at `at` (across the yard from its middle) facing `yaw`, driven forward until `done` or
        // `seconds` are up
        private static CharacterController Drive(Locomotion locomotion, Vector2 at, float yaw, Func<CharacterController, Vector2, bool> done, float seconds = 8f)
        {
            var world = Built.Value;
            var p = Workshop.Centre + at;
            var droid = new CharacterController(new Vector3(p.X, world.Terrain.HeightAt(p.X, p.Y) + 0.3f, p.Y), yaw) { Gait = Locomotions.GaitOf(locomotion) };
            droid.SnapToGround(world.Physics);
            for (var t = 0; t < seconds / Grounds.Tick && !done(droid, Across(droid)); t++)
                droid.Step(Grounds.Forward(), Grounds.Tick, world.Physics);
            return droid;
        }

        private static Vector2 Across(CharacterController droid) => new Vector2(droid.Position.X, droid.Position.Z) - Workshop.Centre;

        private static float Ground => Built.Value.Terrain.HeightAt(Workshop.Centre.X, Workshop.Centre.Y);

        // The kerbs: green, yellow, orange, red, west to east; each painted for the first that gets up it
        [Theory]
        [InlineData(Locomotion.Segway, 0, true)]
        [InlineData(Locomotion.Segway, 1, false)]
        [InlineData(Locomotion.Tracks, 1, true)]
        [InlineData(Locomotion.Tracks, 2, false)]
        [InlineData(Locomotion.TriStar, 2, true)]
        [InlineData(Locomotion.TriStar, 3, false)]
        public void EachGetsOverTheKerbsPaintedForItAndNoHigher(Locomotion locomotion, int kerb, bool over)
        {
            var droid = Drive(locomotion, new Vector2(-8.5f + 2f * kerb, 0f), North, (d, at) => at.Y < -3f);
            Assert.Equal(over, Across(droid).Y < -3f);
            if (!over)
                Assert.Equal(Ground, droid.Position.Y, 2);
        }

        [Theory]
        [InlineData(Locomotion.Segway, false)]
        [InlineData(Locomotion.Tracks, false)]
        [InlineData(Locomotion.TriStar, true)]
        public void OnlyTheTriStarClimbsTheStairToThePlatform(Locomotion locomotion, bool climbs)
        {
            var droid = Drive(locomotion, new Vector2(3.5f, 0.5f), North, (d, at) => d.Position.Y > Ground + Workshop.PlatformTop - 0.01f);
            Assert.Equal(climbs, droid.Position.Y > Ground + Workshop.PlatformTop - 0.01f);
        }

        [Theory]
        [MemberData(nameof(LocomotionTests.Bases), MemberType = typeof(LocomotionTests))]
        public void AnyOfThemDrivesUpTheRamp(Locomotion locomotion)
        {
            var droid = Drive(locomotion, new Vector2(9.7f, -4.5f), West, (d, at) => at.X < 4f);
            Assert.Equal(Ground + Workshop.PlatformTop, droid.Position.Y, 2);
        }

        // The narrow gap is the west one, the wide one east of it (see Workshop.Gates)
        [Theory]
        [InlineData(Locomotion.Segway, -8.15f, true)]
        [InlineData(Locomotion.TriStar, -8.15f, true)]
        [InlineData(Locomotion.Tracks, -8.15f, false)]
        [InlineData(Locomotion.Tracks, -5.8f, true)]
        public void TracksDontFitThroughTheNarrowGap(Locomotion locomotion, float x, bool through)
        {
            var droid = Drive(locomotion, new Vector2(x, -5.5f), North, (d, at) => at.Y < -7.6f);
            Assert.Equal(through, Across(droid).Y < -7.6f);
        }

        [Fact]
        public void TheWorkshopStartIsInTheYard()
        {
            var start = Built.Value.Starts["workshop"];
            var at = start.At - Workshop.Centre;
            Assert.True(MathF.Abs(at.X) < Workshop.Half && MathF.Abs(at.Y) < Workshop.Half);
        }
    }
}
