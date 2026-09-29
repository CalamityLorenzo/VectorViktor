using Microsoft.Xna.Framework;
using System;
using System.Linq;
using World.Core.Animation;
using World.Core.Characters;
using Xunit;

namespace World.Core.Tests
{
    // Rigs, keyframed clips and the animator (see World.Core.Animation), and the droid's rig (see DroidRig).
    public class AnimationTests
    {
        private const float Close = 1e-4f;

        private static void Near(Vector3 expected, Vector3 actual, float within = Close) =>
            Assert.True(Vector3.Distance(expected, actual) < within, $"expected {expected}, got {actual}");

        private static void Near(float expected, float actual, float within = Close) =>
            Assert.True(MathF.Abs(expected - actual) < within, $"expected {expected}, got {actual}");

        // The angle between two turns, however they're signed
        private static float Apart(Quaternion a, Quaternion b) => 2f * MathF.Acos(MathF.Min(1f, MathF.Abs(Quaternion.Dot(a, b))));

        // ---- Poses and keys

        [Fact]
        public void APoseSizesThenTurnsThenMoves()
        {
            var pose = new Pose(new Vector3(5f, 0f, 0f), Pose.Turn(Vector3.Up, MathHelper.PiOver2), new Vector3(2f));
            // (1, 0, 0) doubled, turned a quarter to the left (onto -Z), then moved 5 along X
            Near(new Vector3(5f, 0f, -2f), Vector3.Transform(Vector3.UnitX, pose.Matrix));
        }

        [Fact]
        public void AChannelHoldsItsEndsAndFillsBetweenThem()
        {
            var channel = new Channel<Vector3>(Vector3.Lerp);
            channel.Add(1f, Vector3.Zero, Ease.Linear);
            channel.Add(3f, new Vector3(4f, 0f, 0f), Ease.Linear);
            Near(Vector3.Zero, channel.Sample(0f));
            Near(new Vector3(1f, 0f, 0f), channel.Sample(1.5f));
            Near(new Vector3(4f, 0f, 0f), channel.Sample(10f));
            Assert.Equal(3f, channel.Duration);
        }

        [Fact]
        public void EasingChangesTheWayNotTheEnds()
        {
            foreach (var ease in Enum.GetValues<Ease>())
            {
                Near(0f, ease.Apply(0f));
                Near(1f, ease.Apply(1f));
            }
            Near(0.5f, Ease.InOut.Apply(0.5f));
            Assert.True(Ease.In.Apply(0.25f) < 0.25f);     // slow to start
            Assert.True(Ease.Out.Apply(0.25f) > 0.25f);    // quick to start
            Assert.Equal(0f, Ease.Step.Apply(0.99f));      // nothing until it's time
        }

        [Fact]
        public void AStepKeyJumpsWhenItsTimeComes()
        {
            var channel = new Channel<float>((a, b, t) => a + (b - a) * t);
            channel.Add(0f, 0f, Ease.Linear);
            channel.Add(2f, 1f, Ease.Step);
            Assert.Equal(0f, channel.Sample(1.99f));
            Assert.Equal(1f, channel.Sample(2f));
        }

        [Fact]
        public void KeysMustGoInInTimeOrder()
        {
            var channel = new Channel<Vector3>(Vector3.Lerp);
            channel.Add(1f, Vector3.Zero, Ease.Linear);
            channel.Add(1f, Vector3.One, Ease.Linear);   // the same time: a jump, allowed
            Assert.Throws<ArgumentException>(() => channel.Add(0.5f, Vector3.One, Ease.Linear));
        }

        [Fact]
        public void TurnsBetweenKeysGoTheShortWayRound()
        {
            // From 170 degrees one way to 170 the other: 20 degrees through the back, not 340 through the front
            var track = new Track("x")
                .Turn(0f, Vector3.Up, MathHelper.ToRadians(170f), Ease.Linear)
                .Turn(1f, Vector3.Up, MathHelper.ToRadians(-170f), Ease.Linear);
            var half = track.Sample(0.5f, Pose.Identity).Rotation;
            Near(0f, Apart(half, Pose.Turn(Vector3.Up, MathHelper.Pi)), 1e-3f);
        }

        [Fact]
        public void ATrackLeavesWhatItDoesntKeyAsItWas()
        {
            var under = Pose.At(new Vector3(1f, 2f, 3f));
            var track = new Track("lid").Turn(0f, Vector3.UnitX, 0f).Turn(1f, Vector3.UnitX, -1f);
            var posed = track.Sample(1f, under);
            Near(under.Translation, posed.Translation);
            Near(0f, Apart(Pose.Turn(Vector3.UnitX, -1f), posed.Rotation));
        }

