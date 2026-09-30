using Microsoft.Xna.Framework;
using System;
using World.Buildings;
using World.Core.Movement;
using Xunit;

namespace World.Core.Tests
{
    public class LedgeTests
    {
        // A beam along the X axis from x = -10 to 10, like the monorail's: 0.7 m wide and 0.9 m tall on flat ground
        private const float Half = 0.35f, Top = 0.9f;
        private static BuildingGround Beam(float top = Top) =>
            new BuildingGround(Grounds.Flat(), Array.Empty<Building>(), ledges: new[] { new Ledge(new Vector2(-10f, 0f), new Vector2(10f, 0f), Half, -0.3f, top) });

        private static CharacterController Walker(IGround ground, Vector3 at, float yaw)
        {
            var walker = new CharacterController(at, yaw);
            walker.SnapToGround(ground);
            return walker;
        }

        [Fact]
        public void WalkingIntoItStopsYouAtItsSideOnTheGround()
        {
            var ground = Beam();
            var walker = Walker(ground, new Vector3(0f, 0f, -3f), MathHelper.Pi);   // facing south, at it
            Grounds.Run(walker, Grounds.Forward(), 3f, ground);
            Assert.Equal(0f, walker.Position.Y, 3);
            Assert.True(walker.Position.Z <= -Half - CharacterController.Radius + 0.01f, $"got into it, to z = {walker.Position.Z}");
            Assert.True(walker.Position.Z > -Half - CharacterController.Radius - 0.1f, $"stopped short, at z = {walker.Position.Z}");
        }

        [Fact]
        public void YouCanJumpUpOntoItAndWalkAlongItsTop()
        {
            var ground = Beam();
            // Walking at it, then jumping, and stopping once landed on it
            var walker = Walker(ground, new Vector3(-5f, 0f, -2.5f), MathHelper.Pi);
            Grounds.Run(walker, Grounds.Forward(), 0.4f, ground);
            var jumping = new MoveInput(new Vector2(0f, 1f), Jump: true);
            for (var t = 0; t < 120 && !(walker.Grounded && walker.Position.Y > Top / 2f); t++)
                walker.Step(jumping, Grounds.Tick, ground);
            Grounds.Run(walker, MoveInput.None, 1f, ground);
            Assert.Equal(Top, walker.Position.Y, 3);

            // Along it, east
            walker.Yaw = Grounds.East;
            var z = walker.Position.Z;
            Grounds.Run(walker, Grounds.Forward(), 3f, ground, w => Assert.Equal(Top, w.Position.Y, 3));
            Assert.True(walker.Position.X > 1f);
            Assert.Equal(z, walker.Position.Z, 2);
        }

        [Fact]
        public void WalkingOffItsSideDropsYouToTheGround()
        {
            var ground = Beam();
            var walker = Walker(ground, new Vector3(0f, 2f, 0f), 0f);
            Assert.Equal(Top, walker.Position.Y, 3);
            Grounds.Run(walker, Grounds.Forward(), 2f, ground);
            Assert.Equal(0f, walker.Position.Y, 3);
            Assert.True(walker.Position.Z < -Half);
        }

        [Fact]
        public void OneNoHigherThanAStepIsSteppedUpOnto()
        {
            var ground = Beam(top: 0.2f);
            var walker = Walker(ground, new Vector3(0f, 0f, -3f), MathHelper.Pi);
            Grounds.Run(walker, Grounds.Forward(), 1.2f, ground);
            Assert.Equal(0.2f, walker.Position.Y, 3);
            Assert.True(MathF.Abs(walker.Position.Z) < Half);
        }

        [Fact]
        public void ItsTopIsTheSkylineOverIt()
        {
            var ground = Beam();
            Assert.Equal(Top, ground.SkylineAt(0f, 0.3f, 0f), 3);
            Assert.Equal(0f, ground.SkylineAt(0f, 1f, 0f), 3);
            Assert.Equal(Top, ground.SkylineAt(0f, 1f, 0.7f), 3);
        }
    }
}
