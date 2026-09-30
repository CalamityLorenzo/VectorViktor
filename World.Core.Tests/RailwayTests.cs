using Maps.Coast;
using MeshProps;
using Microsoft.Xna.Framework;
using System;
using World.Core.Characters;
using World.Core.Movement;
using World.Maps;
using Xunit;
using static Maps.Coast.Railway;

namespace World.Core.Tests
{
    public class RailwayTests
    {
        private static readonly Lazy<BuiltWorld> Built = new Lazy<BuiltWorld>(() => WorldBuilder.Build(CoastMap.Map));

        // Put there as the game puts you at a start (see Start.Above), and left to settle
        private static Player Drop(Vector2 at, float yaw, float above = 0f)
        {
            var terrain = Built.Value.Terrain;
            var player = new Player(new Vector3(at.X, above > 0f ? terrain.HeightAt(at.X, at.Y) + above : 0f, at.Y), yaw, Built.Value.Physics);
            Run(player, MoveInput.None, 2f);
            return player;
        }

        // As the game steps you: among the bodies, which you stand on and walk into
        private static void Run(Player player, MoveInput input, float seconds, Action<CharacterController> eachTick = null)
        {
            for (var t = 0; t < (int)MathF.Round(seconds / Grounds.Tick); t++)
            {
                player.Step(input, Grounds.Tick, Built.Value.Physics);
                eachTick?.Invoke(player.Body);
            }
        }

        [Fact]
        public void ThePiecesJoinEndToEndFromTheBufferStopToPastTheEdgeOfTheWorld()
        {
            Assert.Equal(new Vector2(BufferX, 0f), Pieces[0].Start);
            for (var k = 1; k < Pieces.Count; k++)
            {
                Assert.True(Vector2.Distance(Pieces[k - 1].End, Pieces[k].Start) < 1e-3f, $"piece {k} doesn't start where {k - 1} ends");
                Assert.Equal(Pieces[k - 1].EndHeading, Pieces[k].Heading, 4);
                Assert.True(Pieces[k].Length <= PieceLength + 1e-3f);
            }
            var last = Pieces[^1];
            Assert.Equal(CoastLineX, last.End.X, 2);
            Assert.True(last.End.Y < -Built.Value.Terrain.Depth / 2f, "stops short of the edge of the world");
            Assert.Equal(MathHelper.Pi, last.EndHeading, 4);   // heading north
        }

        // Every metre along the line, but over the bridge: the ground under the middle of the beam is the formation,
        // level with the plateau, whatever the land round it does
        [Fact]
        public void TheBeamStandsOnLevelGroundAllTheWay()
        {
            var terrain = Built.Value.Terrain;
            foreach (var piece in Pieces)
                for (var s = 0f; s < piece.Length; s += 1f)
                {
                    var at = piece.At(s);
                    if (!terrain.Contains(at.X, at.Y) || (at.X > BridgeWest && at.X < BridgeEast && MathF.Abs(at.Y) < BridgeWidth))
                        continue;
                    Assert.True(MathF.Abs(terrain.HeightAt(at.X, at.Y) - Formation) < 0.01f, $"the formation's at {terrain.HeightAt(at.X, at.Y)} at {at}");
                }
        }

        [Fact]
        public void TheBridgeIsOverTheGorgeWithLandUnderBothEnds()
        {
            var terrain = Built.Value.Terrain;
            foreach (var z in new[] { -FormationHalf, 0f, FormationHalf })
            {
                Assert.True(BridgeWest < CoastTerrain.RiverAt(z) - CoastTerrain.GorgeHalfWidth - 1f);
                Assert.True(BridgeEast > CoastTerrain.RiverAt(z) + CoastTerrain.GorgeHalfWidth + 1f);
            }
            // The formation meets the deck's top, and is level with the plateau either side of the beam
            Assert.Equal(Formation, terrain.HeightAt(BridgeWest, 0f), 2);
            Assert.Equal(Formation, terrain.HeightAt(BridgeEast, 0f), 2);
            foreach (var z in new[] { -FormationHalf + 0.5f, FormationHalf - 0.5f })
            {
                Assert.Equal(Formation, terrain.HeightAt(BridgeWest - 1f, z), 2);
                Assert.Equal(Formation, terrain.HeightAt(BridgeEast + 1f, z), 2);
            }
        }

