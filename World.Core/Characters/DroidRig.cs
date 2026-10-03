using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using World.Core.Animation;

namespace World.Core.Characters
{
    // The starting droid (see GameDesign.md 3.1) as a rig: an urn-shaped head on a broom handle, balanced on a pair
    // of hoverboard wheels, with one stick of an arm, worked by a servo clamped to the broom and ending in two sporks
    // crudely bolted together. It faces +Z, as the meshes do, standing on y = 0 midway between its wheels. About
    // 1.4 m to the top of its head.
    //
    // The wheels are one way of getting about (see Locomotion): Build can stand it on tank tracks or tri-star wheels
    // instead, or in a milk churn (see DroidBases), whatever's below the axle changing, and the rest standing higher or
    // lower on it (in the churn, leaning).
    //
    //   root ─ axle ─┬ wheel-left, wheel-right         turn with Roll (on the others, see DroidBases)
    //                └ lean ─ spine ─┬ shoulder ─ arm ─ hand
    //                                ├ shoulder-left                     a socket: a limb can be fitted here (see JointedArm)
    //                                └ head ─┬ ear-left ─ dish-left      dishes wander with Listen
    //                                        ├ ear-right ─ dish-right
    //                                        └ rail ─ camera             round the visor with Look
    //
    // The head, from the front (Paul's sketch): about as tall as it is wide, its corners rounded, on a short
    // narrower neck; the ruby visor a band round its upper part, standing a little proud of it; the ears hoops on
    // its sides, seen face on from the front, level with its lower middle. The visor goes all the way round, and
    // the head camera runs round inside it like a rail, so it can look any way at all without the head turning.
    //
    // The head works everything, so it's wired to it (see Wiring): two cords run down the back of the broom from
    // the neck, one to the arm's servo, and on from there down the arm to the sporks, the other to the axle's hub
    // and out along the axle to the motor inside each wheel. They're thin; they show close to.
    //
    // Facing +Z with y up, its left is +X.
    public static class DroidRig
    {
        // Its parts' names, for clips and the meshes (see World.Rendering's DroidMesh)
        public const string Root = "root", Axle = "axle", WheelLeft = "wheel-left", WheelRight = "wheel-right",
            Lean = "lean", Spine = "spine", Shoulder = "shoulder", Arm = "arm", Hand = "hand", ShoulderLeft = "shoulder-left",
            Head = "head", EarLeft = "ear-left", EarRight = "ear-right", DishLeft = "dish-left", DishRight = "dish-right",
            Rail = "rail", Camera = "camera";

        // What's drawn at them: the part names a mesh is found by. Both wheels are the same wheel, both ears the same ear.
        public const string AxlePart = "droid-axle", WheelPart = "droid-wheel", SpinePart = "droid-broom", ArmPart = "droid-stick-arm",
            HandPart = "droid-sporks", HeadPart = "droid-urn-head", EarPart = "droid-ear", DishPart = "droid-dish", CameraPart = "droid-camera",
            ServoMountPart = "droid-servo-mount", LimbPart = "droid-limb";

        // Hoverboard wheels, either side of the axle
        public const float WheelRadius = 0.14f, WheelWidth = 0.06f, Track = 0.40f;   // Track: between the wheels' middles

        // The broom handle, from the axle to the bottom of the neck
        public const float SpineLength = 0.86f, SpineRadius = 0.035f;   // thicker than a real broom's, so its colour shows at low resolution

        // The head, measured off the sketch: its body HeadHeight tall and HeadRadius round, rounded top and bottom
        // by Bevel, on a neck NeckHeight tall; the visor from VisorBottom to VisorTop up the head's body, standing
        // VisorProud out from it; the ears' middles EarHeight up it, each hoop EarRadius round
        public const float NeckRadius = 0.10f, NeckHeight = 0.05f;
        public const float HeadRadius = 0.17f, HeadHeight = 0.34f, Bevel = 0.04f;
        public const float VisorBottom = 0.19f, VisorTop = 0.30f, VisorProud = 0.012f;
        // How far the head camera tilts from level: sometimes the head can't move, so the camera has to do the looking
        public static readonly float MaxLookUp = MathHelper.ToRadians(60f), MaxLookDown = MathHelper.ToRadians(90f);
        public const float EarRadius = 0.045f, EarHeight = 0.14f;

