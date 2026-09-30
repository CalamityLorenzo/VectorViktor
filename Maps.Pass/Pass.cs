using MeshProps;
using MeshRendering;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using World.Buildings;
using World.Core;
using World.Maps;
using static Maps.Pass.PassTerrain;

namespace Maps.Pass
{
    // The pass's ground (see PassTerrain): the car, parked at the trailhead to drive the road in; places to start along
    // the road's way, in the car; and on foot in the basin by the town's lake. In the basin, just in front of where you
    // start there, giant robots' wreckage: two heads, sitting on their necks and looking your way, and an arm, torn
    // off, lying on the ground to climb on (see RobotHeadMesh, MegatronHeadMesh, RobotArmMesh). The road (see Road) is
    // a district of its own, and the town will be.
    public sealed class Pass : IDistrict
    {
        public static readonly float TrailheadAlong = PassRoute.ControlAlong[PassRoute.Trailhead] + 12f;

        public IEnumerable<ParkedCar> Cars(Terrain terrain)
        {
            yield return new ParkedCar(PassRoute.At(TrailheadAlong).at, PassRoute.YawAt(TrailheadAlong));
        }

        public IReadOnlyDictionary<string, Start> Starts { get; } = new Dictionary<string, Start>
        {
            ["trailhead"] = OnTheRoad(TrailheadAlong),                                   // in the car, at the start of the way
            ["firstpass"] = OnTheRoad(PassRoute.ControlAlong[PassRoute.FirstPass]),
            ["lake"] = OnTheRoad(PassRoute.ControlAlong[PassRoute.LakeShelf]),         // on the shelf above the lake
            ["secondpass"] = OnTheRoad(PassRoute.ControlAlong[PassRoute.SecondPass]),
            // On foot, in the basin, north of the town's lake, looking over it
            ["town"] = new(TownStart, MathHelper.Pi, Above: 2f),
        };

        // The giant robots' wreckage, all to one scale: two heads, an Autobot's and one after Megatron, facing the town
        // start, and an arm, from the torn shoulder out to the east, to climb on
        public const float RobotScale = 3f;
        public const float HeadScale = RobotScale * 1.15f;   // the heads a size up on the arm, to be seen from further off
        private static readonly Vector2 TownStart = TownLakeCentre - new Vector2(0f, TownLakeRadius + TownLakeShore + 20f);
        private static readonly Vector2 HeadAt = new Vector2(285f, 202f), MegatronAt = new Vector2(270f, 200f), ArmAt = new Vector2(311f, 199f);
        private static readonly float HeadYaw = Facing(HeadAt, TownStart), MegatronYaw = Facing(MegatronAt, TownStart);
        private const float ArmYaw = MathHelper.PiOver2;

        // The yaw that turns a piece's face (its +Z) from `at` towards `target`
        private static float Facing(Vector2 at, Vector2 target) => MathF.Atan2(target.X - at.X, target.Y - at.Y);

        public IEnumerable<Fixture> Fixtures(Terrain terrain)
        {
            var arm = RobotArmMesh.Palette(armour: new Color(180, 30, 35), metal: new Color(150, 155, 165), cable: new Color(35, 35, 40));
            yield return new Fixture(RobotHeadMesh.Source(RobotExpression.Stern, AutobotPalette), Place(terrain, HeadAt, HeadYaw, HeadScale));
            yield return new Fixture(MegatronHeadMesh.Source(MegatronPalette), Place(terrain, MegatronAt, MegatronYaw, HeadScale));
            yield return new Fixture(RobotArmMesh.Source(arm), Place(terrain, ArmAt, ArmYaw, RobotScale));
        }

        private static readonly Color[] AutobotPalette = RobotHeadMesh.Palette(helmet: new Color(40, 80, 170), face: new Color(170, 175, 185),
            ear: new Color(60, 170, 175), metal: new Color(90, 95, 105), eye: new Color(120, 220, 255), gem: new Color(200, 30, 40), mouth: new Color(25, 25, 30));
        private static readonly Color[] MegatronPalette = MegatronHeadMesh.Palette(helmet: new Color(150, 150, 158), face: new Color(185, 185, 190),
            ear: new Color(120, 120, 128), metal: new Color(70, 70, 78), eye: new Color(230, 30, 30), mouth: new Color(25, 25, 30));

