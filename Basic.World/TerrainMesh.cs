using MeshCore.Library;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using World.Core;

namespace Basic.World
{
    // The terrain as one mesh: every triangle HeightAt uses, flat-coloured by what it is - sand round the
    // lakes and ponds (up to `shore` above their water), grass in three bands of height, rock wherever
    // it's too steep to walk - and shaded in three steps by how squarely it faces the light, so the hills' shapes read between the grid lines, which
    // are drawn every couple of cells, in white like everything else. Cliff faces get no grid, only an
    // outline where the rock ends. The mesh is in world coordinates. A big terrain is drawn a chunk at a
    // time (see Terrain.ChunkCellsOf): each chunk's mesh is just its own cells, the lines along its far
    // edges left to the chunks beyond it, unless it's at the terrain's own edge.
    public static class TerrainMesh
    {
        private const int Sand = 0, GrassLow = 1, GrassMid = 2, GrassHigh = 3, Rock = 4, Bands = 5;
        private const int Shades = 3;
        private const int GridEvery = 2;   // cells between grid lines

        private static readonly Vector3 Light = Vector3.Normalize(new Vector3(-0.5f, 1f, -0.3f));   // from high in the north-west

        public static Color[] Palette()
        {
            var bands = new[]
            {
                new Color(200, 180, 110),   // sand
                new Color(60, 150, 60),     // grass, low
                new Color(80, 170, 70),     // grass, mid
                new Color(110, 180, 90),    // grass, high
                new Color(120, 110, 100),   // rock
            };
            var palette = new Color[Bands * Shades];
            for (var b = 0; b < Bands; b++)
            {
                palette[b * Shades + 0] = Color.Lerp(bands[b], Color.White, 0.15f);
                palette[b * Shades + 1] = bands[b];
                palette[b * Shades + 2] = Color.Lerp(bands[b], Color.Black, 0.3f);
            }
            return palette;
        }

        public static MeshData Build(GraphicsDevice device, Terrain terrain, float shore) =>
            Build(device, terrain, shore, 0, 0, terrain.Width, terrain.Depth);

        // Cells i0 to i0 + cellsX (not including it) along X, and j0 to j0 + cellsZ along Z.
        public static MeshData Build(GraphicsDevice device, Terrain terrain, float shore, int i0, int j0, int cellsX, int cellsZ)
        {
            // Gathered per colour first: MeshData's draw ranges must each be one unbroken run
            var slots = new List<(Vector3 a, Vector3 b, Vector3 c)>[Bands * Shades];
            for (var s = 0; s < slots.Length; s++)
                slots[s] = new List<(Vector3, Vector3, Vector3)>();

            for (var j = j0; j < j0 + cellsZ; j++)
                for (var i = i0; i < i0 + cellsX; i++)
                    foreach (var southWest in new[] { false, true })
                    {
                        var triangle = terrain.Triangle(i, j, southWest);
                        var centre = (triangle.a + triangle.b + triangle.c) / 3f;
                        var normal = terrain.NormalAt(centre.X, centre.Z);
                        slots[Band(terrain, centre, shore) * Shades + Shade(normal)].Add(triangle);
                    }

            var mesh = new MeshBuilder();
            for (var s = 0; s < slots.Length; s++)
            {
                if (slots[s].Count == 0)
                    continue;
                mesh.AddSolidRange(slots[s].Count, s);
                foreach (var (a, b, c) in slots[s])
                    mesh.AddTri(a, b, c);
            }

            // The lines. On open ground, a grid: every GridEvery-th line of cell edges each way - every
            // cell's would crowd so close in the distance, at low resolution, that far hills turn white. A
            // cliff face gets no grid at all, only its outline: wherever a rock triangle meets one that isn't
            // (its crest and its foot, diagonals and all). And always the terrain's own rim.
            bool? Rock(int i, int j, bool southWest) =>
                i < 0 || j < 0 || i >= terrain.Width || j >= terrain.Depth ? null : IsRock(terrain, i, j, southWest);

            var lines = 0;
            void Edge(Vector3 a, Vector3 b, bool? one, bool? other, bool onGrid)
            {
                var outline = one.HasValue && other.HasValue && one.Value != other.Value;
                var rim = !one.HasValue || !other.HasValue;
                var open = one == false && other == false && onGrid;
                if (outline || rim || open)
                {
                    mesh.AddLine(a, b);
                    lines++;
                }
            }

            // This range's own cells' north and west edges, and its south and east edges only at the terrain's
            // own edge (otherwise they're the next chunk's north and west ones)
            var lastJ = j0 + cellsZ == terrain.Depth ? j0 + cellsZ : j0 + cellsZ - 1;
            var lastI = i0 + cellsX == terrain.Width ? i0 + cellsX : i0 + cellsX - 1;
            for (var j = j0; j <= lastJ; j++)
                for (var i = i0; i <= lastI; i++)
                {
                    // Along X at row j: the south-west triangle of the cell to its north, the north-east one of the cell to its south
                    if (i < i0 + cellsX)
                        Edge(terrain.Corner(i, j), terrain.Corner(i + 1, j), Rock(i, j - 1, true), Rock(i, j, false), j % GridEvery == 0);
                    // Along Z at column i: the north-east triangle of the cell to its west, the south-west one of the cell to its east
                    if (j < j0 + cellsZ)
                        Edge(terrain.Corner(i, j), terrain.Corner(i, j + 1), Rock(i - 1, j, false), Rock(i, j, true), i % GridEvery == 0);
                    // Each cell's own diagonal: never part of the grid, only ever of an outline
                    if (i < i0 + cellsX && j < j0 + cellsZ)
                        Edge(terrain.Corner(i, j), terrain.Corner(i + 1, j + 1), Rock(i, j, false), Rock(i, j, true), false);
                }

            // A chunk that's all cliff face has no lines of its own, and a line buffer can't be empty
            if (lines == 0)
                mesh.AddLine(terrain.Corner(i0, j0), terrain.Corner(i0, j0));
            return mesh.Build(device);
        }

        // Too steep to walk: drawn as rock, and outlined rather than gridded.
        private static bool IsRock(Terrain terrain, int i, int j, bool southWest)
        {
            var (a, b, c) = terrain.Triangle(i, j, southWest);
            var centre = (a + b + c) / 3f;
            return !terrain.IsWalkable(centre.X, centre.Z);
        }

        private static int Band(Terrain terrain, Vector3 centre, float shore)
        {
            if (!terrain.IsWalkable(centre.X, centre.Z))
                return Rock;
            foreach (var pool in terrain.Pools)
                if (pool.Covers(centre.X, centre.Z) && centre.Y < pool.Level + shore)
                    return Sand;
            if (centre.Y < 1.5f)
                return GrassLow;
            return centre.Y < 4f ? GrassMid : GrassHigh;
        }

        private static int Shade(Vector3 normal)
        {
            var d = Vector3.Dot(normal, Light);
            return d > 0.95f ? 0 : d > 0.8f ? 1 : 2;
        }
    }
}
