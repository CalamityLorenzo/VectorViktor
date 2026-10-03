using Maps.Coast;
using Maps.Home;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using World.Buildings;
using World.Core.Characters;
using World.Core.Movement;
using World.Core.Physics;
using World.Maps;
using World.Maps.Files;
using Xunit;

namespace World.Core.Tests
{
    // The ramps up to buildings' doorways (see Doorstep): a floor stands a step up from the ground round it, more than
    // wheels or tracks get up, so without them the droid couldn't get in.
    public class DoorstepTests
    {
        private static readonly Lazy<MapLibrary> Library = new Lazy<MapLibrary>(() =>
        {
            var library = new MapLibrary();
            HomeMap.AddTo(library);
            CoastMap.AddTo(library);
            return library;
        });

        public static TheoryData<Locomotion> Bases() => new TheoryData<Locomotion>(Locomotions.Movers);

        private static BuiltWorld Bungalow() => WorldBuilder.Build(new IDistrict[]
        {
            new FileDistrict("test", new DistrictFile { Buildings = { new BuildingEntry("house.bungalow", "b1", Vector2.Zero, Door: Side.South) } }, Library.Value),
        });

        private static void OpenEveryDoor(BuildingGround ground)
        {
            foreach (var door in ground.Doors)
                door.Toggle();
            for (var i = 0; i < 120; i++)
                ground.StepDoors(Grounds.Tick, Array.Empty<Body>(), Array.Empty<(Vector3, float, float)>());
        }

        // Driven at `door` from `back` metres out, `aside` metres off its middle and `degrees` off square: how far
        // past the door's line it gets, and its feet's height there
        private static (float past, float y) Drive(BuildingGround ground, Door door, Gait gait, float back = 3f, float aside = 0f, float degrees = 0f)
        {
            var middle = door.Hinge + door.Shut * door.Width / 2f;
            var across = new Vector2(-door.Into.Y, door.Into.X);
            var turn = MathHelper.ToRadians(degrees);
            var heading = door.Into * MathF.Cos(turn) + across * MathF.Sin(turn);
            var start = middle + across * aside - heading * back;
            var droid = new CharacterController(new Vector3(start.X, door.Bottom + 1f, start.Y), MathF.Atan2(heading.X, -heading.Y)) { Gait = gait };
            droid.SnapToGround(ground);
            Grounds.Run(droid, Grounds.Forward(), back + 3f, ground);
            return (Vector2.Dot(new Vector2(droid.Position.X, droid.Position.Z) - middle, door.Into), droid.Position.Y);
        }

        [Theory]
        [MemberData(nameof(Bases))]
        public void EachGetsInAtAnOpenDoor(Locomotion locomotion)
        {
            var world = Bungalow();
            OpenEveryDoor(world.Ground);
            var door = world.Ground.Doors.Single();
            foreach (var (aside, degrees) in new[] { (0f, 0f), (-0.15f, 10f), (0.15f, -10f), (0.05f, 20f) })
            {
                var (past, y) = Drive(world.Ground, door, Locomotions.GaitOf(locomotion), aside: aside, degrees: degrees);
                Assert.True(past > 1f, $"{aside} m aside, {degrees} degrees off: stopped {past:0.00} m past the door's line");
                Assert.Equal(door.Bottom, y, 0.001f);
            }
        }

        [Fact]
        public void ItRisesFromTheGroundToTheFloorNoSteeperThanItsSlope()
        {
            var world = Bungalow();
            var step = world.Ground.Doorsteps.Single();
            Assert.Equal(world.Terrain.HeightAt(step.End.X, step.End.Y), step.Foot, 0.001f);
            Assert.True((step.Floor - step.Foot) / step.Length <= Doorstep.Slope + 1e-4f);
            // Its top, all the way up and across, is no more than a sub-step's rise from the next bit up it
            for (var x = -step.HalfWidth + 0.05f; x < step.HalfWidth; x += 0.1f)
            {
                var side = new Vector2(-step.Out.Y, step.Out.X) * x;
                float? last = null;
                var top = MathF.Abs(x) < step.GapHalf ? -0.2f : 0f;   // over the threshold, in the doorway; beside it, the wall
                for (var d = step.Length + 0.3f; d >= top - 1e-4f; d -= CharacterController.MaxSubStep)
                {
                    var p = step.Outer + side + step.Out * d;
                    var height = world.Ground.GroundBelow(new Vector3(p.X, step.Floor + 0.01f, p.Y), 1f)!.Value;
                    if (last is { } before)
                        Assert.True(height - before <= Doorstep.Slope * CharacterController.MaxSubStep + 0.005f,
                            $"{x:0.00} across, {d:0.00} out: up {height - before:0.000} in one sub-step");
                    last = height;
                }
                Assert.Equal(step.Floor, last!.Value, 0.001f);
            }
        }

        // Every door out of a building on the maps: the droid gets in on any base that moves, wherever the walker can
        [Theory]
        [InlineData("home")]
        [InlineData("coast")]
        public void EveryWayInOnTheMapsTakesEveryBase(string map)
        {
            var world = WorldBuilder.Build(Library.Value.Open(map));
            OpenEveryDoor(world.Ground);
            var stuck = new List<string>();
            foreach (var step in world.Ground.Doorsteps)
            {
                var door = world.Ground.Doors.FirstOrDefault(d => Vector2.Distance(d.Hinge, step.Inner) < step.GapHalf + 0.1f);
                if (door == null)
                    continue;
                if (Drive(world.Ground, door, Gait.Walker).past < 1f)
                    continue;   // somewhere the walker can't go either: not the doorstep's business
                foreach (var locomotion in Locomotions.Movers)
                    if (Drive(world.Ground, door, Locomotions.GaitOf(locomotion)).past < 1f)
                        stuck.Add($"{Locomotions.NameOf(locomotion)} at the door at {step.Inner}");
            }
            Assert.Empty(stuck);
        }
    }
}
