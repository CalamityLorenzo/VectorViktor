using MeshCore.Library;
using MeshRendering;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using World.Buildings;
using World.Core;
using World.Core.Movement;
using World.Maps;

namespace Maps.Pass
{
    // Where the road can be driven but not walked, so there's no getting out of the car to go on (see PassMap):
    //
    //  - the ford, on the shelf above the cliff lake: the road dips into a river running fast across it, from the
    //    mountainside on its left (going towards the town) and over the shelf's edge on its right, into the lake. The
    //    car drives through it, slowed by the water; a walker's carried off by it, faster than anyone can wade, over the
    //    edge and down into the lake - from where a stair climbs the cliff back to the road, on this side of the ford
    //    (see Stair)
    //  - the jump, on the long straight climb to the first pass: the road rises up a ramp to a lip, and beyond it falls
    //    away into a pit, its floor sloping down to the foot of a wall the road goes on from the top of. Fast enough off
    //    the lip, a car flies over it; a walker can't jump a quarter of the way. Whoever drops into the pit can climb
    //    back out up its floor, and try again
    //
    // Both are the road's own shape (see Offset): the terrain's cut to it (see PassTerrain) and the road's laid along
    // it (see Road), but not over the pit, where there's no road.
    public sealed class Crossings : IDistrict
    {
        // The ford: its bed level BedHalf either side of FordAt along the road, Dip below the road there, and the road
        // easing down to it over Bank either side; the water Depth over the bed, flowing at FlowSpeed
        public const float FordAt = 812f;
        public const float BedHalf = 9f, Bank = 10f, Dip = 1f, Depth = 0.55f;
        public const float FlowSpeed = 6f;   // metres per second: more than anyone can wade, even running

        // The jump: the ramp RampLength long, rising RampRise to its lip at JumpAt; beyond it the pit, GapLength to
        // the wall, its floor sloping down from the lip at PitGrade below the road's own
        public const float JumpAt = 450f;
        public const float RampLength = 14f, RampRise = 2f, GapLength = 12f, PitGrade = 0.38f;   // the wall 2.6 m: out of a jump's reach
        public const float JumpFar = JumpAt + GapLength;   // the top of the wall, where the road goes on

        public static readonly float BedLevel = PassRoute.At(FordAt).height - Dip;
        public static readonly float FordLevel = BedLevel + Depth;

        // The road's surface `s` along, where PassRoute has it `road` high: down into the ford, up the ramp, down into
        // the pit, and elsewhere just that
        public static float Surface(float s, float road)
        {
            var fromFord = MathF.Abs(s - FordAt);
            if (fromFord < BedHalf + Bank)
            {
                var into = fromFord <= BedHalf ? 1f : SmoothStep(1f - (fromFord - BedHalf) / Bank);
                return road + (BedLevel - road) * into;
            }
            if (s > JumpAt - RampLength && s <= JumpAt)
            {
                var up = (s - (JumpAt - RampLength)) / RampLength;
                return road + RampRise * up * up;   // steepest at the lip
            }
            if (s > JumpAt && s < JumpFar)
                return road + RampRise - PitGrade * (s - JumpAt);
            return road;
        }

        public static float Surface(float s) => Surface(s, PassRoute.At(s).height);

        // How far that is from PassRoute's height
        public static float Offset(float s) => Surface(s) - PassRoute.At(s).height;

        // The pit's floor is bare rock, drawn as a cliff is: on the road's strip (and a cell over its edge), over the pit
        public bool Rocky(float x, float z)
        {
            var p = new Vector2(x, z);
            return PassRoute.Within(p, PassRoute.HalfWidth + 1f) && InThePit(PassRoute.Nearest(p).s);
        }

        // Whether `s` along is over the pit, where no road's laid
        public static bool InThePit(float s) => s > JumpAt && s < JumpFar;

        // Where the road's shaped by them, all told: where Road lays it a metre at a time, to follow the shape
        public static bool Shaped(float s) => MathF.Abs(s - FordAt) < BedHalf + Bank + 1f || (s > JumpAt - RampLength - 1f && s < JumpFar + 1f);

        // Which way the river runs, across the ground: across the road, to its right going towards the town
        public static Vector2 Flow
        {
            get
            {
                var heading = PassRoute.At(FordAt).heading;
                return new Vector2(-heading.Y, heading.X);
            }
        }

