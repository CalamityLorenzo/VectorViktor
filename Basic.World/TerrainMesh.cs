using MeshCore.Library;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using World.Core;

namespace Basic.World
{
    // The terrain as one mesh: every triangle HeightAt uses, flat-coloured by what it is - sand round the
    // lakes and ponds (up to `shore` above their water), grass in three bands of height, rock wherever
    // it's too steep to walk - and shaded in three steps by how squarely it faces the light, so the hills'
    // shapes read between the grid lines, which are drawn every couple of cells, in white like everything else. Cliff faces get no grid, only an
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

        // Cells i0 to i0 + cellsX (not including it) along X, and j0 to j0 + cellsZ along Z. Where `bare`
        // says so (under a road, say), the ground gets no grid lines: they'd show through what's on it.
        public static MeshData Build(GraphicsDevice device, Terrain terrain, float shore, int i0, int j0, int cellsX, int cellsZ,
                                     Func<float, float, bool> bare = null)
        {
            // Every triangle's facing is worked out once, here, for this range's cells and the triangles the
            // lines along its north and west edges look across into - the south-west ones of the row of cells
            // north of it, the north-east ones of the column west of it (the lines on its south and east edges
            // are the next range's, unless they're the terrain's own rim). Only those, so as not to work out the
            // heights of chunks beyond them: whether it's rock (-1 off the terrain, or not needed). The range's own
            // triangles go into the mesh as they're found, each in its colour (see MeshBuilder).
            var mesh = new MeshBuilder();
            var w = cellsX + 2;
            var rock = new sbyte[w * (cellsZ + 2) * 2];
            int RockIndex(int i, int j, bool southWest) => ((j - j0 + 1) * w + (i - i0 + 1)) * 2 + (southWest ? 1 : 0);

            for (var j = j0 - 1; j <= j0 + cellsZ; j++)
                for (var i = i0 - 1; i <= i0 + cellsX; i++)
                    for (var k = 0; k < 2; k++)
                    {
                        var southWest = k == 1;
                        var alongside = (j == j0 - 1 && i >= i0 && i < i0 + cellsX && southWest) ||
                                        (i == i0 - 1 && j >= j0 && j < j0 + cellsZ && !southWest);
                        var own = i >= i0 && j >= j0 && i < i0 + cellsX && j < j0 + cellsZ;
                        if (i < 0 || j < 0 || i >= terrain.Width || j >= terrain.Depth || !(own || alongside))
                        {
                            rock[RockIndex(i, j, southWest)] = -1;
                            continue;
                        }
                        var normal = terrain.TriangleNormal(i, j, southWest);
                        var steep = !Terrain.IsWalkableNormal(normal);
                        rock[RockIndex(i, j, southWest)] = steep ? (sbyte)1 : (sbyte)0;
                        if (!own)
                            continue;
                        var (a, b, c) = terrain.Triangle(i, j, southWest);
                        var band = steep ? Rock : Band(terrain, (a + b + c) / 3f, shore);
                        mesh.AddTri(band * Shades + Shade(normal), a, b, c);
                    }

            // The lines. On open ground, a grid: every GridEvery-th line of cell edges each way - every
            // cell's would crowd so close in the distance, at low resolution, that far hills turn white. A
            // cliff face gets no grid at all, only its outline: wherever a rock triangle meets one that isn't
            // (its crest and its foot, diagonals and all). And always the terrain's own rim.
            bool? IsRock(int i, int j, bool southWest) => rock[RockIndex(i, j, southWest)] switch { -1 => null, 0 => false, _ => true };

            var lines = 0;
            void Edge(Vector3 a, Vector3 b, bool? one, bool? other, bool onGrid)
            {
                var outline = one.HasValue && other.HasValue && one.Value != other.Value;
                var rim = !one.HasValue || !other.HasValue;
                var open = one == false && other == false && onGrid;
                if (!outline && !rim && !open)
                    return;
                var middle = (a + b) / 2f;
                if (bare != null && bare(middle.X, middle.Z))
                    return;
                mesh.AddLine(a, b);
                lines++;
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
                        Edge(terrain.Corner(i, j), terrain.Corner(i + 1, j), IsRock(i, j - 1, true), IsRock(i, j, false), j % GridEvery == 0);
                    // Along Z at column i: the north-east triangle of the cell to its west, the south-west one of the cell to its east
                    if (j < j0 + cellsZ)
                        Edge(terrain.Corner(i, j), terrain.Corner(i, j + 1), IsRock(i - 1, j, false), IsRock(i, j, true), i % GridEvery == 0);
                    // Each cell's own diagonal: never part of the grid, only ever of an outline
                    if (i < i0 + cellsX && j < j0 + cellsZ)
                        Edge(terrain.Corner(i, j), terrain.Corner(i + 1, j + 1), IsRock(i, j, false), IsRock(i, j, true), false);
                }

            // A chunk that's all cliff face has no lines of its own, and a line buffer can't be empty
            if (lines == 0)
                mesh.AddLine(terrain.Corner(i0, j0), terrain.Corner(i0, j0));
            return mesh.Build(device);
        }

        // Which colour walkable ground is (steep ground is always Rock): sand at the water's edge, or grass by height.
        private static int Band(Terrain terrain, Vector3 centre, float shore)
        {
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
