using Microsoft.Xna.Framework;
using System;
using World.Core.Animation;

namespace World.Core.Characters
{
    // What the droid stands on (see Locomotion), as the bottom of its rig: everything up to the axle, the hub the broom
    // stands in, whatever's under it. DroidRig.Build hangs the rest of the droid from the axle, so the broom, the arm
    // and the head are the same on any of them; only how high they stand changes (HubHeight).
    //
    // Segway: the axle on its two wheels (DroidRig's own sizes)
    //
    //   root ─ axle ─┬ wheel-left, wheel-right
    //
    // Tracks: a hull on two tracks, each track a loop of shoes running round a sprocket (behind), an idler (in front)
    // and two road wheels under it; the axle on a turntable on the hull, which turns the whole droid above it
    //
    //   root ─ chassis ─┬ track-left ─┬ track-left-sprocket, track-left-idler, track-left-road-0, -1
    //                   │             └ track-left-shoes                 run round the track with RunTracks
    //                   ├ track-right ...
    //                   └ turret ─ axle                                    turned with Turret
    //
    // TriStar: the axle on a spider each side, three arms each with a wheel on its end, two of them on the ground
    //
    //   root ─ axle ─┬ spider-left ─ spider-left-wheel-0, -1, -2         tipped over with Clusters
    //                └ spider-right ...
    //
    // A track's shoes are one part, not one each: two dozen apiece would be four dozen meshes to draw, and each mesh drawn
    // costs as much again whatever its size. They're evenly spaced, so run on by one shoe's length a track looks just as
    // it did; so its shoes are drawn as one mesh of the whole loop, baked at TrackFrames points through one shoe's length
    // of running, and running it picks which (see TrackPart), as the bird's flapping is baked (see MeshProps' BirdMesh).
    //
    // Churn: no axle, only a milk churn, the broom's foot stood on the bottom inside it and the broom leaning against the
    // rim of its mouth, out over its side (see ChurnLean), the arm swung out to hang clear of it (ChurnArm)
    //
    //   root ─ churn ─ axle                                              the broom's foot, leaning
    //
    // The tracks and spiders are the same either side, so the meshes are too; nothing's mirrored. Facing +Z, its left is +X.
    public static class DroidBases
    {
        public const string Chassis = "chassis", Turret = "turret", TrackLeft = "track-left", TrackRight = "track-right",
            SpiderLeft = "spider-left", SpiderRight = "spider-right";

        public const string HullPart = "tank-hull", TurntablePart = "tank-turntable", HubPart = "droid-hub", ShoePart = "tank-shoe",
            SprocketPart = "tank-sprocket", IdlerPart = "tank-idler", RoadWheelPart = "tank-road-wheel",
            SpiderPart = "tristar-spider", SpiderWheelPart = "tristar-wheel";

        public const string Churn = "churn", ChurnPart = "milk-churn";

        // ---- Tracks

        // The tracks: TrackGauge between their middles, each TrackWidth wide; their end wheels TrackSpan apart, each
        // EndWheelRadius round, and Shoes shoes, each ShoeLength long and ShoeThickness thick, running round them
        public const float TrackGauge = 0.66f, TrackWidth = 0.16f, TrackSpan = 0.62f, EndWheelRadius = 0.12f;
        public const float ShoeThickness = 0.024f, ShoeLength = 0.07f;
        public const int Shoes = 24;
        public const int TrackFrames = 16;   // a track's shoes baked this many times through one shoe's length of running
        public const float RoadWheelRadius = 0.09f, RoadWheelAt = 0.15f;   // two, RoadWheelAt either side of the middle

        // The hull between the tracks, and the turntable on it; the axle (the hub the broom stands in) on that
        public const float HullWidth = 0.48f, HullLength = 0.8f, HullBottom = 0.06f, HullTop = 0.30f;
        public const float TurntableRadius = 0.2f, TurntableHeight = 0.05f;
        public const float HubDepth = 0.035f;   // the hub block's bottom, under the axle (see DroidMesh's axle)

