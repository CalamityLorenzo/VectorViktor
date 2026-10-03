using Microsoft.Xna.Framework;
using System;
using System.Linq;
using World.Core.Animation;
using World.Core.Characters;
using World.Core.Movement;
using Xunit;

namespace World.Core.Tests
{
    // The droid's ways of getting about (see Locomotion): what each can't do (its Gait, in the controller), its rig
    // (see DroidBases) and how the rig follows it (see DroidMotion).
    public class LocomotionTests
    {
        private static CharacterController Droid(Locomotion locomotion, IGround ground, Vector3 at, float yaw = 0f)
        {
            var body = new CharacterController(at, yaw) { Gait = Locomotions.GaitOf(locomotion) };
            body.SnapToGround(ground);
            return body;
        }

        public static TheoryData<Locomotion> Bases() => new TheoryData<Locomotion>(Locomotions.All);
        public static TheoryData<Locomotion> Movers() => new TheoryData<Locomotion>(Locomotions.Movers);

        // ---- What none of them can do

        [Theory]
        [MemberData(nameof(Bases))]
        public void NoneOfThemGoesSideways(Locomotion locomotion)
        {
            var ground = Grounds.Flat();
            var droid = Droid(locomotion, ground, Vector3.Zero);
            Grounds.Run(droid, new MoveInput(new Vector2(1f, 0f)), 1f, ground);
            Assert.True(droid.Position.Length() < 0.001f, $"went sideways to {droid.Position}");
        }

        [Theory]
        [MemberData(nameof(Bases))]
        public void NoneOfThemJumps(Locomotion locomotion)
        {
            var ground = Grounds.Flat();
            var droid = Droid(locomotion, ground, Vector3.Zero);
            Grounds.Run(droid, new MoveInput(Vector2.Zero, Jump: true), 0.5f, ground, d => Assert.True(d.Grounded));
        }

        [Fact]
        public void TheChurnGoesNowhere()
        {
            var ground = Grounds.Flat();
            var droid = Droid(Locomotion.Churn, ground, Vector3.Zero, 0.3f);
            Grounds.Run(droid, new MoveInput(new Vector2(0.5f, 1f), Turn: 1f, Run: true), 2f, ground);
            Grounds.Run(droid, new MoveInput(new Vector2(0f, -1f), Turn: -1f), 2f, ground);
            Assert.True(droid.Position.Length() < 0.001f, $"went to {droid.Position}");
            Assert.Equal(0.3f, droid.Yaw, 4);
        }

        [Fact]
        public void TheWalkerStillDoesBoth()
        {
            var ground = Grounds.Flat();
            var walker = new CharacterController(Vector3.Zero);
            Grounds.Run(walker, new MoveInput(new Vector2(1f, 0f)), 1f, ground);
            Assert.True(walker.Position.X > 2f);
            walker.Step(new MoveInput(Vector2.Zero, Jump: true), Grounds.Tick, ground);
            Assert.False(walker.Grounded);
        }

        // ---- Steps

        // A kerb `height` high east of x = 0, driven at from the west
        [Theory]
        [InlineData(Locomotion.Segway, 0.05f, true)]
        [InlineData(Locomotion.Segway, 0.12f, false)]
        [InlineData(Locomotion.Tracks, 0.12f, true)]
        [InlineData(Locomotion.Tracks, 0.18f, false)]
        [InlineData(Locomotion.TriStar, 0.18f, true)]
        [InlineData(Locomotion.TriStar, 0.25f, false)]
        public void EachGetsUpAStepNoHigherThanItsOwn(Locomotion locomotion, float height, bool getsUp)
        {
            var ground = new StepGround(height);
            var droid = Droid(locomotion, ground, new Vector3(-1.5f, 0f, 0f), Grounds.East);
            Grounds.Run(droid, Grounds.Forward(), 3f, ground);
            if (getsUp)
                Assert.True(droid.Position.X > 0.5f && MathF.Abs(droid.Position.Y - height) < 0.001f, $"stopped at {droid.Position}");
            else
                Assert.True(droid.Position.X < 0f && droid.Position.Y == 0f, $"got to {droid.Position}");
        }

        [Fact]
        public void AStepTooHighStopsItsFrontNotItsMiddle()
        {
            var ground = new StepGround(0.2f);
            var droid = Droid(Locomotion.Tracks, ground, new Vector3(-2f, 0f, 0f), Grounds.East);
            Grounds.Run(droid, Grounds.Forward(), 3f, ground);
            var front = droid.Position.X + droid.Gait.Radius;
            Assert.InRange(front, -0.12f, 0.001f);
        }