        public IReadOnlyDictionary<string, Start> Starts { get; } = new Dictionary<string, Start>
        {
            ["ford"] = InTheCar(FordAt - 80f),     // on the shelf, heading for the ford
            ["jump"] = InTheCar(JumpAt - 90f),     // on the straight up to the jump: run-up enough, flat out
        };

        private static Start InTheCar(float s) => new(PassRoute.At(s).at, PassRoute.YawAt(s), Above: 2f, InCar: true);

        // The river where it crosses the road: a rectangle along it, the road's width (a little in from its edges, so
        // none of it hangs out over the drop beyond), flowing across it
        public static Pool Ford
        {
            get
            {
                var (at, _, heading) = PassRoute.At(FordAt);
                return new Pool(at, 0f, FordLevel, new Vector2(BedHalf + Bank, PassRoute.HalfWidth - 0.3f),
                    Turn: MathF.Atan2(heading.Y, heading.X), Current: Flow * FlowSpeed);
            }
        }

        public IEnumerable<Pool> Pools(Terrain terrain)
        {
            yield return Ford;
        }

        // The river coming down the mountainside into the ford, and going on over the edge and down the cliff into the
        // lake: water lying on the ground, across the ford's bed
        public IEnumerable<Fixture> Fixtures(Terrain terrain)
        {
            var flow = Flow;
            yield return Falls(terrain, "ford-falls-in", -flow, 14f, null);
            yield return Falls(terrain, "ford-falls-out", flow, 40f, PassTerrain.CliffLake.Level);
            yield return StairMesh();
            foreach (var sign in JumpSigns.Fixtures())
                yield return sign;
        }

        // Water draped over the ground from the ford's edge on the side `outward`, `reach` out, or until it's down to
        // `downTo`: rows of points across the bed, a little above the ground
        private static Fixture Falls(Terrain terrain, string name, Vector2 outward, float reach, float? downTo)
        {
            const float step = 0.5f, lift = 0.3f;
            var rows = new List<Vector3[]>();
            for (var s = FordAt - BedHalf; s <= FordAt + BedHalf + 0.01f; s += 1f)
            {
                var (at, _, _) = PassRoute.At(s);
                var row = new List<Vector3>();
                for (var d = PassRoute.HalfWidth - 0.3f; d <= PassRoute.HalfWidth + reach; d += step)
                {
                    var p = at + outward * d;
                    var ground = terrain.HeightAt(p.X, p.Y);
                    row.Add(new Vector3(p.X, MathF.Max(ground + lift, d < PassRoute.HalfWidth ? FordLevel : float.MinValue), p.Y));
                    if (downTo is { } bottom && ground < bottom)
                        break;
                }
                rows.Add(row.ToArray());
            }
            var shape = rows.ToArray();
            return new Fixture(new MeshSource(name, d =>
            {
                var mesh = new MeshBuilder();
                for (var r = 1; r < shape.Length; r++)
                {
                    var (a, b) = (shape[r - 1], shape[r]);
                    for (var k = 1; k < Math.Min(a.Length, b.Length); k++)
                        mesh.AddQuad(0, a[k - 1], a[k], b[k], b[k - 1]);
                }
                return mesh.Build(d);
            }, FallsPalette), Matrix.Identity);
        }

        private static readonly Color[] FallsPalette = { new Color(70, 130, 200) };

        // Streaks of foam going across the ford with the current, in white, so it shows which way it's going (and how
        // fast) whatever the colours: each a line a metre and a half long, round and round from one side to the other
        public IEnumerable<ScenePart> Moving(Terrain terrain)
        {
            var foam = new MeshSource("ford-foam", d =>
            {
                var mesh = new MeshBuilder();
                mesh.AddLine(Vector3.Zero, new Vector3(1.5f, 0f, 0f));
                return mesh.Build(d);
            }, Array.Empty<Color>());
            var flow = Flow;
            var turn = Matrix.CreateRotationY(-MathF.Atan2(flow.Y, flow.X));   // its +X the way the water's going
            var width = 2f * PassRoute.HalfWidth - 2.5f;
            var random = new Random(7);
            for (var k = 0; k < 14; k++)
            {
                var along = FordAt - BedHalf + 1f + (float)random.NextDouble() * (2f * BedHalf - 2f);
                var (at, _, _) = PassRoute.At(along);
                var start = at - flow * (width / 2f);
                var phase = (float)random.NextDouble() * width;
                yield return new ScenePart(foam, t =>
                {
                    var p = start + flow * ((t * FlowSpeed + phase) % width);
                    return turn * Matrix.CreateTranslation(p.X, FordLevel + 0.03f, p.Y);
                });
            }
        }

