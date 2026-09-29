using Microsoft.Xna.Framework;
using System;
using World.Core.Characters;
using Xunit;

namespace World.Core.Tests
{
    // How the droid's rig follows it (see DroidMotion): wheels rolling with the ground, and a lean into speeding up.
    public class DroidMotionTests
    {
        private const float Tick = 1f / 60f;
        private const float North = 0f;   // facing -Z

        private static Vector3 Ahead(float yaw, float speed) => new Vector3(MathF.Sin(yaw), 0f, -MathF.Cos(yaw)) * speed;

        [Fact]
        public void RollingForwardTurnsBothWheelsAsFarAsItGoes()
        {
            var motion = new DroidMotion();
            for (var t = 0; t < 60; t++)
                motion.Follow(Ahead(North, 2f), North, Tick);
            Assert.InRange(motion.LeftRolled, 1.99f, 2.01f);
            Assert.InRange(motion.RightRolled, 1.99f, 2.01f);
        }

        [Fact]
        public void BackingUpTurnsThemBack()
        {
            var motion = new DroidMotion();
            for (var t = 0; t < 30; t++)
                motion.Follow(Ahead(North, -1f), North, Tick);
            Assert.InRange(motion.LeftRolled, -0.51f, -0.49f);
        }

        [Fact]
        public void TurningOnTheSpotTurnsTheWheelsOppositeWays()
        {
            var motion = new DroidMotion();
            var yaw = 0f;
            for (var t = 0; t < 60; t++)
            {
                yaw += MathHelper.PiOver2 * Tick;   // a quarter turn to the right over a second
                motion.Follow(Vector3.Zero, yaw, Tick);
            }
            // The left wheel goes forward round the outside, the right one back, each a quarter of the circle between them
            var quarter = MathHelper.PiOver2 * DroidRig.Track / 2f;
            Assert.InRange(motion.LeftRolled, quarter * 0.98f, quarter * 1.02f);
            Assert.InRange(motion.RightRolled, -quarter * 1.02f, -quarter * 0.98f);
        }

        [Fact]
        public void ItLeansIntoSpeedingUpAndBackFromSlowingDown()
        {
            var motion = new DroidMotion();
            motion.Follow(Vector3.Zero, North, Tick);
            for (var t = 1; t <= 30; t++)
                motion.Follow(Ahead(North, t * 0.1f), North, Tick);   // speeding up at 6 m/s²
            Assert.True(motion.Lean > 0.1f, $"leaning forward only {motion.Lean}");

            for (var t = 29; t >= 0; t--)
                motion.Follow(Ahead(North, t * 0.1f), North, Tick);   // slowing down as hard
            Assert.True(motion.Lean < -0.1f, $"leaning back only {motion.Lean}");
        }

        [Fact]
        public void AtASteadySpeedTheLeanSettlesUpright()
        {
            var motion = new DroidMotion();
            for (var t = 0; t < 30; t++)
                motion.Follow(Ahead(North, t * 0.1f), North, Tick);
            for (var t = 0; t < 120; t++)
                motion.Follow(Ahead(North, 3f), North, Tick);
            Assert.InRange(motion.Lean, -0.01f, 0.01f);
        }

        [Fact]
        public void TheLeanHasALimit()
        {
            var motion = new DroidMotion();
            motion.Follow(Vector3.Zero, North, Tick);
            for (var t = 0; t < 60; t++)
                motion.Follow(Ahead(North, 30f), North, Tick);   // a jump to 30 m/s: an enormous acceleration, once
            Assert.True(motion.Lean <= DroidMotion.MaxLean);
        }
    }
}
