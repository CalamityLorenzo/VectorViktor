using Microsoft.Xna.Framework;
using System;
using World.Core.Movement;
using Xunit;

namespace World.Core.Tests
{
    public class CharacterControllerTests
    {
        private static CharacterController StandOn(IGround ground, float x, float z, float yaw = Grounds.East)
        {
            var walker = new CharacterController(new Vector3(x, 100f, z), yaw);
            walker.SnapToGround(ground);
            return walker;
        }

        [Fact]
        public void StandingOnTheFlatStaysPut()
        {
            var flat = Grounds.Flat();
            var walker = StandOn(flat, 1f, 2f);
            Grounds.Run(walker, MoveInput.None, 10f, flat);
            Assert.True(walker.Grounded);
            Assert.Equal(new Vector3(1f, 0f, 2f), walker.Position);
        }

        [Fact]
        public void WalkingOnTheFlatKeepsToTheGroundAtWalkingSpeed()
        {
            var flat = Grounds.Flat();
            var walker = StandOn(flat, -10f, 0f);
            Grounds.Run(walker, Grounds.Forward(), 4f, flat, w => Assert.True(w.Grounded));
            Assert.Equal(0f, walker.Position.Y);
            Assert.InRange(walker.Position.X, -10f + 4f * CharacterController.WalkSpeed - 0.3f, -10f + 4f * CharacterController.WalkSpeed);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void ClimbsA20DegreeSlope(bool run)
        {
            var slope = Grounds.SlopeFrom(20f);
            var walker = StandOn(slope, -2f, 0f);
            Grounds.Run(walker, Grounds.Forward(run), 3f, slope);
            Assert.True(walker.Grounded);
            Assert.True(walker.Position.X > 4f, $"only got to x = {walker.Position.X}");
            Assert.Equal(slope.HeightAt(walker.Position.X, walker.Position.Z), walker.Position.Y, 3);
        }

        [Fact]
        public void IsStoppedByA60DegreeFaceWithoutClimbingIt()
        {
            var face = Grounds.SlopeFrom(60f);
            var walker = StandOn(face, -5f, 0f);
            Grounds.Run(walker, Grounds.Forward(run: true), 4f, face);
            Assert.True(walker.Position.X < 0.05f, $"got into the face, to x = {walker.Position.X}");
            Assert.True(walker.Position.Y < 0.1f, $"climbed it, to y = {walker.Position.Y}");
            Assert.True(walker.Grounded);
        }

        [Fact]
        public void SlidesAlongA60DegreeFaceWhenWalkingIntoItAtAnAngle()
        {
            var face = Grounds.SlopeFrom(60f);
            var walker = StandOn(face, -3f, 10f, yaw: MathHelper.PiOver4);   // north-east, into the face and along it
            Grounds.Run(walker, Grounds.Forward(), 6f, face);
            Assert.True(walker.Position.X < 0.05f);
            Assert.True(walker.Position.Y < 0.1f);
            Assert.True(walker.Position.Z < 2f, $"stuck against the face at z = {walker.Position.Z}");
        }

        [Fact]
        public void WalksOffACliffFallsAndLands()
        {
            var cliff = Grounds.PlateauEdge(5f);
            var walker = StandOn(cliff, -5f, 0f);
            Assert.Equal(5f, walker.Position.Y);

            var wasAirborne = false;
            var lowest = float.MaxValue;
            Grounds.Run(walker, Grounds.Forward(), 4f, cliff, w =>
            {
                wasAirborne |= !w.Grounded;
                lowest = MathF.Min(lowest, w.Position.Y);
            });

            Assert.True(wasAirborne);
            Assert.True(walker.Grounded);
            Assert.True(walker.Position.X > 1f);
            Assert.Equal(0f, walker.Position.Y, 3);
            Assert.True(lowest >= -1e-3f, "fell through the ground");
        }

        [Fact]
        public void WalkingOffTheEdgeCarriesOnForwardAsItFalls()
        {
            var cliff = Grounds.PlateauEdge(20f);
            var walker = StandOn(cliff, -3f, 0f);
            var xWhenLeaving = float.NaN;
            Grounds.Run(walker, Grounds.Forward(run: true), 3f, cliff, w =>
            {
                if (!w.Grounded && float.IsNaN(xWhenLeaving))
                    xWhenLeaving = w.Position.X;
            });
            // A run of about 6 m/s for the second or so the drop takes carries you well clear of the foot
            Assert.True(walker.Position.X > xWhenLeaving + 5f);
        }

        [Theory]
        [InlineData(0.25f, true)]
        [InlineData(0.5f, false)]
        public void StepsUpNoMoreThanMaxStepUp(float height, bool climbs)
        {
            var step = new StepGround(height);
            var walker = StandOn(step, -2f, 0f);
            Grounds.Run(walker, Grounds.Forward(), 2f, step);
            if (climbs)
            {
                Assert.True(walker.Position.X > 1f);
                Assert.Equal(height, walker.Position.Y);
            }
            else
            {
                Assert.True(walker.Position.X < 0f);
                Assert.Equal(0f, walker.Position.Y);
            }
        }

        [Fact]
        public void JumpsAboutAMetreAndLandsAgain()
        {
            var flat = Grounds.Flat();
            var walker = StandOn(flat, 0f, 0f);
            var highest = 0f;
            walker.Step(new MoveInput(Vector2.Zero, Jump: true), Grounds.Tick, flat);
            Grounds.Run(walker, MoveInput.None, 2f, flat, w => highest = MathF.Max(highest, w.Position.Y));
            Assert.InRange(highest, 0.9f, 1.2f);
            Assert.True(walker.Grounded);
            Assert.Equal(0f, walker.Position.Y);
        }

        [Fact]
        public void CanWalkUpTheCausewayOntoThePlateau()
        {
            var world = TerrainGenerator.Create();
            var centre = TerrainGenerator.PlateauCentre;
            var foot = centre.X - TerrainGenerator.PlateauRadius + 1f - TerrainGenerator.RampLength - 3f;
            var walker = StandOn(world, foot, centre.Y);
            Grounds.Run(walker, Grounds.Forward(), 16f, world);
            Assert.Equal(TerrainGenerator.PlateauHeight, walker.Position.Y, 2);
        }

        [Fact]
        public void CantWalkOffTheEdgeOfTheWorld()
        {
            var flat = Grounds.Flat();
            var walker = StandOn(flat, 28f, 0f);
            Grounds.Run(walker, Grounds.Forward(run: true), 3f, flat);
            Assert.True(walker.Position.X <= 32f);
            Assert.True(walker.Grounded);
        }
    }
}