        // The arm: a stick hanging from the servo's shaft, the servo clamped to the broom on its right, ShoulderHeight up it
        public const float ShoulderHeight = 0.62f, ArmLength = 0.45f, ArmRadius = 0.025f;
        public const float ServoWidth = 0.035f, ServoHeight = 0.045f, ServoDepth = 0.05f;   // across, up, front to back

        // The hand: two sporks, bowls facing each other like a pincer, their handles bolted and lashed together
        // SporkTop below the end of the arm, each SporkApart out from the middle, splayed SporkSplay radians
        public const float SporkTop = 0.012f, SporkApart = 0.012f, SporkSplay = 0.1f;
        public const float SporkHandle = 0.065f, SporkBowl = 0.05f, SporkBowlWidth = 0.034f, SporkTines = 0.01f;
        public const float HandLength = SporkTop + SporkHandle + SporkBowl + SporkTines;

        // The motors inside the wheels, on the axle (so they don't turn with the wheel): drums MotorLength long, just
        // inside each wheel
        public const float MotorRadius = 0.05f, MotorLength = 0.035f;

        // The cables' thicknesses: the cords down the broom, and the finer filament to the motors and the sporks
        public const float CordRadius = 0.004f, FilamentRadius = 0.0025f;

        // The arm at rest: hanging from the shoulder, a little forward and a little out from the broom, so the two
        // don't run together at low resolution
        public static readonly Quaternion Hanging = Pose.Turn(Vector3.UnitZ, -0.12f) * Pose.Turn(Vector3.UnitX, -0.25f);

        // A jointed arm (see JointedArm): each of its two sticks LimbLength long, hanging as the stick arm does but on the
        // left, bent a little at the elbow
        public const float LimbLength = 0.22f;
        public static readonly Quaternion HangingLeft = Pose.Turn(Vector3.UnitZ, 0.12f) * Pose.Turn(Vector3.UnitX, -0.25f);
        public static readonly Quaternion ElbowRest = Pose.Turn(Vector3.UnitX, -0.3f);

        public static float VisorRadius => HeadRadius + VisorProud;
        public static float VisorMiddle => NeckHeight + (VisorBottom + VisorTop) / 2f;   // up from the head's joint
        public static float EarOut => HeadRadius + EarRadius * 0.8f;                     // the hoop sits against the head's side
        public static float Height => HeightOn(Locomotion.Segway);

        // To the top of its head, standing on `locomotion`: in the churn, leaning, a little less
        public static float HeightOn(Locomotion locomotion)
        {
            var above = SpineLength + NeckHeight + HeadHeight;
            if (locomotion == Locomotion.Churn)
                return DroidBases.HubHeight(locomotion) + above * MathF.Cos(DroidBases.ChurnLean) + HeadRadius * MathF.Sin(DroidBases.ChurnLean);
            return DroidBases.HubHeight(locomotion) + above;
        }

