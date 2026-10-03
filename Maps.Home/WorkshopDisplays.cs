using MeshRendering;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using World.Core.Animation;
using World.Core.Characters;
using World.Maps;
using World.Rendering;

namespace Maps.Home
{
    // What's on show in the workshop yard (see Workshop): a droid on each way of getting about, going through its paces
    // over and over, and the spare parts laid out on the bench. Positions are across the yard from its middle (x east,
    // z south), on its ground; `yard` places them in the world.
    //
    // Each droid's moves are a function of time worked out here, as DroidDisplay's are, not the game's own (see
    // DroidMotion), which follows a droid being driven: these follow a script.
    public static class WorkshopDisplays
    {
        // Where each stands: in a row across the south of the yard, facing south, towards the 'workshop' start
        public static readonly Vector2 ChurnAt = new Vector2(-7f, 3f), SegwayAt = new Vector2(-4f, 3f), TankAt = new Vector2(0f, 3f),
            TriStarAt = new Vector2(5f, 3.5f);

        // The tri-star's stair, north of it: ShowSteps steps up to a landing, each ShowRise high and ShowRun deep, the
        // first starting ShowStairFrom north of where it stands
        public const float ShowRise = 0.16f, ShowRun = Workshop.StairRun, ShowStairFrom = 1.3f, ShowLanding = 1f, ShowWidth = 1f;
        public const int ShowSteps = 2;
        private const float ShowClimb = ShowStairFrom + ShowSteps * ShowRun + ShowLanding / 2f;   // how far north it goes, to the landing's middle

        // The stair's steps, landing last: each one's middle, half its size each way, and its top
        public static IEnumerable<(Vector2 at, Vector2 half, float top)> ShowStair()
        {
            for (var k = 0; k < ShowSteps; k++)
                yield return (TriStarAt - new Vector2(0f, ShowStairFrom + (k + 0.5f) * ShowRun), new Vector2(ShowWidth / 2f, ShowRun / 2f), (k + 1) * ShowRise);
            var landing = ShowStairFrom + ShowSteps * ShowRun;
            yield return (TriStarAt - new Vector2(0f, landing + ShowLanding / 2f), new Vector2(ShowWidth / 2f, ShowLanding / 2f), (ShowSteps + 1) * ShowRise);
        }

        public static IEnumerable<ScenePart> Parts(Matrix yard)
        {
            var palette = DroidMesh.StartingPalette();
            var meshes = DroidMesh.Sources(palette);
            var cable = DroidMesh.CableSource(palette);
            IEnumerable<ScenePart> Show(Locomotion locomotion, Vector2 at, Action<Rig, float> pose) =>
                RigScene.Parts(DroidRig.Build(locomotion), meshes, pose, Matrix.CreateTranslation(at.X, 0f, at.Y) * yard, cable, DroidMesh.Variants);
            return Show(Locomotion.Churn, ChurnAt, Teetering)
                .Concat(Show(Locomotion.Segway, SegwayAt, Rocking))
                .Concat(Show(Locomotion.Tracks, TankAt, Turning))
                .Concat(Show(Locomotion.TriStar, TriStarAt, Climbing));
        }

        // In the churn: going nowhere, the broom swaying a little where it leans on the rim, and now and then wobbling, as
        // if this time it'll go over, then settling back
        private static void Teetering(Rig rig, float t)
        {
            var sway = 0.01f * MathF.Sin(1.1f * t) + 0.006f * MathF.Sin(2.3f * t + 1f);
            var wobble = 0.03f * MathF.Pow(MathF.Max(0f, MathF.Sin(0.25f * t)), 12f) * MathF.Sin(7f * t);
            var across = Vector3.Cross(Vector3.Up, DroidBases.ChurnLeanTowards);
            rig.Change(DroidRig.Lean, p => p with { Rotation = Pose.Turn(across, sway + wobble) });
            Look(rig, t, 0);
        }

        // On Segway wheels: rocking back and forth, leaning into each start and stop, as DroidDisplay does
        private static void Rocking(Rig rig, float t)
        {
            const float travel = 0.35f, rate = 0.9f, leanPerAcceleration = 0.5f;
            var along = travel * MathF.Sin(rate * t);
            rig.Change(DroidRig.Root, p => p with { Translation = new Vector3(0f, 0f, along) });
            DroidRig.Roll(rig, along);
            DroidRig.Tilt(rig, leanPerAcceleration * -travel * rate * rate * MathF.Sin(rate * t));
            Look(rig, t, 1);
        }

