using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using World.Core.Animation;
using World.Core.Characters;
using Xunit;

namespace World.Core.Tests
{
    // Clip events, fitting limbs, and cut scenes (see Timeline, FittingScene): AnimationPlan.md steps 5 and 6.
    public class SceneTests
    {
        private const float Close = 1e-3f;
        private const float Tick = 1f / 60f;

        private static void Near(Vector3 expected, Vector3 actual, float within = Close) =>
            Assert.True(Vector3.Distance(expected, actual) < within, $"expected {expected}, got {actual}");

        private static List<string> Names(Animator animator) => animator.Fired.Select(f => f.e.Name).ToList();

        // ---- Clip events

        [Fact]
        public void AClipsEventsFireAsItPassesThem()
        {
            var clip = new Clip("drop", duration: 2f).Event(1f, "clunk").Event(0f, "start").Event(2f, "end");
            var animator = new Animator(new Rig());
            animator.Play(clip);

            animator.Step(0.5f);
            Assert.Equal(new[] { "start" }, Names(animator));   // the one at its very start, on the first step
            animator.Step(0.5f);
            Assert.Equal(new[] { "clunk" }, Names(animator));   // one exactly on the step's end counts
            animator.Step(0.5f);
            Assert.Empty(animator.Fired);
            animator.Step(5f);
            Assert.Equal(new[] { "end" }, Names(animator));
            animator.Step(5f);
            Assert.Empty(animator.Fired);                        // held at its end, it passes nothing more
        }

        [Fact]
        public void ALoopingClipsEventsFireEveryTimeRound()
        {
            var clip = new Clip("wheel", loops: true, duration: 1f).Event(0.25f, "tick").Event(0.75f, "tock");
            var animator = new Animator(new Rig());
            animator.Play(clip);
            animator.Step(2.5f);   // round twice and a half, in one step
            Assert.Equal(new[] { "tick", "tock", "tick", "tock", "tick" }, Names(animator));
        }

        // ---- Fitting limbs

        private static Rig Droid()
        {
            var rig = DroidRig.Build();
            rig.Solve(Matrix.CreateTranslation(2f, 0f, 3f));
            return rig;
        }

        [Fact]
        public void ALimbFitsAtASocketAndComesOffAgain()
        {
            var rig = Droid();
            var before = rig.Count;
            var cables = rig.Cables.Count;
            rig.Attach(DroidRig.JointedArm("arm-left"), DroidRig.ShoulderLeft);
            Assert.Equal(before + 4, rig.Count);
            Near(rig.World(DroidRig.ShoulderLeft).Translation, rig.World("arm-left").Translation);
            Assert.True(rig.World("arm-left-hand").Translation.Y < rig.World(DroidRig.ShoulderLeft).Translation.Y - 0.3f);   // hanging down
            Assert.True(rig.World("arm-left-hand").Translation.X > rig.World(DroidRig.Spine).Translation.X);                  // on its left (+X)

            var limb = rig.Detach("arm-left");
            Assert.Equal(before, rig.Count);
            Assert.Equal(cables, rig.Cables.Count);
            Assert.Equal(4, limb.Count);
            Assert.False(rig.Has("arm-left-forearm"));
            Assert.Throws<ArgumentException>(() => { rig.Attach(DroidRig.JointedArm("head"), DroidRig.ShoulderLeft); });   // names must be new
        }

        [Fact]
        public void AllTheWiringToAPartTakenOffGoesWithItOrIsCut()
        {
            var rig = Droid();
            var arm = rig.Detach(DroidRig.Arm);   // the stick arm, and its sporks
            Assert.True(arm.Has(DroidRig.Hand));
            Assert.All(rig.Cables, c => Assert.All(c.Points, p => Assert.True(rig.Has(p.Node))));
            Assert.Contains(arm.Cables, c => c.Name == "to the front spork");   // wholly on the hand: it came off with it
            Assert.DoesNotContain(rig.Cables, c => c.Name == "to the hand");     // ran from the broom: cut
        }

        [Fact]
        public void HungFromSomethingElseAPartStaysWhereItIs()
        {
            var rig = Droid();
            rig.Attach(DroidRig.JointedArm("arm-left", Pose.At(new Vector3(0f, -0.1f, 0f))), DroidRig.Hand);
            var hand = rig.World("arm-left-hand");
            rig.Reparent("arm-left", DroidRig.ShoulderLeft);
            Near(hand.Translation, rig.World("arm-left-hand").Translation);

            // And now it goes where the shoulder goes, not the hand
            rig.Reset();
            rig.Change(DroidRig.Arm, p => p with { Rotation = Pose.Turn(Vector3.UnitX, -1.5f) });
            rig.Solve(Matrix.CreateTranslation(2f, 0f, 3f));
            Near(hand.Translation, rig.World("arm-left-hand").Translation);

            // Parents still come before their children, so it solves in one pass
            for (var i = 0; i < rig.Count; i++)
                Assert.True(rig[i].Parent < i);
            Assert.Throws<ArgumentException>(() => rig.Reparent(DroidRig.Spine, DroidRig.Head));   // not from something hung from it
        }

        // ---- Timelines

