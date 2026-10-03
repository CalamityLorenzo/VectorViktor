using MeshCore.Library;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MeshProps
{
    // A wall shelf of books: a teak plank on two iron brackets, a row of a dozen hardbacks of different heights and
    // thicknesses standing on it with their spines flush with its front, some with gilt bands, one at the end leaning
    // back on the others, and three lying in a pile at the far end. 0.90 wide, 0.22 deep. Its back (-Z) goes against the
    // wall, the books' spines face +Z; centred on X and Z, its brackets' feet on y = 0, so put it up a wall with Above.
    public static class BookshelfMesh
    {
        // The shelf and each of the four book colours have three shades (see MeshBuilder); the brackets one, the gilt one.
        public const int ShelfBase = 0, Bracket = 3, BookBase = 4, Gilt = 16;
        public const int BookColours = 4, PaletteSize = 17;

        public const float Width = 0.90f, Depth = 0.22f, ShelfTop = 0.16f;

        public static Color[] Palette(Color shelf, Color bracket, Color bookA, Color bookB, Color bookC, Color bookD, Color gilt)
        {
            var palette = new Color[PaletteSize];
            MeshBuilder.SetBoxShades(palette, ShelfBase, shelf);
            palette[Bracket] = bracket;
            var books = new[] { bookA, bookB, bookC, bookD };
            for (var i = 0; i < BookColours; i++)
                MeshBuilder.SetBoxShades(palette, BookBase + 3 * i, books[i]);
            palette[Gilt] = gilt;
            return palette;
        }

        // The books standing up, left to right: thickness, height, depth, which colour, and whether its spine has gilt bands
        private static readonly (float Thickness, float Height, float Depth, int Colour, bool Banded)[] Row =
        {
            (0.030f, 0.24f, 0.17f, 0, true), (0.042f, 0.26f, 0.19f, 1, false), (0.025f, 0.21f, 0.15f, 2, true),
            (0.035f, 0.23f, 0.16f, 3, false), (0.050f, 0.28f, 0.20f, 0, true), (0.028f, 0.20f, 0.14f, 2, false),
            (0.032f, 0.24f, 0.17f, 1, true), (0.045f, 0.27f, 0.19f, 3, true), (0.022f, 0.19f, 0.14f, 0, false),
            (0.038f, 0.25f, 0.18f, 2, true), (0.030f, 0.22f, 0.16f, 1, false), (0.040f, 0.26f, 0.18f, 3, true),
        };

        public static MeshData Build(GraphicsDevice device)
        {
            const float plank = 0.025f, gap = 0.002f, front = Depth / 2f - 0.01f;   // `front`: where the spines are
            const float wall = -Depth / 2f + 0.005f;                                 // the brackets' upright, against the wall
            var mesh = new MeshBuilder();

            // The plank, and under it the brackets: an upright against the wall, an arm under the plank, a strut between them
            mesh.AddBox(ShelfBase, new Vector3(0f, ShelfTop - plank, 0f), Depth, Width, plank, sealBottom: true);
            const float arm = ShelfTop - plank - 0.008f;
            foreach (var x in new[] { -0.30f, 0.30f })
            {
                mesh.AddTube(new Vector3(x, 0f, wall), new Vector3(x, arm, wall), 0.008f, 0.008f, 4, Bracket);
                mesh.AddTube(new Vector3(x, arm, wall), new Vector3(x, arm, Depth / 2f - 0.04f), 0.008f, 0.008f, 4, Bracket);
                mesh.AddTube(new Vector3(x, 0.015f, wall), new Vector3(x, arm, 0.04f), 0.006f, 0.006f, 4, Bracket);
            }

            // The row, a little gap between each so their sides don't fight
            var left = -Width / 2f + 0.03f;
            foreach (var book in Row)
            {
                var slot = BookBase + 3 * book.Colour;
                var x = left + book.Thickness / 2f;
                mesh.AddBox(slot, new Vector3(x, ShelfTop, front - book.Depth / 2f), book.Depth, book.Thickness, book.Height);
                if (book.Banded)
                    foreach (var y in new[] { 0.02f, book.Height - 0.03f })
                    {
                        var band = new[]
                        {
                            new Vector3(left, ShelfTop + y, front + 0.001f), new Vector3(left + book.Thickness, ShelfTop + y, front + 0.001f),
                            new Vector3(left + book.Thickness, ShelfTop + y + 0.008f, front + 0.001f), new Vector3(left, ShelfTop + y + 0.008f, front + 0.001f),
                        };
                        mesh.AddPolygon(Gilt, band);
                        mesh.AddLineLoop(band);
                    }
                left += book.Thickness + gap;
            }

            // One leaning back on the row, tipped on its bottom left edge until its top left corner touches the last one
            const float lean = 0.32f, leanThickness = 0.03f, leanHeight = 0.22f, leanDepth = 0.16f;
            var foot = new Vector3(left + leanHeight * MathF.Sin(lean), ShelfTop, front - leanDepth / 2f);
            var tipped = Matrix.CreateRotationZ(lean) * Matrix.CreateTranslation(foot);
            AddBox(mesh, BookBase + 3 * 1, new Vector3(leanThickness / 2f, 0f, 0f), leanDepth, leanThickness, leanHeight, tipped);

            // A pile of three lying flat at the far end, each a little smaller and turned a little from the one under it
            var y0 = ShelfTop;
            var pile = new[] { (0.22f, 0.16f, 0.035f, 2, 0f), (0.20f, 0.15f, 0.030f, 0, 0.12f), (0.17f, 0.12f, 0.025f, 3, -0.08f) };
            foreach (var (width, depth, thickness, colour, turn) in pile)
            {
                var placed = Matrix.CreateRotationY(turn) * Matrix.CreateTranslation(0.27f, y0, front - 0.09f);
                AddBox(mesh, BookBase + 3 * colour, Vector3.Zero, depth, width, thickness, placed);
                y0 += thickness;
            }

            return mesh.Build(device);
        }

        // A box as MeshBuilder.AddBox makes one (on its bottom centre, `length` along Z, `width` along X), sealed, then
        // moved by `transform`: for one that isn't square to the shelf
        private static void AddBox(MeshBuilder mesh, int baseSlot, Vector3 bottomCentre, float length, float width, float height, Matrix transform)
        {
            var (hf, hr, hu) = (Vector3.UnitZ * (length / 2f), Vector3.UnitX * (width / 2f), Vector3.Up * height);
            Vector3 P(Vector3 v) => Vector3.Transform(bottomCentre + v, transform);
            var (a, b, c, d) = (P(-hf - hr), P(hf - hr), P(hf + hr), P(-hf + hr));
            var (e, f, g, h) = (P(-hf - hr + hu), P(hf - hr + hu), P(hf + hr + hu), P(-hf + hr + hu));

            mesh.AddQuad(baseSlot + MeshBuilder.Side, a, b, f, e);
            mesh.AddQuad(baseSlot + MeshBuilder.Side, c, d, h, g);
            mesh.AddQuad(baseSlot + MeshBuilder.Dim, b, c, g, f);
            mesh.AddQuad(baseSlot + MeshBuilder.Dim, d, a, e, h);
            mesh.AddQuad(baseSlot + MeshBuilder.Dim, a, b, c, d);
            mesh.AddQuad(baseSlot + MeshBuilder.Top, e, f, g, h);
            mesh.AddLineLoop(a, b, c, d);
            mesh.AddLineLoop(e, f, g, h);
            mesh.AddLine(a, e); mesh.AddLine(b, f); mesh.AddLine(c, g); mesh.AddLine(d, h);
        }
    }
}
