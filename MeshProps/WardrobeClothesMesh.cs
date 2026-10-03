using MeshCore.Library;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace MeshProps
{
    // What's in a wardrobe (a two-door cupboard Width x Depth x Height, see World.Buildings' CabinetSpec, with a shelf
    // half way up), crammed in so it won't stay in: clothes on hangers along a rail under the top, a pile folded on the
    // floor of it, and a sleeve and a trouser leg shut in the doors, poking out between them. Its origin is the middle of the
    // cupboard's footprint on the floor, its front towards +Z, as the cupboard's: placed and turned as the cupboard is.
    public static class WardrobeClothesMesh
    {
        // Four cloths, three shades each (see MeshBuilder), and the rail and hangers
        public const int ClothA = 0, ClothB = 3, ClothC = 6, ClothD = 9, Rail = 12;
        public const int PaletteSize = 15;

        private const float Proud = 0.02f;   // how far the cupboard's doors stand out from its front (see CabinetMesh)

        public static Color[] Palette(Color a, Color b, Color c, Color d)
        {
            var palette = new Color[PaletteSize];
            MeshBuilder.SetBoxShades(palette, ClothA, a);
            MeshBuilder.SetBoxShades(palette, ClothB, b);
            MeshBuilder.SetBoxShades(palette, ClothC, c);
            MeshBuilder.SetBoxShades(palette, ClothD, d);
            MeshBuilder.SetBoxShades(palette, Rail, new Color(190, 190, 195));
            return palette;
        }

        public static MeshSource Source(float width, float depth, float height, Color[] palette) =>
            new MeshSource($"wardrobeclothes:{width:F2}x{depth:F2}x{height:F2}", d => Build(d, width, depth, height), palette);

        public static MeshData Build(GraphicsDevice device, float width, float depth, float height)
        {
            var mesh = new MeshBuilder();
            const float board = 0.02f, plinth = 0.06f;
            var inside = width - 2f * board;
            var cloths = new[] { ClothA, ClothB, ClothC, ClothD, ClothB, ClothA };

            // The rail, across under the top, and on it the clothes, hung side on, each on a hanger
            var rail = height - board - 0.1f;
            mesh.AddTube(new Vector3(-inside / 2f, rail, 0f), new Vector3(inside / 2f, rail, 0f), 0.012f, 0.012f, 6, Rail);
            var shoulders = depth - 0.12f;
            for (var k = 0; k < cloths.Length; k++)
            {
                var x = -inside / 2f + (k + 0.5f) * inside / cloths.Length;
                var length = k % 2 == 0 ? 0.7f : 0.62f;   // a coat, a shirt...
                mesh.AddBox(cloths[k], new Vector3(x, rail - 0.06f - length, 0f), shoulders, 0.05f, length);
                mesh.AddLine(new Vector3(x, rail + 0.012f, 0f), new Vector3(x, rail - 0.06f, -shoulders / 2f));   // the hanger
                mesh.AddLine(new Vector3(x, rail + 0.012f, 0f), new Vector3(x, rail - 0.06f, shoulders / 2f));
            }

            // Folded on the bottom, a pile not quite square
            var floor = plinth;
            foreach (var (cloth, x, z, h) in new[] { (ClothC, -0.12f, 0f, 0.09f), (ClothD, -0.1f, 0.02f, 0.07f), (ClothA, -0.13f, -0.01f, 0.08f) })
            {
                mesh.AddBox(cloth, new Vector3(x, floor, z), 0.32f, 0.36f, h);
                floor += h;
            }

            // Shut in the doors: a sleeve from a shirt inside, and lower down a trouser leg, each squashed flat through the gap
            // between the doors (thinner than it, so it doesn't cut into them) and hanging out over their fronts, clear of them
            var front = depth / 2f + Proud + 0.012f;   // just in front of the doors' faces
            foreach (var (cloth, top, length, radius) in new[] { (ClothB, 1.5f, 0.38f, 0.04f), (ClothD, 0.42f, 0.3f, 0.05f) })
            {
                var (inner, outer) = (depth / 2f - 0.1f, front + radius);
                mesh.AddBox(cloth, new Vector3(0f, top - 0.12f, (inner + outer) / 2f), outer - inner, 0.004f, 0.12f);   // through the gap
                var lip = new Vector3(0f, top - 0.06f, front + radius);
                mesh.AddTube(lip, lip + new Vector3(0.02f, -length, 0.03f), radius, radius * 0.85f, 6, cloth, ringEdges: true);
            }
            return mesh.Build(device);
        }
    }
}