        // On tracks: turning on the spot, right round, the tracks running opposite ways, while the turntable turns the
        // other way just as fast, so the droid above it keeps facing you
        private static void Turning(Rig rig, float t)
        {
            const float rate = 0.5f;   // radians a second
            var turned = rate * t;     // to the right
            rig.Change(DroidRig.Root, p => p with { Rotation = Pose.Turn(Vector3.Up, -turned) });
            var around = turned * DroidBases.TrackGauge / 2f;
            DroidBases.RunTracks(rig, around, -around);
            DroidBases.TurnTurret(rig, turned);
            Look(rig, t, 2);
        }

        // On tri-star wheels: backing up the stair behind it to the landing (as you'd pull a sack truck up stairs),
        // waiting, and coming down it forwards; its spiders tipping over at each step
        private static void Climbing(Rig rig, float t)
        {
            const float period = 14f;
            var s = t % period;
            var north = s < 1.5f ? 0f : s < 6.5f ? Ease((s - 1.5f) / 5f) : s < 8f ? 1f : s < 13f ? 1f - Ease((s - 8f) / 5f) : 0f;
            var a = north * ShowClimb;   // how far north of where it stands: backwards, since it faces south

            // Each step's edge, and the stretch either side of it over which it's climbed: from its back wheels meeting the
            // riser to its middle being over the step
            var (height, tipped) = (0f, 0f);
            for (var k = 0; k <= ShowSteps; k++)
            {
                var edge = ShowStairFrom + k * ShowRun;
                var up = Ease(Math.Clamp((a - edge + 0.12f) / 0.2f, 0f, 1f));
                height += ShowRise * up;
                tipped += up;
            }
            rig.Change(DroidRig.Root, p => p with { Translation = new Vector3(0f, height, -a) });
            DroidBases.Clusters(rig, -a, -a, -tipped * DroidBases.ClusterStep);
            Look(rig, t, 3);
        }

        // Its head camera wandering round the visor and its ears listening, each droid out of step with the others
        private static void Look(Rig rig, float t, int seed)
        {
            DroidRig.Look(rig, 0.6f * MathF.Sin(0.3f * t + seed), up: 0.15f * MathF.Sin(0.5f * t + seed));
            DroidRig.Listen(rig, t, seed);
        }

        private static float Ease(float x) => x * x * (3f - 2f * x);

        // The spare parts on the bench, the middle of whose top is `bench` across the yard and `top` up: a wheel, a
        // tri-star spider with its wheels, a strip of track shoes, the stick arm with its sporks, and a jointed arm in
        // pieces. Each is one of the droid's own parts (see DroidMesh), laid down.
        public static IEnumerable<Fixture> SpareParts(Vector2 bench, float top)
        {
            var meshes = DroidMesh.Sources(DroidMesh.StartingPalette());
            Fixture Part(string part, Matrix lay, float x, float y, float z) =>
                new Fixture(meshes[part], lay * Matrix.CreateTranslation(bench.X + x, top + y, bench.Y + z));
            var faceOn = Matrix.CreateRotationY(MathHelper.PiOver2);   // turning about Z: its face to the south
            var lying = Matrix.CreateRotationZ(MathHelper.PiOver2);    // hanging down -Y, now lying along +X

            yield return Part(DroidRig.WheelPart, faceOn, -1.3f, DroidRig.WheelRadius, 0f);

            var spider = DroidBases.SpiderWheelRadius + DroidBases.SpiderArm / 2f;   // two wheels down
            yield return Part(DroidBases.SpiderPart, faceOn, -0.8f, spider, 0f);
            for (var k = 0; k < 3; k++)
            {
                var arm = DroidBases.SpiderArmAt(k);
                yield return Part(DroidBases.SpiderWheelPart, faceOn, -0.8f + arm.Z, spider + arm.Y, DroidBases.SpiderWheelOut);
            }

            for (var k = 0; k < 5; k++)
                yield return Part(DroidBases.ShoePart, faceOn, -0.45f + k * (DroidBases.ShoeLength + 0.006f), DroidBases.ShoeThickness / 2f, 0.1f);

            const float r = DroidRig.ArmRadius;
            yield return Part(DroidRig.ArmPart, lying, 0f, r * 1.1f, 0.22f);
            yield return Part(DroidRig.HandPart, Matrix.CreateRotationY(MathHelper.PiOver2) * lying, DroidRig.ArmLength, r * 1.1f, 0.22f);

            yield return Part(DroidRig.ServoMountPart, Matrix.Identity, 0.2f, DroidRig.ServoHeight + 0.004f, -0.2f);
            yield return Part(DroidRig.LimbPart, lying, 0.45f, r * 1.1f, -0.2f);
            yield return Part(DroidRig.LimbPart, lying, 0.45f + DroidRig.LimbLength + 0.03f, r * 1.1f, -0.2f);
            yield return Part(DroidRig.HandPart, Matrix.CreateRotationY(MathHelper.PiOver2) * lying, 0.45f + 2f * DroidRig.LimbLength + 0.06f, r * 1.1f, -0.2f);
        }
    }
}
