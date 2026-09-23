using Microsoft.Xna.Framework;
using System;
using World.Core.Characters;
using World.Core.Movement;
using Xunit;

namespace World.Core.Tests
{
    public class DroneTests
    {
        [Fact]
        public void SettlesBehindAndAboveAStationaryOwner()
        {
            var flat = Grounds.Flat();
            var player = new Player(new Vector3(0f, 0f, 0f), 0f, flat);
            player.Drone.Reset(new Vector3(10f, 0f, 10f), 2f, flat);   // somewhere else entirely

            Grounds.Run(player.Body, MoveInput.None, 0f, flat);
            for (var t = 0; t < 5 * 60; t++)
                player.Step(MoveInput.None, Grounds.Tick, flat);

            var station = Drone.Station(player.Body.Position, player.Body.Yaw);
            Assert.True(Vector3.Distance(station, player.Drone.Position) < 0.05f);
            Assert.Equal(new Vector3(0f, Drone.FollowHeight, Drone.FollowDistance).Z, player.Drone.Position.Z, 1);   // behind: facing north, behind is +Z
            Assert.Equal(0f, MathHelper.WrapAngle(player.Drone.Yaw), 1);   // and turned to face north, as they are
        }

        [Fact]
        public void KeepsItsClearanceOverARidge()
        {
            // A 6 m ridge running north-south, gentle enough to walk over
            var ridge = Terrain.FromFunction(64, 64, 1f, (x, z) => 6f * MathF.Exp(-x * x / 60f));
            var player = new Player(new Vector3(-20f, 0f, 0f), Grounds.East, ridge);
            for (var t = 0; t < 16 * 60; t++)
            {
                player.Step(Grounds.Forward(), Grounds.Tick, ridge);
                var d = player.Drone.Position;
                Assert.True(d.Y >= ridge.HeightAt(d.X, d.Z) + Drone.MinClearance - 1e-3f, $"drone dipped into the ridge at x = {d.X}");
            }
            Assert.True(player.Body.Position.X > 15f, "the walker didn't get over the ridge");
        }

        [Fact]
        public void LagsBehindWhenItsOwnerRuns()
        {
            var flat = Grounds.Flat();
            var player = new Player(new Vector3(-20f, 0f, 0f), Grounds.East, flat);
            for (var t = 0; t < 60; t++)
                player.Step(Grounds.Forward(run: true), Grounds.Tick, flat);
            var station = Drone.Station(player.Body.Position, player.Body.Yaw);
            Assert.True(player.Drone.Position.X < station.X - 0.3f, "it kept up as if it were bolted on");
        }
    }
}