        // ---- The way back up from the lake: a stair up the cliff under the road's right-hand edge, a little before the
        // ford, from under the water to the road, each step a ledge (see Ledge), StairRise up and StairRun on from the
        // one before. They hang out over the lake, StairInner to StairOuter from the road's middle, clear of the cliff;
        // the top one reaches in to the road.
        public const float StairFrom = 752f;   // the bottom step, along the road
        public const float StairRise = 0.25f, StairRun = 0.35f, StairInner = 6.3f, StairOuter = 7.4f, StepThickness = 0.3f;

        // Each step, from the bottom: its two ends, across the stair, its top, and how far it reaches either way along
        // the road. The last, level with the road, is a landing, Landing deep, to step off onto it from.
        public static IReadOnlyList<(Vector2 inner, Vector2 outer, float top, float half)> Stair { get; } = MakeStair();
        public const float Landing = 1.2f;

        private static List<(Vector2, Vector2, float, float)> MakeStair()
        {
            var steps = new List<(Vector2, Vector2, float, float)>();
            var top = PassTerrain.CliffLake.Level - CharacterController.SwimDepth + 0.1f;   // swimming, a step up from your feet
            for (var s = StairFrom; ; s += StairRun, top += StairRise)
            {
                var (at, road, heading) = PassRoute.At(s);
                var right = new Vector2(-heading.Y, heading.X);
                if (road - (top - StairRise) <= StairRise + 0.03f)   // the road's no more than a step up from the last: the landing
                {
                    var middle = PassRoute.At(s + (Landing - StairRun) / 2f).at;
                    steps.Add((middle + right * (PassRoute.HalfWidth - 0.3f), middle + right * StairOuter, road, Landing / 2f));
                    return steps;
                }
                steps.Add((at + right * StairInner, at + right * StairOuter, top, StairRun / 2f));
            }
        }

        public IEnumerable<Ledge> Ledges(Terrain terrain)
        {
            foreach (var (inner, outer, top, half) in Stair)
                yield return new Ledge(inner, outer, half + 0.02f, top - StepThickness, top);
        }

        // The stair's steps, in stone, square-ended (their ledges are round at the ends, a little past them)
        private static Fixture StairMesh()
        {
            var steps = new List<Vector3[]>();
            foreach (var (inner, outer, top, half) in Stair)
            {
                var across = Vector2.Normalize(outer - inner);
                var along = new Vector2(across.Y, -across.X) * half;
                Vector3 At(Vector2 p, float y) => new Vector3(p.X, y, p.Y);
                steps.Add(new[] { At(inner - along, top), At(inner + along, top), At(outer + along, top), At(outer - along, top) });
            }
            var shape = steps.ToArray();
            var palette = new Color[3];
            MeshBuilder.SetBoxShades(palette, 0, new Color(150, 140, 125));
            return new Fixture(new MeshSource("ford-stair", d =>
            {
                var mesh = new MeshBuilder();
                var down = Vector3.Down * StepThickness;
                foreach (var t in shape)
                {
                    mesh.AddQuad(MeshBuilder.Top, t[0], t[1], t[2], t[3]);
                    for (var k = 0; k < 4; k++)
                    {
                        var (a, b) = (t[k], t[(k + 1) % 4]);
                        mesh.AddQuad(k % 2 == 0 ? MeshBuilder.Dim : MeshBuilder.Side, a, b, b + down, a + down);
                        mesh.AddLine(a, b);
                        mesh.AddLine(a + down, b + down);
                        mesh.AddLine(a, a + down);
                    }
                }
                return mesh.Build(d);
            }, palette), Matrix.Identity);
        }

        private static float SmoothStep(float t) => t * t * (3f - 2f * t);
    }
}
