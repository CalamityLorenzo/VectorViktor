using MeshCore.Library;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;

namespace MeshRawData
{
    // The paved edge round a swimming pool: four slabs making a frame round a rectangle `halfX` by `halfZ`
    // either side of the origin (the water's edge), reaching Outside beyond it and Inside over it, their
    // tops a little above y = 0 - the level of the ground round the pool. Three draw ranges in all.
    public static class PoolSurroundMesh
    {
        public const int Stone = 0;
        public const int PaletteSize = 3;

        private const float Outside = 0.5f, Inside = 0.25f, Thickness = 0.12f, Above = 0.03f;

        public static Color[] Palette(Color stone)
        {
            var palette = new Color[PaletteSize];
            MeshBuilder.SetBoxShades(palette, Stone, stone);
            return palette;
        }

        public static MeshData Build(GraphicsDevice device, float halfX, float halfZ)
        {
            var mesh = new MeshBuilder();
            var faces = new[] { new List<Vector3[]>(), new List<Vector3[]>(), new List<Vector3[]>() };   // side, end, top

            void Slab(float x0, float z0, float x1, float z1)
            {
                float y0 = Above - Thickness, y1 = Above;
                Vector3 a = new(x0, y0, z0), b = new(x1, y0, z0), c = new(x1, y0, z1), d = new(x0, y0, z1);
                Vector3 e = new(x0, y1, z0), f = new(x1, y1, z0), g = new(x1, y1, z1), h = new(x0, y1, z1);
                faces[MeshBuilder.Side].Add(new[] { a, b, f, e });
                faces[MeshBuilder.Side].Add(new[] { c, d, h, g });
                faces[MeshBuilder.Dim].Add(new[] { b, c, g, f });
                faces[MeshBuilder.Dim].Add(new[] { d, a, e, h });
                faces[MeshBuilder.Top].Add(new[] { e, f, g, h });
                mesh.AddLineLoop(e, f, g, h);
            }

            float ox = halfX + Outside, oz = halfZ + Outside, ix = halfX - Inside, iz = halfZ - Inside;
            Slab(-ox, -oz, ox, -iz);   // north
            Slab(-ox, iz, ox, oz);     // south
            Slab(-ox, -iz, -ix, iz);   // west
            Slab(ix, -iz, ox, iz);     // east

            for (var shade = 0; shade < faces.Length; shade++)
            {
                mesh.AddSolidRange(faces[shade].Count * 2, Stone + shade);
                foreach (var quad in faces[shade])
                    mesh.AddQuad(quad[0], quad[1], quad[2], quad[3]);
            }
            return mesh.Build(device);
        }
    }
}