        // ---- Clips

        // A drawer sliding 0.4 out over 2 s
        private static Clip Slide(bool loops)
        {
            var clip = new Clip("slide", loops);
            clip.Track("drawer").Move(0f, Vector3.Zero, Ease.Linear).Move(2f, new Vector3(0f, 0f, 0.4f), Ease.Linear);
            return clip;
        }

        [Fact]
        public void AClipThatLoopsComesRoundAgainAndOneThatDoesntHoldsItsEnd()
        {
            Near(0.5f, Slide(loops: true).Local(2.5f));
            Near(2f, Slide(loops: false).Local(2.5f));
            Near(1.5f, Slide(loops: true).Local(-0.5f));
        }

        [Fact]
        public void AClipPosesTheNamedPartsAndSkipsTheOnesARigHasnt()
        {
            var rig = new Rig();
            rig.Add("carcass", null, Pose.Identity);
            rig.Add("drawer", "carcass", Pose.At(new Vector3(0f, 0.5f, 0f)));
            var clip = Slide(loops: false);
            clip.Track("handle").Move(0f, Vector3.One);   // no handle on this one

            clip.Apply(rig, 1f);
            Near(new Vector3(0f, 0f, 0.2f), rig["drawer"].Pose.Translation);
        }

        [Fact]
        public void AWeightedClipGoesOnlyPartWay()
        {
            var rig = new Rig();
            rig.Add("drawer", null, Pose.Identity);
            Slide(loops: false).Apply(rig, 2f, weight: 0.25f);
            Near(new Vector3(0f, 0f, 0.1f), rig["drawer"].Pose.Translation);
        }

        // ---- Rigs

        [Fact]
        public void APartMovesWithWhatItHangsFrom()
        {
            var rig = new Rig();
            rig.Add("door", null, Pose.At(new Vector3(10f, 0f, 0f)));
            rig.Add("handle", "door", Pose.At(new Vector3(0.8f, 1f, 0f)));
            rig.Change("door", p => p with { Rotation = Pose.Turn(Vector3.Up, MathHelper.PiOver2) });
            rig.Solve(Matrix.CreateTranslation(0f, 5f, 0f));
            // The handle, 0.8 along the door, swung a quarter turn left about its hinge, on the door, placed 5 up
            Near(new Vector3(10f, 6f, -0.8f), rig.World("handle").Translation);
        }

        [Fact]
        public void APartsParentMustComeFirstAndNamesAreOnlyUsedOnce()
        {
            var rig = new Rig();
            rig.Add("a", null, Pose.Identity);
            Assert.Throws<ArgumentException>(() => rig.Add("b", "nothing yet", Pose.Identity));
            Assert.Throws<ArgumentException>(() => rig.Add("a", null, Pose.Identity));
        }

        [Fact]
        public void ResetPutsEveryPartBackWhereItRests()
        {
            var rig = new Rig();
            rig.Add("a", null, Pose.At(Vector3.One));
            rig.Change("a", p => p with { Translation = Vector3.Zero });
            rig.Reset();
            Near(Vector3.One, rig["a"].Pose.Translation);
        }

        // ---- Cables

        [Fact]
        public void ACableStretchesFromOnePartToTheNextHoweverTheyMove()
        {
            var rig = new Rig();
            rig.Add("post", null, Pose.Identity);
            rig.Add("gate", "post", Pose.At(new Vector3(1f, 0f, 0f)));
            var cable = rig.AddCable(new Cable("wire", 0.01f, new[]
            {
                new CablePoint("post", new Vector3(0f, 1f, 0f)),
                new CablePoint("gate", new Vector3(0.5f, 1f, 0f)),
            }));
            rig.Change("gate", p => p with { Rotation = Pose.Turn(Vector3.Up, MathHelper.PiOver2) });   // swung open
            rig.Solve(Matrix.Identity);

            var span = rig.Span(cable, 0);
            Near(new Vector3(0f, 1f, 0f), Vector3.Transform(Vector3.Zero, span));        // the unit cable's start at one end
            Near(new Vector3(1f, 1f, -0.5f), Vector3.Transform(Vector3.UnitZ, span));    // and its end at the other, on the gate as it's swung
            Near(0.01f, Vector3.TransformNormal(Vector3.UnitX, span).Length());          // and as thick as the cable
        }