        // Stairs walked as a slope (as a building's are) with steps `riser` high: a slope rising north from z = -1, as
        // steep as the stairs are, at 0.3 m a step
        private sealed class Stairs : IGround
        {
            public Stairs(float riser) => Riser = riser;
            public float Riser { get; }
            private float Height(Vector3 p) => Math.Clamp(-p.Z - 1f, 0f, 3f) * Riser / 0.3f;
            public float? GroundBelow(Vector3 feet, float reach) => Height(feet);
            public Vector3 NormalAt(Vector3 feet) => Vector3.Up;
            public bool IsWalkable(Vector3 feet) => true;
            public float RiserAt(Vector3 feet) => -feet.Z > 1f && -feet.Z < 4f ? Riser : 0f;
        }

        [Theory]
        [InlineData(Locomotion.Segway, false)]
        [InlineData(Locomotion.Tracks, false)]
        [InlineData(Locomotion.TriStar, true)]
        public void OnlyTheTriStarGetsUpStairs(Locomotion locomotion, bool getsUp)
        {
            var ground = new Stairs(0.18f);
            var droid = Droid(locomotion, ground, Vector3.Zero);
            Grounds.Run(droid, Grounds.Forward(), 5f, ground);
            Assert.Equal(getsUp, droid.Position.Y > 1f);
        }

        // ---- Pushing

        // Each pushes harder than the one before (Segway, tri-star, tracks), so each shifts the workshop's crate for it and
        // the lighter ones, and not the heavier
        public static TheoryData<Locomotion, int> BasesAndCrates()
        {
            var pairs = new TheoryData<Locomotion, int>();
            foreach (var locomotion in Locomotions.All)
                for (var k = 0; k < global::Maps.Home.Workshop.Crates.Length; k++)
                    pairs.Add(locomotion, k);
            return pairs;
        }

        [Theory]
        [MemberData(nameof(BasesAndCrates))]
        public void EachShiftsTheCratesNoHeavierThanItsOwn(Locomotion locomotion, int crate)
        {
            var (mass, _, size) = global::Maps.Home.Workshop.Crates[crate];
            var world = new Physics.PhysicsWorld(Grounds.Flat());
            var box = world.Add(new Physics.Body("crate", new Vector3(size), mass, Vector3.Zero));
            var droid = new Player(new Vector3(-2f, 0f, 0f), Grounds.East, world);
            droid.Body.Gait = Locomotions.GaitOf(locomotion);
            for (var t = 0; t < 4f / Grounds.Tick; t++)
            {
                droid.Step(Grounds.Forward(), Grounds.Tick, world);
                world.Step(Grounds.Tick);
            }
            var shifts = Locomotions.IndexOf(locomotion) - Locomotions.IndexOf(Locomotion.Segway) >= crate;   // the churn, nothing
            if (shifts)
                Assert.True(box.Position.X > 0.3f, $"only pushed it {box.Position.X} m");
            else
                Assert.True(MathF.Abs(box.Position.X) < 0.01f, $"pushed it {box.Position.X} m");
        }

        [Fact]
        public void EachIsHeavierAndPushesHarderThanTheOneBefore()
        {
            for (var k = 1; k < Locomotions.All.Count; k++)
            {
                var (weaker, stronger) = (Locomotions.GaitOf(Locomotions.All[k - 1]), Locomotions.GaitOf(Locomotions.All[k]));
                Assert.True(stronger.Mass > weaker.Mass && stronger.PushForce > weaker.PushForce && stronger.PushPower > weaker.PushPower,
                    $"{stronger.Name} isn't stronger than {weaker.Name}");
            }
        }

        // ---- The rigs

        [Theory]
        [MemberData(nameof(Bases))]
        public void EachStandsTheBroomOnItsHub(Locomotion locomotion)
        {
            var rig = DroidRig.Build(locomotion);
            rig.Solve(Matrix.Identity);
            var hub = DroidBases.HubHeight(locomotion);
            var upright = locomotion == Locomotion.Churn ? MathF.Cos(DroidBases.ChurnLean) : 1f;   // in the churn, leaning
            Assert.Equal(hub, rig.World(DroidRig.Axle).Translation.Y, 4);
            Assert.Equal(hub + DroidRig.SpineLength * upright, rig.World(DroidRig.Head).Translation.Y, 4);
            Assert.Equal(DroidRig.HeightOn(locomotion), Locomotions.GaitOf(locomotion).Height);
        }