        public static Rig Build(Locomotion locomotion = Locomotion.Segway)
        {
            var rig = new Rig();
            rig.Add(Root, null, Pose.Identity);
            DroidBases.Add(rig, locomotion);

            // Everything above the wheels leans about the axle, as a Segway rider does
            rig.Add(Lean, Axle, Pose.Identity);
            rig.Add(Spine, Lean, Pose.Identity, SpinePart);
            rig.Add(Shoulder, Spine, Pose.At(new Vector3(-(SpineRadius + ServoWidth + ArmRadius), ShoulderHeight, 0f)));   // the servo's shaft, on its right
            rig.Add(Arm, Shoulder, Pose.At(Vector3.Zero, locomotion == Locomotion.Churn ? DroidBases.ChurnArm : Hanging), ArmPart);
            rig.Add(Hand, Arm, Pose.At(new Vector3(0f, -ArmLength, 0f)), HandPart);
            rig.Add(ShoulderLeft, Spine, Pose.At(new Vector3(0f, ShoulderHeight, 0f)));   // the broom's middle: a limb brings its own servo

            rig.Add(Head, Spine, Pose.At(new Vector3(0f, SpineLength, 0f)), HeadPart);
            var ears = NeckHeight + EarHeight;
            rig.Add(EarLeft, Head, Pose.At(new Vector3(EarOut, ears, 0f)), EarPart);
            rig.Add(DishLeft, EarLeft, Pose.Identity, DishPart);
            rig.Add(EarRight, Head, Pose.At(new Vector3(-EarOut, ears, 0f), Pose.Turn(Vector3.Up, MathHelper.Pi)), EarPart);   // turned so its dish faces out
            rig.Add(DishRight, EarRight, Pose.Identity, DishPart);

            // The camera's rail is the visor: the rail turns about the head's axis, carrying the camera round
            rig.Add(Rail, Head, Pose.At(new Vector3(0f, VisorMiddle, 0f)));
            rig.Add(Camera, Rail, Pose.At(new Vector3(0f, 0f, VisorRadius)), CameraPart);

            Wiring(rig, locomotion);
            return rig;
        }

        // A second arm, with an elbow, to fit at ShoulderLeft (see Rig.Attach): a servo of its own clamped to the broom's left
        // (the limb's root, `prefix`), an upper arm hanging from its shaft (`prefix`-upper), a forearm from the elbow
        // (`prefix`-forearm) and a pair of sporks for a hand (`prefix`-hand). `fitting` is where its root rests relative to
        // what it's fitted to: at a socket, nothing; carried in a hand, wherever it's held.
        public static Rig JointedArm(string prefix, Pose? fitting = null)
        {
            var arm = new Rig();
            arm.Add(prefix, null, fitting ?? Pose.Identity, ServoMountPart);
            arm.Add(prefix + "-upper", prefix, Pose.At(new Vector3(SpineRadius + ServoWidth + ArmRadius, 0f, 0f), HangingLeft), LimbPart);
            arm.Add(prefix + "-forearm", prefix + "-upper", Pose.At(new Vector3(0f, -LimbLength, 0f), ElbowRest), LimbPart);
            arm.Add(prefix + "-hand", prefix + "-forearm", Pose.At(new Vector3(0f, -LimbLength, 0f)), HandPart);
            return arm;
        }

        // The sporks close front to back, so from in front the front one's bowl is seen, not their edges.
        public static readonly Matrix SporkFacing = Matrix.CreateRotationY(-MathHelper.PiOver2);

        // Where a spork is on the hand: `side` 1 for the one in front, -1 behind. Its handle runs down its -Y from its
        // origin, its bowl's hollow facing in (towards the other).
        public static Matrix SporkPlace(float side) =>
            Matrix.CreateRotationZ(side * SporkSplay) * Matrix.CreateTranslation(side * SporkApart, -SporkTop, 0f) * SporkFacing;

