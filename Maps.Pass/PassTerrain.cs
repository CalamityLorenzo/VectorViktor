using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using World.Core;

namespace Maps.Pass
{
    // The pass's ground: mountains, and one way through them - the road's (see PassRoute). Its level strip winds up
    // and down between walls rising steeper than anyone can walk (see Terrain.MaxWalkSlopeDegrees), so there's no
    // leaving it but by falling, and nowhere to go if you do. Along its way: a tarn in a deep hollow just below the road
    // down from the first pass; a lake under cliffs, the road on a shelf above its north shore, a drop to the water
    // from both; and at the far end a
    // flat basin, the mountains curving round it like a bay's shore, with a lake in its middle, where a town will go.
    //
    // Everywhere, the ground's the lowest of what each of these would make it on its own: the road's strip, the walls
    // rising from it; a flat's level, the walls rising from its edge; a lake's floor, the cliffs rising from its shore
    // - all but on the road's strip, which is never cut into, so where a lake or the basin lies lower beside it the
    // road's edge is the top of a drop. The walls stand sheer for WallFoot at their feet, then climb at WallSlope, give
    // or take WallWobble, and ease off into summits at about SummitHeight, however high they rose from. (The sheer foot
    // makes every triangle of the terrain across a wall's foot too steep to walk, so it's drawn as rock and the level
    // ground's drawn level up to it. Rising any less steeply at first, some of them would be drawn as grass tipped up
    // the wall, a row of jagged teeth along the road's edge.) Same seed and pads, same mountains.
    public static class PassTerrain
    {
        public const int Size = 1024;         // cells each way
        public const float CellSize = 1f;

        public const float WallSlope = 1.6f;          // rise over run: about 58 degrees
        public const float WallFoot = 1.5f;           // how high a wall stands sheer at its foot, before it slopes away up
        public const float WallWobble = 0.2f;         // how much steeper or shallower than that it is, from place to place
        public const float SummitHeight = 250f;       // about how high the summits are, all round, above the road's highest...
        public const float SummitSwing = 50f;         // ...give or take this

        // A flat or a lake: the ground inside Circles, level at Level - or for a lake, its water's surface at Level and
        // its floor shelving Depth below that. Its edge is only ever where it is, with no wandering in and out of its
        // own: the walls rising from it must be as steep all the way along it as they are from the road.
        public sealed record Hollow(string Name, (Vector2 centre, float radius)[] Circles, float Level, float Depth = 0f)
        {
            // How far outside its edge (x, z) is: negative inside
            public float Outside(float x, float z)
            {
                var outside = float.MaxValue;
                foreach (var (centre, radius) in Circles)
                    outside = MathF.Min(outside, Vector2.Distance(new Vector2(x, z), centre) - radius);
                return outside;
            }

            // Water to fill it, if it's a lake
            public IEnumerable<Pool> Water()
            {
                foreach (var (centre, radius) in Circles)
                    yield return new Pool(centre, radius + 1f, Level);
            }
        }

        // The trailhead: a level space round the road's start
        public static readonly Hollow TrailheadFlat = new("trailhead", new[] { (PassRoute.Controls[PassRoute.Trailhead].at, 16f) },
                                                          PassRoute.Controls[PassRoute.Trailhead].height);

        // The tarn, deep in a hollow just south of the road down from the first pass
        public static readonly Hollow Tarn = new("tarn", new[] { (new Vector2(-192f, 44f), 20f), (new Vector2(-172f, 62f), 14f), (new Vector2(-210f, 60f), 11f) },
                                                 58f, 5f);

        // The lake under the cliffs, below the road's shelf
        public static readonly Hollow CliffLake = new("cliff lake", new[]
        {
            (new Vector2(-60f, 122f), 55f), (new Vector2(-20f, 112f), 40f), (new Vector2(5f, 145f), 50f), (new Vector2(-112f, 155f), 35f), (new Vector2(-40f, 185f), 40f),
            (new Vector2(45f, 118f), 22f), (new Vector2(-98f, 118f), 24f), (new Vector2(30f, 190f), 28f), (new Vector2(-128f, 188f), 18f),
        }, 55f, 9f);

        // The basin at the far end, and the lake in its middle: its water TownLakeLevel, a shore easing down to it
        // from the basin's level over TownLakeShore, and deepening to TownLakeDepth
        public static readonly Hollow Basin = new("basin", new[]
        {
            (new Vector2(300f, 270f), 170f), (new Vector2(420f, 170f), 80f), (new Vector2(160f, 380f), 90f), (new Vector2(440f, 380f), 90f),
            (new Vector2(200f, 170f), 55f), (new Vector2(470f, 260f), 45f), (new Vector2(90f, 330f), 45f), (new Vector2(300f, 455f), 45f),
            (new Vector2(470f, 320f), 40f),
        }, 14f);
        public static readonly Vector2 TownLakeCentre = new Vector2(300f, 280f);
        public const float TownLakeRadius = 55f, TownLakeShore = 14f, TownLakeDepth = 4f, TownLakeWander = 6f;
        public static readonly float TownLakeLevel = Basin.Level - 0.4f;