        // The broom stands on the churn's bottom, leaning on the rim of its mouth, and its head hangs out past the churn's
        // side, the way it leans
        [Fact]
        public void InTheChurnTheBroomLeansOnTheRimAndItsHeadHangsOut()
        {
            var rig = DroidRig.Build(Locomotion.Churn);
            rig.Solve(Matrix.Identity);
            var foot = rig.World(DroidRig.Spine).Translation;
            var up = Vector3.Normalize(rig.World(DroidRig.Head).Translation - foot);
            var atRim = foot + up * ((DroidBases.ChurnHeight - foot.Y) / up.Y);
            var fromMiddle = new Vector2(atRim.X, atRim.Z).Length();
            Assert.InRange(fromMiddle + DroidRig.SpineRadius / up.Y, DroidBases.ChurnMouthRadius - 0.002f, DroidBases.ChurnMouthRadius + 0.002f);
            Assert.True(new Vector2(foot.X, foot.Z).Length() < DroidBases.ChurnRadius - 0.05f, $"its foot's at {foot}");
            var head = rig.World(DroidRig.Head).Translation + up * (DroidRig.NeckHeight + DroidRig.HeadHeight / 2f);   // its middle
            Assert.True(Vector3.Dot(head, DroidBases.ChurnLeanTowards) > DroidBases.ChurnRadius, $"its head's at {head}");
        }

        // In the churn, the arm and its sporks hang clear of it: out past the rim, the shoulder and the body, and below the
        // shoulder past the handles too
        [Fact]
        public void InTheChurnTheArmHangsClearOfIt()
        {
            var rig = DroidRig.Build(Locomotion.Churn);
            rig.Solve(Matrix.Identity);
            var arm = rig.World(DroidRig.Arm);
            var (from, down) = (arm.Translation, Vector3.TransformNormal(-Vector3.UnitY, arm));
            const float handles = DroidBases.ChurnRadius + 0.045f, shoulder = 0.32f;
            for (var d = 0f; d <= DroidRig.ArmLength + DroidRig.HandLength; d += 0.02f)
            {
                var p = from + down * d;
                if (p.Y > DroidBases.ChurnHeight + DroidRig.ArmRadius)
                    continue;
                var clear = (p.Y < shoulder ? handles : DroidBases.ChurnRadius) + DroidRig.ArmRadius;
                Assert.True(new Vector2(p.X, p.Z).Length() > clear, $"{d:0.00} m down the arm, at {p}, it's in the churn");
            }
            Assert.True(from.Y + down.Y * (DroidRig.ArmLength + DroidRig.HandLength) > 0.02f, "its sporks are in the ground");
        }

        [Fact]
        public void TheChurnIsJustOverAThirdAsTallAsTheDroidOnItsWheels()
        {
            var third = DroidRig.HeightOn(Locomotion.Segway) / 3f;
            Assert.InRange(DroidBases.ChurnHeight, third, third * 1.1f);
        }

        [Fact]
        public void TheSegwaysLeftWheelIsOnItsLeft()
        {
            var rig = DroidRig.Build();
            rig.Solve(Matrix.Identity);
            Assert.True(rig.World(DroidRig.WheelLeft).Translation.X > 0f);   // facing +Z, its left is +X
            Assert.True(rig.World(DroidRig.EarLeft).Translation.X > 0f);
        }

        [Fact]
        public void ATanksLowestShoesAreOnTheGround()
        {
            var lowest = Enumerable.Range(0, DroidBases.Shoes).Min(k => DroidBases.ShoeAt(k, 0.37f).Translation.Y);
            Assert.Equal(DroidBases.ShoeThickness / 2f, lowest, 4);
        }

        [Fact]
        public void RunningATrackOnByAShoeMovesEachShoeToTheNextOnesPlace()
        {
            var pitch = DroidBases.PathLength / DroidBases.Shoes;
            for (var k = 0; k < DroidBases.Shoes; k++)
            {
                var moved = DroidBases.ShoeAt(k, pitch);
                var next = DroidBases.ShoeAt((k + 1) % DroidBases.Shoes, 0f);
                Assert.True(Vector3.Distance(moved.Translation, next.Translation) < 1e-4f, $"shoe {k}: {moved.Translation} not {next.Translation}");
            }
        }

