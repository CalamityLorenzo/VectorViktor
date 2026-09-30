using Microsoft.Xna.Framework;
using World.Core.Animation;

namespace World.Core.Characters
{
    // The droid's first cut scene (AnimationPlan.md 8): it fits itself a second arm. It starts with the new arm held in
    // its sporks (Hold). It lifts it across its chest to its left side; at the "attach arm" moment the arm is let go of
    // there, hung from the left shoulder where it is (Rig.Reparent), and eased into its socket; the other arm drops back
    // to its side; its head camera runs round the visor to look down at the new arm while it flexes its elbow and its
    // ear dishes twitch; and at the end the arm stays fitted.
    //
    // It's seen from the free cinematic camera, then through the head camera, then from the drone: CameraTrack.Free,
    // HeadCamera and DroneCamera, the last two the game's to say where they are.
    public static class FittingScene
    {
        public const float Duration = 8f;
        public const string NewArm = "arm-left";
        public const string AttachEvent = "attach arm";
        public const string HeadCamera = "head", DroneCamera = "drone";

        // Clips' names
        public const string Present = "present", Seat = "seat", Flex = "flex", LookAtIt = "look at it";

        // The stick arm lifted out and forward, so the new arm it carries (see Held) swings round in front, its servo
        // brought to the left shoulder (found by trying: within 6 cm of it)
        public static readonly Quaternion Offering =
            Pose.Turn(Vector3.Up, 0.65f) * Pose.Turn(Vector3.UnitZ, -0.8f) * Pose.Turn(Vector3.UnitX, -0.8f);

        // How the new arm is carried: its sporks gripped in the droid's, so it lies back along the stick arm, a little in
        // front of it, its servo up by the shoulders
        public static readonly Pose Held = Pose.At(new Vector3(0f, 0.38f, 0.07f));

        // The new arm, in the droid's hand, ready to start (taken off first if it's been fitted already).
        public static void Hold(Rig rig)
        {
            if (rig.Has(NewArm))
                rig.Detach(NewArm);
            rig.Attach(DroidRig.JointedArm(NewArm, Held), DroidRig.Hand);
        }

        // The scene, for a droid whose rig is `rig` (holding the new arm: see Hold), its clips played by `animator`,
        // standing placed by `placement` (for the free camera's keys, which are round it).
        public static Timeline Build(Rig rig, Animator animator, Matrix placement)
        {
            var scene = new Timeline(Duration);
            scene.Play(0f, animator, PresentClip());
            scene.On(AttachEvent, () =>
            {
                rig.Reparent(NewArm, DroidRig.ShoulderLeft);
                animator.Play(SeatClip(), fadeIn: 1f);   // from wherever it was let go of, into the socket
            });
            scene.Stop(1.9f, animator, Present, fadeOut: 1.2f);   // the stick arm back down to its side
            scene.Play(2.8f, animator, LookClip());
            scene.Play(3.2f, animator, FlexClip());
            scene.At(Duration, () =>
            {
                // Fitted: its socket is where it rests now, so it stays put when the clips go
                rig.SetRest(NewArm, Pose.Identity);
                foreach (var name in new[] { Seat, Flex, LookAtIt })
                    animator.Stop(name);
            });

            Vector3 At(float x, float y, float z) => Vector3.Transform(new Vector3(x, y, z), placement);
            scene.Camera
                .Cut(0f, CameraTrack.Free)
                .Key(0f, At(1.7f, 1.3f, 2.3f), At(0f, 0.9f, 0f))
                .Key(2.8f, At(1.1f, 1.05f, 1.2f), At(0.1f, 0.75f, 0f))
                .Cut(2.8f, HeadCamera)
                .Cut(5.6f, DroneCamera);
            return scene;
        }

        // The stick arm lifting the new one across to the left shoulder, and letting go of it there.
        public static Clip PresentClip()
        {
            var clip = new Clip(Present);
            clip.Track(DroidRig.Arm)
                .Turn(0f, DroidRig.Hanging)
                .Turn(1.4f, Offering)
                .Turn(1.9f, Offering);
            clip.Event(1.5f, AttachEvent);
            return clip;
        }

        // The new arm's servo in its socket, and the arm hanging from it as it should.
        public static Clip SeatClip()
        {
            var clip = new Clip(Seat, duration: 1f);
            clip.Track(NewArm).Move(0f, Vector3.Zero).Turn(0f, Quaternion.Identity);
            return clip;
        }

        // Trying the new elbow: bent up twice, the sporks turned about, and the ear dishes twitching at the noise of it.
        public static Clip FlexClip()
        {
            var clip = new Clip(Flex);
            var bent = Pose.Turn(Vector3.UnitX, -1.7f);
            clip.Track(NewArm + "-forearm")
                .Turn(0f, DroidRig.ElbowRest)
                .Turn(0.6f, bent)
                .Turn(1.1f, DroidRig.ElbowRest)
                .Turn(1.7f, bent)
                .Turn(2.4f, DroidRig.ElbowRest);
            clip.Track(NewArm + "-hand")
                .Turn(0.6f, Quaternion.Identity)
                .Turn(1.0f, Vector3.Up, 1.2f)
                .Turn(1.4f, Vector3.Up, -1.2f)
                .Turn(1.8f, Quaternion.Identity);
            foreach (var (dish, way) in new[] { (DroidRig.DishLeft, 1f), (DroidRig.DishRight, -1f) })
                clip.Track(dish)
                    .Turn(0f, Quaternion.Identity)
                    .Turn(0.15f, Vector3.Up, way * 0.7f, Ease.Out)
                    .Turn(0.5f, Vector3.Up, way * 0.7f)
                    .Turn(0.9f, Quaternion.Identity);
            return clip;
        }

        // The head camera run a little way round the visor to its left, and tipped nearly straight down at the new arm,
        // which hangs almost under it; then back. (The head can't bend that far to look: the camera has to.)
        public static Clip LookClip()
        {
            const float round = 0.6f, down = 1.35f;
            var clip = new Clip(LookAtIt);
            clip.Track(DroidRig.Rail)
                .Turn(0f, Quaternion.Identity)
                .Turn(0.8f, Vector3.Up, round)
                .Turn(2.6f, Vector3.Up, round)
                .Turn(3.4f, Quaternion.Identity);
            clip.Track(DroidRig.Camera)
                .Turn(0f, Quaternion.Identity)
                .Turn(0.8f, Vector3.UnitX, down)   // down (see DroidRig.Look)
                .Turn(2.6f, Vector3.UnitX, down)
                .Turn(3.4f, Quaternion.Identity);
            return clip;
        }
    }
}