        public static IReadOnlyList<Hollow> Flats { get; } = new[] { TrailheadFlat, Basin };
        public static IReadOnlyList<Hollow> Lakes { get; } = new[] { Tarn, CliffLake };

        public static Terrain Create(int seed = 1, IReadOnlyList<TerrainGenerator.Pad>? pads = null)
        {
            var random = new Random(seed);
            var wobble = Waves(random, 220f, 100f);
            var summits = Waves(random, 320f, 140f);
            var road = new RoadField(wobble, summits);

            float Ground(float x, float z)
            {
                var w = wobble(x, z);
                var m = summits(x, z);
                var (onRoad, h) = road.At(x, z, w, m);
                if (onRoad)
                    return h;
                foreach (var flat in Flats)
                {
                    var outside = flat.Outside(x, z);
                    h = MathF.Min(h, outside <= 0f ? FlatFloor(flat, x, z) : flat.Level + Rise(outside, flat.Level, w, m));
                }
                foreach (var lake in Lakes)
                {
                    var outside = lake.Outside(x, z);
                    h = MathF.Min(h, outside <= 0f ? lake.Level - lake.Depth * SmoothStep(MathF.Min(-outside / 12f, 1f))
                                                    : lake.Level + Rise(outside, lake.Level, w, m));
                }
                return h;
            }

            var water = new List<Pool>();
            foreach (var lake in Lakes)
                water.AddRange(lake.Water());
            water.Add(new Pool(TownLakeCentre, TownLakeRadius + TownLakeShore + TownLakeWander + 1f, TownLakeLevel));

            return Terrain.FromFunction(Size, Size, CellSize, TerrainGenerator.Levelled(Ground, pads)).Flood(water.ToArray());
        }

        // How far a wall climbs over a run from its foot, `from` high, towards the summits, where its wobble and its
        // summit's height (see Waves) are these
        private static float Rise(float run, float from, float wobble, float summit)
        {
            if (run <= 0f)
                return 0f;
            var top = SummitHeight + SummitSwing * summit - from;
            return top * MathF.Tanh((WallFoot + WallSlope * (1f + WallWobble * wobble) * run) / top);
        }

        // A flat's level; in the basin, dipping to the town's lake
        private static float FlatFloor(Hollow flat, float x, float z)
        {
            if (flat != Basin)
                return flat.Level;
            var fromShore = TownLakeRadius + TownLakeShore - Vector2.Distance(new Vector2(x, z), TownLakeCentre) - TownLakeWander * Shoreline(x, z);
            if (fromShore <= 0f)
                return flat.Level;
            // Down to the water over the shore, then on down to the lake's depth
            var drop = flat.Level - TownLakeLevel + TownLakeDepth;
            return flat.Level - drop * SmoothStep(MathF.Min(fromShore / (TownLakeShore * 2.5f), 1f));
        }

        // Between -1 and 1, wandering over tens of metres: how far in or out a shore is
        private static float Shoreline(float x, float z) =>
            0.6f * MathF.Sin((0.8f * x + 0.6f * z) * MathHelper.TwoPi / 55f + 1.1f) + 0.4f * MathF.Sin((-0.3f * x + 0.95f * z) * MathHelper.TwoPi / 23f + 0.3f);

        // A sum of a few long waves at random angles and phases, between -1 and 1: `longest` down to `shortest` long
        private static Func<float, float, float> Waves(Random random, float longest, float shortest)
        {
            var waves = new (Vector2 direction, float wavelength, float weight, float phase)[4];
            var weights = new[] { 0.4f, 0.3f, 0.2f, 0.1f };
            for (var k = 0; k < waves.Length; k++)
            {
                var angle = (float)(random.NextDouble() * MathHelper.TwoPi);
                var wavelength = MathHelper.Lerp(longest, shortest, k / (waves.Length - 1f));
                waves[k] = (new Vector2(MathF.Cos(angle), MathF.Sin(angle)), wavelength, weights[k], (float)(random.NextDouble() * MathHelper.TwoPi));
            }
            return (x, z) =>
            {
                var sum = 0f;
                foreach (var (direction, wavelength, weight, phase) in waves)
                    sum += weight * MathF.Sin((direction.X * x + direction.Y * z) * MathHelper.TwoPi / wavelength + phase);
                return sum;
            };
        }

        private static float SmoothStep(float t) => t * t * (3f - 2f * t);

        // The road's strip and the walls rising from it: the lowest, anywhere, of the road's height at the nearest
        // point of each segment of its middle line and a wall's rise over the run from the strip's edge to there.
        //
        // Worked out exactly near the road, within Near of its strip, where it's sharp - the strip's edge, the foot of
        // the walls - from the segments that could matter there, sorted into square cells beforehand. Further off,
        // where it's all smooth mountainside, from a grid worked out beforehand every GridSpacing, between its points.
        private sealed class RoadField
        {
            private const float Cell = 16f;
            private const float Near = 16f;
            private const float Reach = 40f;          // how much further off than the nearest a segment can be and still be the lowest, near the road
            private const float GridSpacing = 2f;