        [Fact]
        public void ATimelineRunsItsCuesInOrderAndCanBeSkipped()
        {
            var done = new List<string>();
            var rig = new Rig();
            rig.Add("lid", null, Pose.Identity);
            var animator = new Animator(rig);
            var open = new Clip("open").Event(0.5f, "creak");
            open.Track("lid").Turn(0f, Quaternion.Identity).Turn(1f, Vector3.UnitX, -1f);

            var scene = new Timeline(3f)
                .At(2f, () => done.Add("two"))
                .At(0f, () => done.Add("zero"))
                .Play(1f, animator, open)
                .On("creak", () => done.Add("creak"))
                .At(3f, () => done.Add("end"));

            scene.Step(1.2f);
            Assert.Equal(new[] { "zero" }, done);
            scene.Step(0.4f);
            Assert.Equal(new[] { "zero", "creak" }, done);   // the clip started at 1, so it creaks at 1.5
            Assert.True(scene.Playing);

            scene.Skip();
            Assert.Equal(new[] { "zero", "creak", "two", "end" }, done);
            Assert.True(scene.Finished);
            animator.Apply();
            rig.Solve(Matrix.Identity);
            Assert.True(Quaternion.Dot(Pose.Turn(Vector3.UnitX, -1f), rig["lid"].Pose.Rotation) > 0.9999f);   // it ends open
        }

        [Fact]
        public void TheCameraTrackCutsBetweenTheFreeCameraAndWorldCameras()
        {
            var track = new CameraTrack()
                .Cut(0f, CameraTrack.Free)
                .Key(0f, new Vector3(0f, 1f, 5f), Vector3.Zero, Ease.Linear)
                .Key(2f, new Vector3(4f, 1f, 5f), Vector3.Zero, Ease.Linear)
                .Cut(3f, "drone");
            var drone = new CameraView(new Vector3(9f, 9f, 9f), Vector3.Down, Vector3.Forward);
            CameraView? World(string name) => name == "drone" ? drone : null;

            var halfway = track.ViewAt(1f, World)!.Value;
            Near(new Vector3(2f, 1f, 5f), halfway.Eye);
            Near(Vector3.Normalize(-halfway.Eye), halfway.Forward);
            Assert.Equal(drone, track.ViewAt(4f, World));
            Assert.Null(new CameraTrack().Cut(1f, "mirror").ViewAt(0.5f, World));   // before its first cut
        }

        // ---- The droid fits its second arm

        // The scene played through a tick at a time, the rig posed and solved each tick as a game would
        private static IEnumerable<(Timeline scene, Rig rig)> Play(Matrix placement)
        {
            var rig = DroidRig.Build();
            rig.Solve(placement);
            FittingScene.Hold(rig);
            var animator = new Animator(rig);
            var scene = FittingScene.Build(rig, animator, placement);
            while (scene.Playing)
            {
                scene.Step(Tick);
                animator.Apply();
                rig.Solve(placement);
                yield return (scene, rig);
            }
        }

        [Fact]
        public void TheNewArmMovesSmoothlyFromTheHandIntoItsSocket()
        {
            var placement = Matrix.CreateRotationY(0.7f) * Matrix.CreateTranslation(-3f, 1f, 4f);
            Vector3? last = null;
            var worst = 0f;
            Rig rig = null;
            var attached = false;
            foreach (var (scene, r) in Play(placement))
            {
                rig = r;
                var at = r.World(FittingScene.NewArm).Translation;
                if (last is { } before)
                    worst = MathF.Max(worst, Vector3.Distance(before, at));
                last = at;
                if (!attached && r[r[FittingScene.NewArm].Parent].Name == DroidRig.ShoulderLeft)
                {
                    attached = true;
                    // Let go of near its socket, so easing it in isn't a long slide
                    var gap = Vector3.Distance(at, r.World(DroidRig.ShoulderLeft).Translation);
                    Assert.True(gap < 0.25f, $"let go of {gap:F2} m from the socket");
                }
            }
            Assert.True(attached);
            Assert.True(worst < 0.02f, $"it jumped {worst:F3} m in a tick");   // no more than 1.2 m/s
            Near(rig.World(DroidRig.ShoulderLeft).Translation, rig.World(FittingScene.NewArm).Translation);
        }

        [Fact]
        public void SkippedTheSceneEndsAsIfPlayed()
        {
            var placement = Matrix.CreateTranslation(1f, 0f, 1f);
            var rig = DroidRig.Build();
            rig.Solve(placement);
            FittingScene.Hold(rig);
            var animator = new Animator(rig);
            var scene = FittingScene.Build(rig, animator, placement);
            scene.Step(Tick);
            scene.Skip();

            Assert.Equal(DroidRig.ShoulderLeft, rig[rig[FittingScene.NewArm].Parent].Name);
            // Fitted for good: at rest, with no clips left on it, it's in its socket
            animator.Step(Tick);
            Assert.DoesNotContain(animator.Playing, p => p.Weight > 0f);
            animator.Apply();
            rig.Solve(placement);
            Near(rig.World(DroidRig.ShoulderLeft).Translation, rig.World(FittingScene.NewArm).Translation);
            var untouched = DroidRig.Build();
            untouched.Solve(placement);
            Near(untouched.World(DroidRig.Hand).Translation, rig.World(DroidRig.Hand).Translation);   // the stick arm back at its side
        }

        [Fact]
        public void TheSceneIsSeenFromTheFreeCameraThenTheHeadCameraThenTheDrone()
        {
            var rig = DroidRig.Build();
            FittingScene.Hold(rig);
            var scene = FittingScene.Build(rig, new Animator(rig), Matrix.Identity);
            Assert.Equal(CameraTrack.Free, scene.Camera.CameraAt(0f));
            Assert.Equal(FittingScene.HeadCamera, scene.Camera.CameraAt(4f));
            Assert.Equal(FittingScene.DroneCamera, scene.Camera.CameraAt(7f));
        }
    }
}