        // The end wheels' middles off the ground, and the path the shoes' middles take round them: two straight runs
        // and two half circles, PathRadius round
        public const float TrackAxleHeight = EndWheelRadius + ShoeThickness;
        public const float PathRadius = EndWheelRadius + ShoeThickness / 2f;
        public static float PathLength => 2f * TrackSpan + MathHelper.TwoPi * PathRadius;

        // How far out from its middle it's kept from walls: it's longer and wider than this, but a circle round it all
        // would keep it out of doorways it fits through
        public const float TankRadius = 0.45f;

        public const float TurretSpeed = 1.5f;   // radians a second, turned flat out

        // ---- Tri-star

        // Each spider's arms SpiderArm from its middle to a wheel's, the wheels SpiderWheelRadius round and SpiderWheelWidth
        // wide (fat, soft tyres, a hand's breadth clear of each other), standing SpiderWheelOut outside it, clear of the
        // spider's arms. The spiders are DroidRig.Track apart, as the Segway's wheels are,
        // on the same axle.
        public const float SpiderArm = 0.13f, SpiderWheelRadius = 0.09f, SpiderWheelWidth = 0.06f, SpiderWheelOut = 0.045f;
        public const float ClusterStep = MathHelper.TwoPi / 3f;   // a spider's turn from one pair of wheels down to the next

        // ---- Churn

        // A milk churn, just over a third as tall as the droid on its Segway wheels (ChurnHeight): a foot ring, a body
        // ChurnRadius round up to ChurnShoulder, its shoulder sloping in to a neck ChurnNeckRadius round from ChurnNeck, and a
        // rolled rim round the mouth (ChurnMouthRadius, open: it has no lid) from ChurnRim to the top
        public const float ChurnHeight = 0.48f, ChurnRadius = 0.17f, ChurnShoulder = 0.27f, ChurnNeck = 0.385f, ChurnNeckRadius = 0.1f;
        public const float ChurnRim = 0.455f, ChurnRimRadius = 0.115f, ChurnMouthRadius = 0.09f;
        public const float ChurnFloor = 0.02f;   // the bottom, inside

        // The broom leans ChurnLean radians from upright, out to the droid's right (its arm's side) and a little forward,
        // against the rim across the mouth from where its foot stands: far enough that its head hangs out past the churn's
        // side, as if the lot might go over
        public const float ChurnLean = 0.28f;
        public static readonly Vector3 ChurnLeanTowards = Vector3.Normalize(new Vector3(-1f, 0f, 0.3f));

        // The arm at rest in the churn. Hanging down the broom as it does on wheels (see DroidRig.Hanging), it would reach
        // nearly to the broom's foot, inside the churn; so it's swung out from the broom, past upright, to hang over the
        // churn's side, ChurnArmOut radians out from straight down and a little forward
        public const float ChurnArmOut = 0.3f;
        public static readonly Quaternion ChurnArm = Pose.Turn(Vector3.UnitZ, -(ChurnLean + ChurnArmOut)) * Pose.Turn(Vector3.UnitX, -0.15f);

        // Where the broom's foot stands on the bottom, so that leaning it touches the rim at the mouth
        public static Vector3 ChurnFoot
        {
            get
            {
                var touches = ChurnMouthRadius - DroidRig.SpineRadius / MathF.Cos(ChurnLean);   // from the middle, at the top
                var below = ChurnHeight - ChurnFloor;
                return new Vector3(0f, ChurnFloor, 0f) + ChurnLeanTowards * (touches - below * MathF.Tan(ChurnLean));
            }
        }

        // The broom's lean in the churn: about the line across the way it leans
        public static Quaternion ChurnTilt => Pose.Turn(Vector3.Cross(Vector3.Up, ChurnLeanTowards), ChurnLean);

        // ---- Any

        // How high the axle is off the ground: the broom's foot, so the whole droid stands this much higher
        public static float HubHeight(Locomotion locomotion) => locomotion switch
        {
            Locomotion.Tracks => HullTop + TurntableHeight + HubDepth,
            Locomotion.TriStar => SpiderWheelRadius + SpiderArm * 0.5f,   // two arms down, 30 degrees below level
            Locomotion.Churn => ChurnFloor,
            _ => DroidRig.WheelRadius,
        };