            private static readonly float Origin = -Size * CellSize / 2f;
            private readonly int _cells = (int)MathF.Ceiling(Size * CellSize / Cell);
            private readonly int[][] _near;
            private readonly int _gridPoints = (int)(Size * CellSize / GridSpacing) + 1;
            private readonly float[] _grid;
            private readonly Vector2[] _points;
            private readonly float[] _heights;
            private readonly float[] _along;

            public RoadField(Func<float, float, float> wobble, Func<float, float, float> summits)
            {
                _points = new Vector2[PassRoute.Points.Count];
                _heights = new float[_points.Length];
                _along = new float[_points.Length];
                for (var k = 0; k < _points.Length; k++)
                {
                    _points[k] = PassRoute.Points[k];
                    _heights[k] = PassRoute.Heights[k];
                    _along[k] = PassRoute.Along[k];
                }
                var segments = _points.Length - 1;

                // Each cell's segments: any within Reach of the nearest to anywhere in it
                var halfDiagonal = Cell * MathF.Sqrt(0.5f);
                _near = new int[_cells * _cells][];
                Parallel.For(0, _cells, j =>
                {
                    var distances = new float[segments];
                    for (var i = 0; i < _cells; i++)
                    {
                        var centre = new Vector2(Origin + (i + 0.5f) * Cell, Origin + (j + 0.5f) * Cell);
                        var nearest = float.MaxValue;
                        for (var k = 0; k < segments; k++)
                        {
                            distances[k] = PassRoute.OnSegment(centre, _points[k], _points[k + 1]).distance;
                            nearest = MathF.Min(nearest, distances[k]);
                        }
                        var near = new List<int>();
                        if (nearest - halfDiagonal <= PassRoute.HalfWidth + Near)
                            for (var k = 0; k < segments; k++)
                                if (distances[k] <= nearest + 2f * halfDiagonal + Reach)
                                    near.Add(k);
                        _near[j * _cells + i] = near.ToArray();
                    }
                });

                // The grid, from every segment
                _grid = new float[_gridPoints * _gridPoints];
                Parallel.For(0, _gridPoints, j =>
                {
                    var z = Origin + j * GridSpacing;
                    for (var i = 0; i < _gridPoints; i++)
                    {
                        var x = Origin + i * GridSpacing;
                        _grid[j * _gridPoints + i] = Exact(x, z, wobble(x, z), summits(x, z), 0, segments, null).height;
                    }
                });
            }

            // The nearest point of the segments `from` to `to` (or those listed in `only`) to (x, z) - how far off, how
            // high - and the lowest of the walls rising from them there
            private (float distance, float roadHeight, float height) Exact(float x, float z, float wobble, float summit, int from, int to, int[]? only)
            {
                var p = new Vector2(x, z);
                float nearest = float.MaxValue, roadHeight = 0f, lowest = float.MaxValue;
                var count = only?.Length ?? to - from;
                for (var n = 0; n < count; n++)
                {
                    var k = only != null ? only[n] : from + n;
                    var (t, distance) = PassRoute.OnSegment(p, _points[k], _points[k + 1]);
                    var height = Crossings.Surface(_along[k] + (_along[k + 1] - _along[k]) * t, _heights[k] + (_heights[k + 1] - _heights[k]) * t);
                    if (distance < nearest)
                    {
                        nearest = distance;
                        roadHeight = height;
                    }
                    lowest = MathF.Min(lowest, height + Rise(MathF.Max(0f, distance - PassRoute.HalfWidth), height, wobble, summit));
                }
                return (nearest, roadHeight, lowest);
            }

            // Whether (x, z) is on the road's strip, and the height there: the road's, or the lowest of the walls
            // rising from it, with the wobble and summit (see Rise) there.
            public (bool onRoad, float height) At(float x, float z, float wobble, float summit)
            {
                var i = Math.Clamp((int)((x - Origin) / Cell), 0, _cells - 1);
                var j = Math.Clamp((int)((z - Origin) / Cell), 0, _cells - 1);
                var near = _near[j * _cells + i];
                if (near.Length > 0)
                {
                    var (distance, roadHeight, height) = Exact(x, z, wobble, summit, 0, 0, near);
                    if (distance <= PassRoute.HalfWidth)
                        return (true, roadHeight);
                    if (distance <= PassRoute.HalfWidth + Near)
                        return (false, height);
                }

                // Between the grid's points
                var gx = Math.Clamp((x - Origin) / GridSpacing, 0f, _gridPoints - 1.001f);
                var gz = Math.Clamp((z - Origin) / GridSpacing, 0f, _gridPoints - 1.001f);
                int gi = (int)gx, gj = (int)gz;
                float fx = gx - gi, fz = gz - gj;
                var row = gj * _gridPoints;
                var top = MathHelper.Lerp(_grid[row + gi], _grid[row + gi + 1], fx);
                var bottom = MathHelper.Lerp(_grid[row + _gridPoints + gi], _grid[row + _gridPoints + gi + 1], fx);
                return (false, MathHelper.Lerp(top, bottom, fz));
            }
        }
    }
}