        // Whichever way you run and jump at it, from either side, beside the beam or along its top: you never get
        // onto the bridge's deck
        [Theory]
        [InlineData(true, 0f, false)]
        [InlineData(true, 0.3f, false)]
        [InlineData(true, -0.3f, false)]
        [InlineData(true, 0.7f, false)]
        [InlineData(true, 0f, true)]
        [InlineData(false, 0f, false)]
        [InlineData(false, 0.3f, false)]
        [InlineData(false, -0.7f, false)]
        [InlineData(false, 0f, true)]
        public void TheBridgesGatesAreShutToAWalker(bool fromTheWest, float offLine, bool onTheBeam)
        {
            var side = onTheBeam ? 0f : 1.5f;
            var from = fromTheWest ? new Vector2(BridgeWest - 8f, side) : new Vector2(BridgeEast + 8f, side);
            var player = Drop(from, (fromTheWest ? MathHelper.PiOver2 : -MathHelper.PiOver2) + offLine, onTheBeam ? 2f : 0f);
            if (onTheBeam)
                Assert.Equal(BeamTop, player.Body.Position.Y, 2);
            var jumping = new MoveInput(new Vector2(0f, 1f), Run: true, Jump: true);
            Run(player, jumping, 15f, w =>
            {
                var onDeck = w.Position.X > BridgeWest + GateInset && w.Position.X < BridgeEast - GateInset &&
                             MathF.Abs(w.Position.Z) < BridgeWidth / 2f && w.Position.Y > Formation - 0.5f;
                Assert.False(onDeck, $"got onto the bridge at {w.Position}");
            });
        }

        [Fact]
        public void TheBridgeAndCoastlineStartsAreOnTheBeam()
        {
            foreach (var name in new[] { "bridge", "coastline" })
            {
                var start = Built.Value.Starts[name];
                var player = Drop(start.At, start.Yaw, start.Above);
                Assert.Equal(BeamTop, player.Body.Position.Y, 2);
            }
        }

        // Walking east along the beam's top from the bridge start: on it all the way, up to the bridge's gate
        [Fact]
        public void YouCanWalkAlongTheBeamToTheBridgesGate()
        {
            var start = Built.Value.Starts["bridge"];
            var player = Drop(start.At, start.Yaw, start.Above);
            Run(player, Grounds.Forward(), 6f, w => Assert.Equal(BeamTop, w.Position.Y, 2));
            Assert.True(player.Body.Position.X > BridgeWest - 1f, $"stopped at x = {player.Body.Position.X}");
            Assert.True(player.Body.Position.X < BridgeWest + GateInset, "got past the gate");
        }

        // Walking at it from beside it: stopped, on the ground, not up onto it or into it
        [Fact]
        public void TheBeamIsTooHighToStepUpOnto()
        {
            var player = Drop(new Vector2(-200f, -3f), MathHelper.Pi);   // facing south, at it
            Assert.Equal(Formation, player.Body.Position.Y, 2);
            Run(player, Grounds.Forward(), 3f);
            Assert.Equal(Formation, player.Body.Position.Y, 2);
            Assert.True(player.Body.Position.Z < -RailwayMesh.BeamWidth / 2f, $"walked into the beam, to z = {player.Body.Position.Z}");
        }

        [Fact]
        public void TheStationStartIsOnThePlatformAndTheHallStartOnItsFloor()
        {
            foreach (var name in new[] { "station", "hall" })
            {
                var start = Built.Value.Starts[name];
                var player = Drop(start.At, start.Yaw, start.Above);
                Assert.Equal(Station.PlatformTop, player.Body.Position.Y, 2);
            }
        }

        [Fact]
        public void TheStepsClimbOntoThePlatform()
        {
            // From the line's side of the platform's east end, walking west up the steps
            var player = Drop(new Vector2(Station.PlatformEast + 4f, Station.PlatformCentre.Y), -MathHelper.PiOver2);
            Assert.Equal(Formation, player.Body.Position.Y, 2);
            Run(player, Grounds.Forward(), 4f);
            Assert.Equal(Station.PlatformTop, player.Body.Position.Y, 2);
            Assert.True(player.Body.Position.X < Station.PlatformEast, "didn't get onto the platform");
        }

        [Fact]
        public void ThePlatformsEdgeIsTooHighToStepUpFromTheLine()
        {
            // Between the beam and the platform, facing north, at the platform: stopped at its edge, not up onto it or into it
            var player = Drop(new Vector2(Station.PlatformCentre.X, -1f), 0f);
            Run(player, Grounds.Forward(), 3f);
            Assert.True(player.Body.Position.Y < Station.PlatformTop - 0.5f, $"climbed onto the platform from the line, to {player.Body.Position.Y}");
            Assert.True(player.Body.Position.Z > -Station.PlatformEdge, $"walked into the platform, to z = {player.Body.Position.Z}");
        }
    }
}