        // Between the middles of whatever it rolls on either side, which is what turning on the spot runs them round
        public static float Gauge(Locomotion locomotion) => locomotion == Locomotion.Tracks ? TrackGauge : DroidRig.Track;

        // The bottom of the rig, from the root to the axle, for `locomotion`.
        public static void Add(Rig rig, Locomotion locomotion)
        {
            switch (locomotion)
            {
                case Locomotion.Tracks:
                    rig.Add(Chassis, DroidRig.Root, Pose.Identity, HullPart);
                    foreach (var (track, side) in new[] { (TrackLeft, 1f), (TrackRight, -1f) })
                    {
                        rig.Add(track, Chassis, Pose.At(new Vector3(side * TrackGauge / 2f, 0f, 0f)));
                        rig.Add(track + "-sprocket", track, Pose.At(new Vector3(0f, TrackAxleHeight, -TrackSpan / 2f)), SprocketPart);
                        rig.Add(track + "-idler", track, Pose.At(new Vector3(0f, TrackAxleHeight, TrackSpan / 2f)), IdlerPart);
                        for (var k = 0; k < 2; k++)
                            rig.Add($"{track}-road-{k}", track, Pose.At(new Vector3(0f, ShoeThickness + RoadWheelRadius, (k * 2 - 1) * RoadWheelAt)),
                                RoadWheelPart);
                        rig.Add(track + "-shoes", track, Pose.Identity, TrackPart(0));
                    }
                    rig.Add(Turret, Chassis, Pose.At(new Vector3(0f, HullTop, 0f)), TurntablePart);
                    rig.Add(DroidRig.Axle, Turret, Pose.At(new Vector3(0f, TurntableHeight + HubDepth, 0f)), HubPart);
                    break;

                case Locomotion.TriStar:
                    rig.Add(DroidRig.Axle, DroidRig.Root, Pose.At(new Vector3(0f, HubHeight(locomotion), 0f)), DroidRig.AxlePart);
                    foreach (var (spider, side) in new[] { (SpiderLeft, 1f), (SpiderRight, -1f) })
                    {
                        rig.Add(spider, DroidRig.Axle, Pose.At(new Vector3(side * DroidRig.Track / 2f, 0f, 0f)), SpiderPart);
                        for (var k = 0; k < 3; k++)
                        {
                            var arm = SpiderArmAt(k);
                            rig.Add($"{spider}-wheel-{k}", spider, Pose.At(new Vector3(side * SpiderWheelOut, arm.Y, arm.Z)), SpiderWheelPart);
                        }
                    }
                    break;

                case Locomotion.Churn:
                    rig.Add(Churn, DroidRig.Root, Pose.Identity, ChurnPart);
                    rig.Add(DroidRig.Axle, Churn, Pose.At(ChurnFoot, ChurnTilt));
                    break;

                default:
                    rig.Add(DroidRig.Axle, DroidRig.Root, Pose.At(new Vector3(0f, DroidRig.WheelRadius, 0f)), DroidRig.AxlePart);
                    rig.Add(DroidRig.WheelLeft, DroidRig.Axle, Pose.At(new Vector3(DroidRig.Track / 2f, 0f, 0f)), DroidRig.WheelPart);
                    rig.Add(DroidRig.WheelRight, DroidRig.Axle, Pose.At(new Vector3(-DroidRig.Track / 2f, 0f, 0f)), DroidRig.WheelPart);
                    break;
            }
        }

        // Where the end of a spider's arm `k` is, from its middle, the spider unturned: the first straight up, the other
        // two 120 degrees round either way, down in front and down behind.
        public static Vector3 SpiderArmAt(int k)
        {
            var angle = k * ClusterStep;
            return new Vector3(0f, SpiderArm * MathF.Cos(angle), SpiderArm * MathF.Sin(angle));
        }

        // The spacing of the shoes round a track
        public static float ShoePitch => PathLength / Shoes;

        // A track's shoes, run round `frame` TrackFrames-ths of the way from one shoe's place to the next's: a mesh of them all
        public static string TrackPart(int frame) => $"tank-track:{frame}";