        [Fact]
        public void ACableMustBeFixedToPartsTheRigHas()
        {
            var rig = new Rig();
            rig.Add("post", null, Pose.Identity);
            Assert.Throws<ArgumentException>(() => rig.AddCable(new Cable("wire", 0.01f, new[]
            {
                new CablePoint("post", Vector3.Zero), new CablePoint("gate", Vector3.One),
            })));
        }

        // ---- The animator

        private static (Animator animator, Rig rig) Drawer()
        {
            var rig = new Rig();
            rig.Add("drawer", null, Pose.Identity);
            return (new Animator(rig), rig);
        }

        [Fact]
        public void AClipFadesInOverTheTimeGiven()
        {
            var (animator, rig) = Drawer();
            var player = animator.Play(Slide(loops: false), fadeIn: 1f);
            Near(0f, player.Weight);
            animator.Step(0.5f);
            Near(0.5f, player.Weight);
            animator.Step(1.5f);
            Near(1f, player.Weight);
            Assert.True(player.Finished);

            animator.Apply();
            Near(new Vector3(0f, 0f, 0.4f), rig["drawer"].Pose.Translation);   // stays open
        }

        [Fact]
        public void AStoppedClipFadesOutThenLetsGo()
        {
            var (animator, rig) = Drawer();
            var player = animator.Play(Slide(loops: true));
            animator.Step(1f);
            animator.Stop(player, fadeOut: 0.5f);
            animator.Step(0.25f);
            Assert.True(animator.IsPlaying("slide"));
            animator.Step(0.25f);
            Assert.False(animator.IsPlaying("slide"));
            Assert.Empty(animator.Playing);

            animator.Apply();
            Near(Vector3.Zero, rig["drawer"].Pose.Translation);   // back where it rests
        }

        [Fact]
        public void ALaterClipIsLaidOverAnEarlierOne()
        {
            var (animator, rig) = Drawer();
            animator.Play(Slide(loops: false));
            var shut = new Clip("shut");
            shut.Track("drawer").Move(0f, Vector3.Zero);
            animator.Play(shut);
            animator.Step(2f);
            animator.Apply();
            Near(Vector3.Zero, rig["drawer"].Pose.Translation);
        }

        // ---- The droid

        private static Rig Droid()
        {
            var rig = DroidRig.Build();
            rig.Solve(Matrix.Identity);
            return rig;
        }

        [Fact]
        public void TheDroidStandsOnItsWheelsWithItsHeadOnTop()
        {
            var rig = Droid();
            Near(DroidRig.WheelRadius, rig.World(DroidRig.WheelLeft).Translation.Y);
            Near(DroidRig.WheelRadius + DroidRig.SpineLength, rig.World(DroidRig.Head).Translation.Y);
            Assert.InRange(DroidRig.Height, 1.3f, 1.5f);
            Assert.True(rig.World(DroidRig.EarLeft).Translation.X > 0f, "its left is +X, facing +Z");
            Assert.True(rig.World(DroidRig.Hand).Translation.X < 0f, "its arm is on its right");
            Assert.True(rig.World(DroidRig.Hand).Translation.Z > 0f, "its arm hangs a little forward");
        }

        [Fact]
        public void TheHeadCameraRunsRoundTheVisorWithoutTheHeadTurning()
        {
            var rig = Droid();
            var (ahead, forward, _) = DroidRig.CameraView(rig);
            Near(Vector3.UnitZ, forward);
            Near(DroidRig.VisorRadius, ahead.Z);

            DroidRig.Look(rig, MathHelper.Pi);   // right round to the back
            rig.Solve(Matrix.Identity);
            var (behind, backward, _) = DroidRig.CameraView(rig);
            Near(-Vector3.UnitZ, backward);
            Near(-DroidRig.VisorRadius, behind.Z);
            Near(ahead.Y, behind.Y);                                                    // along the rail, level
            Near(Vector3.UnitZ, Vector3.TransformNormal(Vector3.UnitZ, rig.World(DroidRig.Head)));   // the head hasn't moved

            DroidRig.Look(rig, MathHelper.PiOver2);   // round to its left
            rig.Solve(Matrix.Identity);
            Near(Vector3.UnitX, DroidRig.CameraView(rig).forward);
        }

        [Fact]
        public void TheHeadCameraLooksUpAndDown()
        {
            var rig = Droid();
            DroidRig.Look(rig, 0f, up: 0.5f);
            rig.Solve(Matrix.Identity);
            var forward = DroidRig.CameraView(rig).forward;
            Near(MathF.Sin(0.5f), forward.Y);
            Assert.True(forward.Z > 0f);
        }

