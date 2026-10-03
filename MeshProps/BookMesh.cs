using MeshCore.Library;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace MeshProps
{
    // A hardback book: two boards and a spine round the block of pages, which stands in from the boards a little, with a
    // couple of lines along each of the pages' edges and two gilt bands round the spine. Its front cover, the board right
    // of the spine, CoverWidth x Height, carries a BookCover's art as a billboard carries its design. Width x Height x
    // Thickness, the spine down the cover's left side.
    //
    // Lying on its back on a desk, its cover faces up, the cover's top edge towards -Z; standing up, it stands on its
    // bottom edge with its cover facing +Z. Either way it's centred on X and Z, on y = 0.
    public static class BookMesh
    {
        // The boards and spine have three shades (see MeshBuilder), and the pages three; Ink and Gilt are for the art.
        public const int CoverBase = 0, PagesBase = 3, Ink = 6, Gilt = 7;
        public const int PaletteSize = 8;

        public const float Width = 0.16f, Height = 0.24f, Thickness = 0.035f;
        private const float Board = 0.004f, Spine = 0.005f, Inset = 0.004f, Lift = 0.0015f;
        public const float CoverWidth = Width - Spine;

        public static Color[] Palette(Color cover, Color ink, Color gilt)
        {
            var palette = new Color[PaletteSize];
            MeshBuilder.SetBoxShades(palette, CoverBase, cover);
            MeshBuilder.SetBoxShades(palette, PagesBase, new Color(240, 232, 205));
            palette[Ink] = ink;
            palette[Gilt] = gilt;
            return palette;
        }

        public static MeshSource Source(BookCover cover, bool standing, Color colour, Color ink, Color gilt) =>
            new MeshSource($"book:{cover.Key}{(standing ? ":standing" : "")}", d => Build(d, cover, standing), Palette(colour, ink, gilt));

        public static MeshData Build(GraphicsDevice device, BookCover cover, bool standing = false)
        {
            var mesh = new MeshBuilder();

            // The book in its own terms: u across the cover from its middle (the spine at -Width/2), v up the cover from its
            // bottom edge, w through it from the back cover (0) to the front (Thickness); and where that is in the world
            Vector3 At(float u, float v, float w) => standing
                ? new Vector3(u, v, w - Thickness / 2f)
                : new Vector3(u, w, Height / 2f - v);

            // A box between those bounds, in a part's three shades, sealed: a book can be seen from any side
            void Box(int slot, float u0, float u1, float v0, float v1, float w0, float w1)
            {
                var (a, b) = (At(u0, v0, w0), At(u1, v1, w1));
                var (min, max) = (Vector3.Min(a, b), Vector3.Max(a, b));
                mesh.AddBox(slot, new Vector3((min.X + max.X) / 2f, min.Y, (min.Z + max.Z) / 2f), max.Z - min.Z, max.X - min.X, max.Y - min.Y, sealBottom: true);
            }

            const float left = -Width / 2f, right = Width / 2f, hinge = left + Spine;
            Box(CoverBase, left, hinge, 0f, Height, 0f, Thickness);                        // spine
            Box(CoverBase, hinge, right, 0f, Height, 0f, Board);                           // back board
            Box(CoverBase, hinge, right, 0f, Height, Thickness - Board, Thickness);        // front board
            Box(PagesBase, hinge, right - Inset, Inset, Height - Inset, Board, Thickness - Board);

            // Lines along the pages' three open edges, a third and two thirds of the way through
            foreach (var t in new[] { 1f / 3f, 2f / 3f })
            {
                var w = MathHelper.Lerp(Board, Thickness - Board, t);
                mesh.AddLine(At(right - Inset, Inset, w), At(right - Inset, Height - Inset, w));
                mesh.AddLine(At(hinge, Height - Inset, w), At(right - Inset, Height - Inset, w));
                mesh.AddLine(At(hinge, Inset, w), At(right - Inset, Inset, w));
            }

            // Gilt bands round the outside of the spine, near its top and bottom
            foreach (var v in new[] { 0.025f, Height - 0.035f })
            {
                var band = new[] { At(left - Lift, v, 0f), At(left - Lift, v, Thickness), At(left - Lift, v + 0.01f, Thickness), At(left - Lift, v + 0.01f, 0f) };
                mesh.AddPolygon(Gilt, band);
                mesh.AddLineLoop(band);
            }

            // The art on the front board, from its middle, a touch off it
            var origin = At((hinge + right) / 2f, 0f, Thickness) + (standing ? Vector3.UnitZ : Vector3.UnitY) * Lift;
            cover.Paint(new PaintedFace(mesh, origin, Vector3.UnitX, standing ? Vector3.UnitY : -Vector3.UnitZ, Lift));
            return mesh.Build(device);
        }
    }

    // What's on a book's front cover: a Key to tell its mesh from the others', and the art it paints there (X across from
    // the cover's middle, Y up from its bottom edge, BookMesh.CoverWidth x BookMesh.Height, in BookMesh.Ink and BookMesh.Gilt).
    // Make another with Titled, or with art of its own, and stand it anywhere with BookMesh.Source.
    public sealed record BookCover(string Key, Action<PaintedFace> Paint)
    {
        // Just the cloth
        public static readonly BookCover Plain = new BookCover("plain", face => { });

        private const float Margin = 0.012f, Border = 0.003f;
        private static void GiltBorder(PaintedFace face) =>
            face.Frame(BookMesh.Gilt, -BookMesh.CoverWidth / 2f + Margin, Margin, BookMesh.CoverWidth / 2f - Margin, BookMesh.Height - Margin, Border);

        // A gilt border, and the title in it a line at a time, centred, with a diamond under it. The letters are 0.02
        // high, or smaller if that's what it takes to get the longest line inside the border.
        public static BookCover Titled(params string[] lines) => new BookCover("titled:" + string.Join("/", lines), face =>
        {
            const float spacing = 0.6f;   // between lines, for each unit of letter height
            var room = BookMesh.CoverWidth - 2f * (Margin + Border + 0.006f);
            var size = 0.02f;
            foreach (var line in lines)
                if (line.Length > 0)
                    size = MathF.Min(size, room / PaintedFace.WidthOf(line, 1f));

            GiltBorder(face);
            var bottom = BookMesh.Height * 0.72f;
            foreach (var line in lines)
            {
                face.WriteCentred(line, 0f, bottom, size, BookMesh.Ink);
                bottom -= size * (1f + spacing);
            }
            var diamond = bottom - 0.015f;
            face.Shape(BookMesh.Gilt,
                new Vector2(0f, diamond - 0.012f), new Vector2(0.009f, diamond),
                new Vector2(0f, diamond + 0.012f), new Vector2(-0.009f, diamond));
        });

        // The droid's own manual: ROBOT MANUAL over a robot's head with an aerial, in a gilt border
        public static readonly BookCover RobotManual = new BookCover("robot-manual", face =>
        {
            const float size = 0.018f;
            GiltBorder(face);
            face.WriteCentred("ROBOT", 0f, 0.185f, size, BookMesh.Ink);
            face.WriteCentred("MANUAL", 0f, 0.155f, size, BookMesh.Ink);

            // The head: a square on a neck, an aerial with a ball on top; and painted over it, two square eyes and a mouth
            static void Rect(PaintedFace on, int slot, float x0, float y0, float x1, float y1) =>
                on.Shape(slot, new Vector2(x0, y0), new Vector2(x1, y0), new Vector2(x1, y1), new Vector2(x0, y1));
            Rect(face, BookMesh.Ink, -0.006f, 0.035f, 0.006f, 0.045f);
            Rect(face, BookMesh.Ink, -0.03f, 0.045f, 0.03f, 0.105f);
            Rect(face, BookMesh.Ink, -0.0015f, 0.105f, 0.0015f, 0.125f);
            Rect(face, BookMesh.Ink, -0.005f, 0.125f, 0.005f, 0.135f);
            var over = face.Over();
            Rect(over, BookMesh.Gilt, -0.02f, 0.078f, -0.008f, 0.09f);
            Rect(over, BookMesh.Gilt, 0.008f, 0.078f, 0.02f, 0.09f);
            Rect(over, BookMesh.Gilt, -0.015f, 0.056f, 0.015f, 0.062f);
        });
    }
}