        // Which of those a track run on `rolled` metres looks like
        public static int TrackFrame(float rolled)
        {
            var into = rolled / ShoePitch;
            var frame = (int)MathF.Floor((into - MathF.Floor(into)) * TrackFrames);
            return Math.Clamp(frame, 0, TrackFrames - 1);
        }

        // Where shoe `k` is on its track, the track having run `rolled` metres (forward is more): its middle on the path,
        // turned so its grip faces out. The path runs back along the bottom from the front, up round the sprocket,
        // forward along the top and down round the idler; rolling forward moves the shoes along it, so the bottom run
        // stays put on the ground as the droid goes over it.
        public static Pose ShoeAt(int k, float rolled)
        {
            var length = PathLength;
            var s = (k * length / Shoes + rolled) % length;
            if (s < 0f)
                s += length;
            float y, z, turn;   // turn: about X, from the grip facing down
            var arc = MathHelper.Pi * PathRadius;
            if (s < TrackSpan)
                (y, z, turn) = (TrackAxleHeight - PathRadius, TrackSpan / 2f - s, 0f);
            else if (s < TrackSpan + arc)
            {
                var a = (s - TrackSpan) / PathRadius;
                (y, z, turn) = (TrackAxleHeight - PathRadius * MathF.Cos(a), -TrackSpan / 2f - PathRadius * MathF.Sin(a), a);
            }
            else if (s < 2f * TrackSpan + arc)
                (y, z, turn) = (TrackAxleHeight + PathRadius, -TrackSpan / 2f + (s - TrackSpan - arc), MathHelper.Pi);
            else
            {
                var a = (s - 2f * TrackSpan - arc) / PathRadius;
                (y, z, turn) = (TrackAxleHeight + PathRadius * MathF.Cos(a), TrackSpan / 2f + PathRadius * MathF.Sin(a), MathHelper.Pi + a);
            }
            return Pose.At(new Vector3(0f, y, z), Pose.Turn(Vector3.UnitX, turn));
        }

        // The tracks, each run round by how far it has rolled: they differ as it turns, and go opposite ways turning on
        // the spot. The wheels inside turn with them.
        public static void RunTracks(Rig rig, float left, float right)
        {
            foreach (var (track, rolled) in new[] { (TrackLeft, left), (TrackRight, right) })
            {
                rig[track + "-shoes"].Part = TrackPart(TrackFrame(rolled));
                var ends = Pose.Turn(Vector3.UnitX, rolled / EndWheelRadius);
                rig.Change(track + "-sprocket", p => p with { Rotation = ends });
                rig.Change(track + "-idler", p => p with { Rotation = ends });
                var road = Pose.Turn(Vector3.UnitX, rolled / RoadWheelRadius);
                for (var k = 0; k < 2; k++)
                    rig.Change($"{track}-road-{k}", p => p with { Rotation = road });
            }
        }

        // The turntable turned `angle` radians from straight ahead (towards its left is more), and everything on it.
        public static void TurnTurret(Rig rig, float angle) =>
            rig.Change(Turret, p => p with { Rotation = Pose.Turn(Vector3.Up, angle) });

        // The hull tipped nose up by `pitch` radians (down, less than nothing), about the middle of where it stands.
        public static void Pitch(Rig rig, float pitch) =>
            rig.Change(Chassis, p => p with { Rotation = Pose.Turn(Vector3.UnitX, -pitch) });

        // The spiders turned `turn` radians forward (tipping over a step), and each wheel on them by how far its side has
        // rolled, less the spider's turn, so the wheels roll at the speed of the ground whatever the spider's doing.
        public static void Clusters(Rig rig, float left, float right, float turn)
        {
            var spider = Pose.Turn(Vector3.UnitX, turn);
            foreach (var (name, rolled) in new[] { (SpiderLeft, left), (SpiderRight, right) })
            {
                rig.Change(name, p => p with { Rotation = spider });
                var wheel = Pose.Turn(Vector3.UnitX, rolled / SpiderWheelRadius - turn);
                for (var k = 0; k < 3; k++)
                    rig.Change($"{name}-wheel-{k}", p => p with { Rotation = wheel });
            }
        }
    }
}