        // The heads' jaws, talking, taking turns: Megatron, then the Autobot
        public IEnumerable<ScenePart> Moving(Terrain terrain)
        {
            var megatron = Place(terrain, MegatronAt, MegatronYaw, HeadScale);
            var autobot = Place(terrain, HeadAt, HeadYaw, HeadScale);
            yield return new ScenePart(MegatronHeadMesh.JawSource(MegatronPalette), t => MegatronHeadMesh.JawAt(Talking(t, 0)) * megatron);
            yield return new ScenePart(RobotHeadMesh.UpperLipSource(AutobotPalette), t => RobotHeadMesh.MouthAt(Talking(t, 1)).UpperLip * autobot);
            yield return new ScenePart(RobotHeadMesh.LowerLipSource(AutobotPalette), t => RobotHeadMesh.MouthAt(Talking(t, 1)).LowerLip * autobot);
            yield return new ScenePart(RobotHeadMesh.JawSource(AutobotPalette), t => RobotHeadMesh.MouthAt(Talking(t, 1)).Jaw * autobot);
        }

        // How far open a jaw is, `seconds` in: each speaker talks for Turn, then the next, round again
        private const float Turn = 2.6f;
        private const int Speakers = 2;
        private static float Talking(float seconds, int speaker)
        {
            if ((int)(seconds / Turn) % Speakers != speaker)
                return 0f;
            var intoTurn = seconds % Turn;
            var easeIn = MathF.Min(1f, MathF.Min(intoTurn, Turn - intoTurn) / 0.2f);   // no snapping shut at the turn's ends
            return easeIn * MathF.Abs(MathF.Sin(seconds * 7f)) * (0.55f + 0.45f * MathF.Sin(seconds * 2.3f));
        }

        // Round each head, its four sides, to walk into
        public IEnumerable<WallSegment> Walls(Terrain terrain)
        {
            foreach (var (at, yaw, half, height) in new[] { (HeadAt, HeadYaw, RobotHeadMesh.HalfFootprint, RobotHeadMesh.Height),
                                                             (MegatronAt, MegatronYaw, MegatronHeadMesh.HalfFootprint, MegatronHeadMesh.Height) })
            {
                var corners = new[] { World(at, yaw, HeadScale, -half.X, -half.Y), World(at, yaw, HeadScale, half.X, -half.Y),
                                      World(at, yaw, HeadScale, half.X, half.Y), World(at, yaw, HeadScale, -half.X, half.Y) };
                var foot = terrain.HeightAt(at.X, at.Y);
                for (var k = 0; k < 4; k++)
                    yield return new WallSegment(corners[k], corners[(k + 1) % 4], foot - 1f, foot + height * HeadScale);
            }
        }

        // The arm's parts' tops, to jump up onto and stand on, their sides to walk into. A ledge is round at its ends,
        // so each top's a row of narrow ones side by side along its length, leaving little of its corners uncovered.
        private const float LedgeHalfWidth = 0.05f;   // in the arm's own units
        public IEnumerable<Ledge> Ledges(Terrain terrain)
        {
            var foot = terrain.HeightAt(ArmAt.X, ArmAt.Y);
            foreach (var (min, max, top) in RobotArmMesh.Tops)
            {
                var size = max - min;
                var alongX = size.X >= size.Y;
                var (length, across) = alongX ? (size.X, size.Y) : (size.Y, size.X);
                var strips = (int)MathF.Ceiling(across / (2f * LedgeHalfWidth));
                var half = across / (2f * strips);
                for (var k = 0; k < strips; k++)
                {
                    var c = half * (2 * k + 1);   // the strip's middle, across
                    var (a, b) = alongX
                        ? (new Vector2(min.X + half, min.Y + c), new Vector2(max.X - half, min.Y + c))
                        : (new Vector2(min.X + c, min.Y + half), new Vector2(min.X + c, max.Y - half));
                    yield return new Ledge(World(ArmAt, ArmYaw, RobotScale, a.X, a.Y), World(ArmAt, ArmYaw, RobotScale, b.X, b.Y), half * RobotScale, foot - 1f, foot + top * RobotScale);
                }
            }
        }

        private static Matrix Place(Terrain terrain, Vector2 at, float yaw, float scale) =>
            Matrix.CreateScale(scale) * Matrix.CreateRotationY(yaw) * Matrix.CreateTranslation(at.X, terrain.HeightAt(at.X, at.Y), at.Y);

        // A point in a piece's own (x, z), in the world, the piece at `at` turned by `yaw`, at `scale`
        private static Vector2 World(Vector2 at, float yaw, float scale, float x, float z)
        {
            var p = Vector3.Transform(new Vector3(x, 0f, z) * scale, Matrix.CreateRotationY(yaw));
            return at + new Vector2(p.X, p.Z);
        }

        // In a car on the road's middle line `s` along it, facing the way to the town
        private static Start OnTheRoad(float s) => new(PassRoute.At(s).at, PassRoute.YawAt(s), Above: 2f, InCar: true);
    }
}