        // The cords and filament from the head to what it works (see the top).
        private static void Wiring(Rig rig, Locomotion locomotion)
        {
            var back = -(SpineRadius + CordRadius);                         // down the back of the broom
            var collar = SpineLength - 0.05f;                               // just under the collar the neck sits in
            CablePoint Neck(float x) => new CablePoint(Head, new Vector3(x, 0.02f, -(NeckRadius + CordRadius)));

            // To the arm: down the right of the broom's back, into the top of the servo; then out of its bottom, down
            // the back of the arm, to where the sporks are lashed, and a strand to each
            var servoX = -(SpineRadius + ServoWidth / 2f);
            var servoBack = -(ServoDepth / 2f + CordRadius);
            var toServo = new List<CablePoint> { Neck(-0.012f) };
            toServo.AddRange(Run(Spine, new Vector3(-0.012f, collar, back), new Vector3(-0.012f, ShoulderHeight + 0.06f, back), 2, new Vector3(0f, 0f, -0.008f)));
            toServo.Add(new CablePoint(Spine, new Vector3(servoX, ShoulderHeight + ServoHeight / 2f - 0.005f, servoBack)));
            rig.AddCable(new Cable("to the servo", CordRadius, toServo));

            var armBack = -(ArmRadius + FilamentRadius);
            var toHand = new List<CablePoint> { new CablePoint(Spine, new Vector3(servoX, ShoulderHeight - ServoHeight / 2f + 0.005f, servoBack)) };
            toHand.AddRange(Run(Arm, new Vector3(0f, -0.04f, armBack), new Vector3(0f, -ArmLength + 0.02f, armBack * 0.87f), 2, new Vector3(0f, 0f, -0.006f)));
            var lashing = new Vector3(0f, -SporkTop - 0.028f, -0.025f);   // just behind the lashing
            toHand.Add(new CablePoint(Hand, lashing));
            rig.AddCable(new Cable("to the hand", FilamentRadius, toHand));
            foreach (var side in new[] { -1f, 1f })
            {
                var handle = Vector3.Transform(new Vector3(0f, -SporkHandle * 0.7f, -0.0065f), SporkPlace(side));
                rig.AddCable(new Cable(side > 0 ? "to the front spork" : "to the back spork", FilamentRadius, new[]
                {
                    new CablePoint(Hand, lashing),
                    new CablePoint(Hand, (lashing + handle) / 2f + new Vector3(0f, -0.004f, -0.003f)),
                    new CablePoint(Hand, handle),
                }));
            }

            // To the wheels: down the left of the broom's back to the hub, then along the back of the axle to each motor
            var toHub = new List<CablePoint> { Neck(0.012f) };
            toHub.AddRange(Run(Spine, new Vector3(0.012f, collar, back), new Vector3(0.012f, 0.04f, back), 5, new Vector3(0f, 0f, -0.008f)));
            toHub.Add(new CablePoint(Axle, new Vector3(0.01f, 0.01f, -0.038f)));
            rig.AddCable(new Cable("to the hub", CordRadius, toHub));
            if (locomotion is Locomotion.Tracks or Locomotion.Churn)
                return;   // its motors are down in the hull, out of sight; in a churn, it has none
            var motorInside = Track / 2f - WheelWidth / 2f - MotorLength;
            foreach (var side in new[] { -1f, 1f })
                rig.AddCable(new Cable(side > 0 ? "to the left motor" : "to the right motor", FilamentRadius, new[]
                {
                    new CablePoint(Axle, new Vector3(side * 0.03f, -0.015f, -0.038f)),
                    new CablePoint(Axle, new Vector3(side * (motorInside * 0.55f), -0.03f, -0.024f)),   // sagging
                    new CablePoint(Axle, new Vector3(side * (motorInside - 0.002f), -0.01f, -0.03f)),
                }));
        }

        // Points along a straight run on one part, from `from` to `to`: clipped to it at each end and `clips` - 1 times
        // between, and bowed out by `bow` midway between each clip and the next.
        private static IEnumerable<CablePoint> Run(string node, Vector3 from, Vector3 to, int clips, Vector3 bow)
        {
            for (var k = 0; k <= clips * 2; k++)
                yield return new CablePoint(node, Vector3.Lerp(from, to, k / (clips * 2f)) + (k % 2 == 1 ? bow : Vector3.Zero));
        }

        // The wheels, turned by how far the droid has rolled forward (backward, less than nothing): as far round their
        // rims as it's gone, so they don't slip.
        public static void Roll(Rig rig, float distance) => Roll(rig, distance, distance);

        // Each wheel by how far it has rolled on its own: they differ when it turns, and go opposite ways when it
        // turns on the spot (see DroidMotion).
        public static void Roll(Rig rig, float left, float right)
        {
            rig.Change(WheelLeft, p => p with { Rotation = Pose.Turn(Vector3.UnitX, left / WheelRadius) });
            rig.Change(WheelRight, p => p with { Rotation = Pose.Turn(Vector3.UnitX, right / WheelRadius) });
        }

