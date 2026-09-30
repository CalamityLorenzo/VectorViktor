using Microsoft.Xna.Framework;
using System;
using World.Buildings;
using World.Core.Animation;
using World.Core.Movement;
using Xunit;

namespace World.Core.Tests
{
    // Chests of drawers and cupboards (see Cabinet, CabinetRig): AnimationPlan.md step 3.
    public class CabinetTests
    {
        private const float Floor = 0.15f;
        private const float South = MathHelper.Pi, North = 0f;

        // Against the south wall of a 6 x 6 room at the origin, facing north: its front at z = 1.775
        private static readonly CabinetSpec Chest = new CabinetSpec(CabinetKind.Drawers, new Vector3(0f, 0f, 2f), 180f, 0.9f, 0.45f, 0.85f, 4, Color.Brown);
        private static readonly CabinetSpec Cupboard = Chest with { Kind = CabinetKind.Cupboard, Count = 2 };
        private const float Front = 2f - 0.225f;

        private static BuildingGround Ground(CabinetSpec cabinet) => new BuildingGround(Grounds.Flat(), new[]
        {
            new Building("hut", new RoomSpec
            {
                Id = "hut", Name = "hut",
                Outline = RoomSpec.Rectangle(6f, 6f), Height = 2.6f,
                Floor = Color.Gray, WallA = Color.White, WallB = Color.LightGray, Ceiling = Color.DarkGray,
                WorldOffset = new Vector3(0f, Floor, 0f),
                Cabinets = new[] { cabinet },
            }),
        });

        private static CharacterController Walker(IGround ground, float z, float yaw)
        {
            var walker = new CharacterController(new Vector3(0f, Floor, z), yaw);
            walker.SnapToGround(ground);
            return walker;
        }

        // Ticks the cabinets and the walker together, as the game does.
        private static void Run(BuildingGround ground, CharacterController walker, MoveInput input, float seconds)
        {
            for (var t = 0; t < (int)MathF.Round(seconds / Grounds.Tick); t++)
            {
                ground.StepDoors(Grounds.Tick, Array.Empty<Physics.Body>(), new[] { (walker.Position, CharacterController.Radius, CharacterController.Height) });
                walker.Step(input, Grounds.Tick, ground);
            }
        }

        [Fact]
        public void ACabinetStandsInYourWay()
        {
            var ground = Ground(Chest);
            var walker = Walker(ground, -1f, South);
            Run(ground, walker, Grounds.Forward(), 4f);
            Assert.InRange(walker.Position.Z, Front - CharacterController.Radius - 0.02f, Front - CharacterController.Radius + 0.01f);
        }

        [Fact]
        public void UsingAChestOpensItsDrawersOneByOneThenShutsThemAll()
        {
            var ground = Ground(Chest);
            var walker = Walker(ground, 0.9f, South);
            var chest = ground.Cabinets[0];
            for (var n = 1; n <= 4; n++)
            {
                Assert.Same(chest, ground.Interact(walker.Position, walker.Heading));
                Run(ground, walker, MoveInput.None, 1f);
                for (var i = 0; i < 4; i++)
                    Assert.Equal(i < n ? Chest.Travel : 0f, chest.Drawers[i].Extent, 3);   // the top one first
            }
            ground.Interact(walker.Position, walker.Heading);
            Run(ground, walker, MoveInput.None, 1f);
            Assert.True(chest.IsShut);
        }

        [Fact]
        public void AnOpenDrawerStopsYouShort()
        {
            var ground = Ground(Chest);
            var walker = Walker(ground, 0.9f, South);
            ground.Interact(walker.Position, walker.Heading);
            Run(ground, walker, MoveInput.None, 1f);
            Run(ground, walker, Grounds.Forward(), 2f);
            var stop = Front - Chest.Travel - CharacterController.Radius;
            Assert.InRange(walker.Position.Z, stop - 0.02f, stop + 0.01f);
        }

        [Fact]
        public void ADrawerWontSlideOutThroughYou()
        {
            var ground = Ground(Chest);
            var walker = Walker(ground, Front - CharacterController.Radius - 0.1f, South);   // 10 cm short of it
            ground.Interact(walker.Position, walker.Heading);
            Run(ground, walker, MoveInput.None, 1f);
            Assert.InRange(ground.Cabinets[0].Drawers[0].Extent, 0.05f, 0.101f);   // up to you, and no further
        }

        [Fact]
        public void ACupboardsDoorsSwingOpenTogetherAndShutAgain()
        {
            var ground = Ground(Cupboard);
            var walker = Walker(ground, 0.65f, South);   // within reach, clear of the doors' swing
            var cupboard = ground.Cabinets[0];
            Assert.Equal(2, cupboard.Leaves.Count);
            ground.Interact(walker.Position, walker.Heading);
            Run(ground, walker, MoveInput.None, 1f);
            Assert.All(cupboard.Leaves, leaf => Assert.Equal(Door.MaxOpen, leaf.Angle));
            // Open, each door stands out from the front, square to it
            Assert.All(cupboard.Leaves, leaf => Assert.True(leaf.Tip.Y < Front - 0.4f));

            ground.Interact(walker.Position, walker.Heading);
            Run(ground, walker, MoveInput.None, 1f);
            Assert.True(cupboard.IsShut);
        }

        [Fact]
        public void OnlyTheCabinetInFrontOfYouOpens()
        {
            var ground = Ground(Chest);
            Assert.Null(ground.Interact(new Vector3(0f, Floor, 0.9f), new Vector3(0f, 0f, -1f)));   // facing away
            Assert.Null(ground.Interact(new Vector3(0f, Floor, 2.5f), new Vector3(0f, 0f, -1f)));   // behind it
            Assert.Null(ground.Interact(new Vector3(0f, Floor, -1f), new Vector3(0f, 0f, 1f)));     // too far off
            Assert.True(ground.Cabinets[0].IsShut);
        }

        [Fact]
        public void TheRigSlidesADrawerOutEased()
        {
            var cabinet = new Cabinet(Chest, new Vector3(0f, Floor, 0f));
            var rig = CabinetRig.Build(Chest);
            rig.Solve(cabinet.Placement);
            var shut = rig.World(CabinetRig.Drawer(0)).Translation;

            var drawer = cabinet.Drawers[0];
            drawer.Toggle();
            while (drawer.Fraction < 0.25f)
                drawer.Step(Grounds.Tick, (_, _) => false);
            rig.Reset();
            CabinetRig.Follow(rig, cabinet);
            rig.Solve(cabinet.Placement);
            var moved = rig.World(CabinetRig.Drawer(0)).Translation - shut;
            Assert.Equal(Ease.InOut.Apply(drawer.Fraction) * drawer.Travel, moved.Length(), 3);   // slower than the simulation, at first
            Assert.True(moved.Z < 0f);                                                               // out of its front, northwards
            Assert.Equal(0f, rig.World(CabinetRig.Drawer(1)).Translation.Z - rig.World(CabinetRig.Drawer(0)).Translation.Z + moved.Z, 3);
        }
    }
}
