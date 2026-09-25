using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;

namespace World.Core
{
    // A heightfield: a grid of Width x Depth square cells, CellSize metres each, centred on the world's
    // origin, with a height at every grid corner. Every cell is split into two triangles along the same
    // diagonal (from its north-west corner to its south-east one), and HeightAt interpolates within those
    // triangles, not bilinearly across the cell - so it matches the mesh drawn from it exactly, and feet
    // never float above it or sink into it.
    public sealed class Terrain : IGround
    {
        // Steeper than this is a cliff: you can't stand on it or walk up it.
        public const float MaxWalkSlopeDegrees = 45f;
        private static readonly float MinWalkNormalY = MathF.Cos(MathHelper.ToRadians(MaxWalkSlopeDegrees));

        private readonly float[] _heights;   // (Width + 1) x (Depth + 1) corners, row by row along X

        public int Width { get; }
        public int Depth { get; }
        public float CellSize { get; }

        // Where grid corner (0, 0) is in the world: the north-west corner of the whole terrain.
        public float OriginX => -Width * CellSize / 2f;
        public float OriginZ => -Depth * CellSize / 2f;

        public Terrain(int width, int depth, float cellSize, float[] heights)
        {
            if (width < 1 || depth < 1)
                throw new ArgumentException("A terrain needs at least one cell each way.");
            if (heights.Length != (width + 1) * (depth + 1))
                throw new ArgumentException($"Expected {(width + 1) * (depth + 1)} corner heights, got {heights.Length}.", nameof(heights));
            Width = width;
            Depth = depth;
            CellSize = cellSize;
            _heights = heights;
        }

        // Samples `height(x, z)` (world coordinates) at every grid corner.
        public static Terrain FromFunction(int width, int depth, float cellSize, Func<float, float, float> height)
        {
            var heights = new float[(width + 1) * (depth + 1)];
            for (var j = 0; j <= depth; j++)
                for (var i = 0; i <= width; i++)
                    heights[j * (width + 1) + i] = height(-width * cellSize / 2f + i * cellSize, -depth * cellSize / 2f + j * cellSize);
            return new Terrain(width, depth, cellSize, heights);
        }

        public float CornerHeight(int i, int j) => _heights[j * (Width + 1) + i];

        public Vector3 Corner(int i, int j) => new Vector3(OriginX + i * CellSize, CornerHeight(i, j), OriginZ + j * CellSize);

        // The two triangles of cell (i, j): first the one on the north-east side of the diagonal, then the
        // south-west one. HeightAt, NormalAt and the drawn mesh all use exactly these.
        public (Vector3 a, Vector3 b, Vector3 c) Triangle(int i, int j, bool southWest) => southWest
            ? (Corner(i, j), Corner(i + 1, j + 1), Corner(i, j + 1))
            : (Corner(i, j), Corner(i + 1, j), Corner(i + 1, j + 1));

        public bool Contains(float x, float z)
        {
            var u = (x - OriginX) / CellSize;
            var v = (z - OriginZ) / CellSize;
            return u >= 0f && v >= 0f && u <= Width && v <= Depth;
        }

        // Height of the surface at (x, z); off the edge, the height at the nearest point on it.
        public float HeightAt(float x, float z)
        {
            var (i, j, fx, fz) = Locate(x, z);
            var a = CornerHeight(i, j);
            var c = CornerHeight(i + 1, j + 1);
            if (fx >= fz)
                return a + (CornerHeight(i + 1, j) - a) * fx + (c - CornerHeight(i + 1, j)) * fz;
            return a + (c - CornerHeight(i, j + 1)) * fx + (CornerHeight(i, j + 1) - a) * fz;
        }

        // The upward normal of the triangle under (x, z).
        public Vector3 NormalAt(float x, float z)
        {
            var (i, j, fx, fz) = Locate(x, z);
            var a = CornerHeight(i, j);
            var c = CornerHeight(i + 1, j + 1);
            float dx, dz;   // the slope: rise per metre along X and along Z
            if (fx >= fz)
            {
                dx = CornerHeight(i + 1, j) - a;
                dz = c - CornerHeight(i + 1, j);
            }
            else
            {
                dx = c - CornerHeight(i, j + 1);
                dz = CornerHeight(i, j + 1) - a;
            }
            return Vector3.Normalize(new Vector3(-dx / CellSize, 1f, -dz / CellSize));
        }

        public bool IsWalkable(float x, float z) => NormalAt(x, z).Y >= MinWalkNormalY;

        // Which cell (x, z) is in, clamped onto the grid, and how far across it (0..1 each way).
        private (int i, int j, float fx, float fz) Locate(float x, float z)
        {
            var u = MathHelper.Clamp((x - OriginX) / CellSize, 0f, Width);
            var v = MathHelper.Clamp((z - OriginZ) / CellSize, 0f, Depth);
            var i = Math.Min((int)u, Width - 1);
            var j = Math.Min((int)v, Depth - 1);
            return (i, j, u - i, v - j);
        }

        // Its lakes and ponds (see Pool), filled once it's made: see Flood.
        public IReadOnlyList<Pool> Pools { get; private set; } = Array.Empty<Pool>();

        public Terrain Flood(params Pool[] pools)
        {
            Pools = pools;
            return this;
        }

        // The surface of whichever pool is over (x, z), where the ground there is below it; null where it's dry.
        public float? WaterLevelAt(float x, float z)
        {
            float? level = null;
            foreach (var pool in Pools)
                if (pool.Covers(x, z) && (!level.HasValue || pool.Level > level.Value) && HeightAt(x, z) < pool.Level)
                    level = pool.Level;
            return level;
        }

        float? IGround.GroundBelow(Vector3 feet, float reach) => Contains(feet.X, feet.Z) ? HeightAt(feet.X, feet.Z) : null;
        float? IGround.WaterAt(Vector3 point) => WaterLevelAt(point.X, point.Z);
        Vector3 IGround.NormalAt(Vector3 feet) => NormalAt(feet.X, feet.Z);
        bool IGround.IsWalkable(Vector3 feet) => IsWalkable(feet.X, feet.Z);
    }
}