        // Tilts everything above the wheels forward (backward, less than nothing) about the axle, by `angle` radians.
        public static void Tilt(Rig rig, float angle) =>
            rig.Change(Lean, p => p with { Rotation = Pose.Turn(Vector3.UnitX, angle) });

        // The head camera `around` radians round the visor from straight ahead (towards the droid's left is more), looking
        // `up` radians above level (held to MaxLookUp, and MaxLookDown below) and `aside` radians along the rail's
        // tangent from straight out.
        public static void Look(Rig rig, float around, float up = 0f, float aside = 0f)
        {
            up = Math.Clamp(up, -MaxLookDown, MaxLookUp);
            rig.Change(Rail, p => p with { Rotation = Pose.Turn(Vector3.Up, around) });
            rig.Change(Camera, p => p with { Rotation = Pose.Turn(Vector3.Up, aside) * Pose.Turn(Vector3.UnitX, -up) });
        }

        // Where the head camera is and which way it looks, as of the rig's last Solve: the eye to see the world from.
        public static (Vector3 eye, Vector3 forward, Vector3 up) CameraView(Rig rig)
        {
            var camera = rig.World(Camera);
            return (camera.Translation, Vector3.Normalize(Vector3.TransformNormal(Vector3.UnitZ, camera)),
                    Vector3.Normalize(Vector3.TransformNormal(Vector3.UnitY, camera)));
        }

        // The ear dishes sampling the air, `seconds` in: each wanders about on its own, a smooth tangle of slow waves
        // (never the same twice over the minutes it'd be watched), turning up to about half a turn either way and
        // tipping up and down less. `seed` keeps two droids out of step.
        public static void Listen(Rig rig, float seconds, int seed = 0)
        {
            Wander(rig, DishLeft, seconds, seed * 7.3f + 1.1f);
            Wander(rig, DishRight, seconds, seed * 7.3f + 4.7f);
        }

        private static void Wander(Rig rig, string dish, float t, float phase)
        {
            if (!rig.Has(dish))
                return;
            var turn = 0.9f * MathF.Sin(0.61f * t + phase) + 0.5f * MathF.Sin(1.37f * t + 2.1f * phase);
            var tip = 0.35f * MathF.Sin(0.83f * t + 1.7f * phase) + 0.2f * MathF.Sin(2.03f * t + phase);
            var twitch = MathF.Pow(MathF.Max(0f, MathF.Sin(0.29f * t + 3f * phase)), 12f) * 0.6f;   // now and then, a quick look round
            rig.Change(dish, p => p with { Rotation = Pose.Turn(Vector3.Up, turn + twitch) * Pose.Turn(Vector3.UnitZ, tip) });
        }

        // A keyframed greeting, looped: it lifts its arm and waves the fork, tilting its head, then rests. Four seconds of
        // waving in every seven. Only the arm, hand and head are keyed, so it plays over rolling and looking round.
        public static Clip Wave()
        {
            var clip = new Clip("wave", loops: true, duration: 7f);
            var raised = Pose.Turn(Vector3.UnitZ, -2.3f) * Pose.Turn(Vector3.UnitX, -0.3f);   // up and out to its right, a little forward
            clip.Track(Arm)
                .Turn(0f, Hanging)
                .Turn(0.8f, raised)
                .Turn(3.6f, raised)
                .Turn(4.6f, Hanging);
            var hand = clip.Track(Hand).Turn(0f, Quaternion.Identity).Turn(0.8f, Quaternion.Identity);
            for (var k = 0; k < 6; k++)
                hand.Turn(1.2f + k * 0.4f, Vector3.UnitZ, k % 2 == 0 ? 0.5f : -0.5f);
            hand.Turn(3.6f, Quaternion.Identity);
            clip.Track(Head)
                .Turn(0.6f, Quaternion.Identity)
                .Turn(1.2f, Vector3.UnitZ, -0.18f)
                .Turn(3.4f, Vector3.UnitZ, -0.18f)
                .Turn(4.2f, Quaternion.Identity);
            return clip;
        }
    }
}