        [Fact]
        public void TheWheelsTurnAsFarRoundAsItRolls()
        {
            var rig = Droid();
            DroidRig.Roll(rig, MathHelper.Pi * DroidRig.WheelRadius);   // half a wheel's rim
            rig.Solve(Matrix.Identity);
            // The top of the wheel is now at the bottom
            var top = Vector3.Transform(new Vector3(0f, DroidRig.WheelRadius, 0f), rig.World(DroidRig.WheelLeft));
            Near(0f, top.Y);

            // A little way forward, and the top of the wheel moves forward
            DroidRig.Roll(rig, 0.01f);
            rig.Solve(Matrix.Identity);
            top = Vector3.Transform(new Vector3(0f, DroidRig.WheelRadius, 0f), rig.World(DroidRig.WheelLeft));
            Assert.True(top.Z > 0f);
        }

        [Fact]
        public void LeaningForwardTipsTheHeadForwardAndLeavesTheWheels()
        {
            var rig = Droid();
            var wheel = rig.World(DroidRig.WheelLeft).Translation;
            DroidRig.Tilt(rig, 0.2f);
            rig.Solve(Matrix.Identity);
            Assert.True(rig.World(DroidRig.Head).Translation.Z > 0.1f);
            Near(wheel, rig.World(DroidRig.WheelLeft).Translation);
        }

        [Fact]
        public void TheEarDishesWanderSmoothly()
        {
            var rig = DroidRig.Build();
            Quaternion At(float t)
            {
                rig.Reset();
                DroidRig.Listen(rig, t);
                return rig[DroidRig.DishLeft].Pose.Rotation;
            }
            var moved = 0f;
            var previous = At(0f);
            for (var t = 1f / 60f; t < 60f; t += 1f / 60f)
            {
                var now = At(t);
                Assert.True(Apart(previous, now) < 0.1f, $"a jump at {t}s");
                moved = MathF.Max(moved, Apart(At(0f), now));
                previous = now;
            }
            Assert.True(moved > 0.5f, "they hardly moved");
            Assert.NotEqual(rig[DroidRig.DishLeft].Pose.Rotation, rig[DroidRig.DishRight].Pose.Rotation);
        }

        // Every cable runs from the head, or on from a part another ends at (through the servo, the hub), and stays
        // joined without stretching far however the droid moves: its longest stretch, rolling, leaning and waving, is
        // no more than a hand's width.
        [Fact]
        public void TheDroidsCablesRunFromItsHeadAndStayJoined()
        {
            var rig = DroidRig.Build();
            Assert.Contains(rig.Cables, c => c.Points[^1].Node == DroidRig.Hand);
            Assert.Contains(rig.Cables, c => c.Points[^1].Node == DroidRig.Axle);
            foreach (var cable in rig.Cables)
                Assert.True(cable.Points[0].Node == DroidRig.Head || rig.Cables.Any(c => c != cable && c.Points[^1].Node == cable.Points[0].Node),
                    $"'{cable.Name}' comes from nowhere");

            var wave = DroidRig.Wave();
            var longest = 0f;
            for (var t = 0f; t < wave.Duration; t += 0.1f)
            {
                rig.Reset();
                wave.Apply(rig, t);
                DroidRig.Tilt(rig, 0.2f * MathF.Sin(t));
                DroidRig.Roll(rig, t);
                rig.Solve(Matrix.Identity);
                foreach (var cable in rig.Cables)
                    for (var k = 0; k < cable.Stretches; k++)
                        longest = MathF.Max(longest, Vector3.TransformNormal(Vector3.UnitZ, rig.Span(cable, k)).Length());
            }
            Assert.InRange(longest, 0.01f, 0.12f);
        }

        [Fact]
        public void TheWaveLiftsTheArmAndPutsItBack()
        {
            var rig = DroidRig.Build();
            var wave = DroidRig.Wave();
            float HandHeight(float t)
            {
                rig.Reset();
                wave.Apply(rig, t);
                rig.Solve(Matrix.Identity);
                return rig.World(DroidRig.Hand).Translation.Y;
            }
            var hanging = HandHeight(0f);
            Assert.True(HandHeight(2f) > rig.World(DroidRig.Shoulder).Translation.Y, "the fork is up above the shoulder");
            Near(hanging, HandHeight(6f));                   // resting again
            Near(HandHeight(1f), HandHeight(1f + wave.Duration));   // and round again
        }
    }
}