        [Fact]
        public void RollingForwardTheBottomRunStaysPutOnTheGround()
        {
            // A shoe in the middle of the bottom run, and the hull rolled forward (+Z) a little: the shoe has gone back
            // along the hull just as far, so it hasn't moved over the ground
            var k = Enumerable.Range(0, DroidBases.Shoes).OrderBy(n => MathF.Abs(DroidBases.ShoeAt(n, 0f).Translation.Z) + DroidBases.ShoeAt(n, 0f).Translation.Y).First();
            var before = DroidBases.ShoeAt(k, 0f).Translation;
            var after = DroidBases.ShoeAt(k, 0.05f).Translation + new Vector3(0f, 0f, 0.05f);
            Assert.True(Vector3.Distance(before, after) < 1e-4f, $"slipped from {before} to {after}");
        }

        [Fact]
        public void ATriStarStandsOnTwoWheelsEachSide()
        {
            var rig = DroidRig.Build(Locomotion.TriStar);
            rig.Solve(Matrix.Identity);
            foreach (var spider in new[] { DroidBases.SpiderLeft, DroidBases.SpiderRight })
            {
                var bottoms = Enumerable.Range(0, 3).Select(k => rig.World($"{spider}-wheel-{k}").Translation.Y - DroidBases.SpiderWheelRadius).OrderBy(y => y).ToArray();
                Assert.Equal(0f, bottoms[0], 4);
                Assert.Equal(0f, bottoms[1], 4);
                Assert.True(bottoms[2] > 0.1f);
            }
        }

        // ---- How the rig follows it

        [Fact]
        public void ATriStarsSpidersTipOverAThirdOfATurnForEachStep()
        {
            var ground = new StepGround(0.18f);
            var droid = Droid(Locomotion.TriStar, ground, new Vector3(-1f, 0f, 0f), Grounds.East);
            var motion = new DroidMotion(Locomotion.TriStar);
            var lowest = 0f;
            Grounds.Run(droid, Grounds.Forward(), 1.5f, ground, d => { motion.Follow(d, Grounds.Tick, ground); lowest = MathF.Min(lowest, motion.Heave); });
            Grounds.Run(droid, MoveInput.None, 1f, ground, d => motion.Follow(d, Grounds.Tick, ground));
            Assert.Equal(0.18f, droid.Position.Y, 3);
            Assert.True(lowest < -0.1f, "the rig went up the step with its feet, all at once");
            Assert.Equal(0f, motion.Heave, 4);
            Assert.Equal(DroidBases.ClusterStep, motion.ClusterTurn, 3);
        }

        [Fact]
        public void ATanksHullNosesUpOntoAKerbAndLevelsOnTop()
        {
            var ground = new StepGround(0.12f);
            var droid = Droid(Locomotion.Tracks, ground, new Vector3(-1.5f, 0f, 0f), Grounds.East);
            var motion = new DroidMotion(Locomotion.Tracks);
            var most = 0f;
            Grounds.Run(droid, Grounds.Forward(), 2.5f, ground, d => { motion.Follow(d, Grounds.Tick, ground); most = MathF.Max(most, motion.Pitch); });
            Grounds.Run(droid, MoveInput.None, 1f, ground, d => motion.Follow(d, Grounds.Tick, ground));
            Assert.True(droid.Position.X > 0.5f, $"didn't get up it: {droid.Position}");
            Assert.True(most > 0.1f, $"nosed up only {most} rad");
            Assert.InRange(motion.Pitch, -0.001f, 0.001f);
            Assert.InRange(motion.Heave, -0.001f, 0.001f);
        }

        [Fact]
        public void ATanksTurntableTurnsWhenAskedAndOnlyATanks()
        {
            var ground = Grounds.Flat();
            foreach (var locomotion in Locomotions.All)
            {
                var droid = Droid(locomotion, ground, Vector3.Zero);
                var motion = new DroidMotion(locomotion);
                Grounds.Run(droid, MoveInput.None, 1f, ground, d => motion.Follow(d, Grounds.Tick, ground, turretTurn: 1f));
                Assert.Equal(locomotion == Locomotion.Tracks ? DroidBases.TurretSpeed : 0f, motion.Turret, 2);
            }
        }

        [Fact]
        public void TracksTurningOnTheSpotRunOppositeWays()
        {
            var motion = new DroidMotion(Locomotion.Tracks);
            var yaw = 0f;
            for (var t = 0; t < 60; t++)
            {
                yaw += MathHelper.PiOver2 * Grounds.Tick;
                motion.Follow(Vector3.Zero, yaw, Grounds.Tick);
            }
            var quarter = MathHelper.PiOver2 * DroidBases.TrackGauge / 2f;
            Assert.InRange(motion.LeftRolled, quarter * 0.97f, quarter * 1.02f);
            Assert.InRange(motion.RightRolled, -quarter * 1.02f, -quarter * 0.97f);
        }
    }
}
