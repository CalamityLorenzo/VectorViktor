using MeshCore.Library;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace MeshRawData
{
    // A white picket fence along a run of points (world X, Z): upright pickets PicketSpacing apart, two
    // rails behind them, and a post at every point. Each picket stands on the ground where it is
    // (`groundAt`), sunk a little into it, so the fence follows the lie of the land. The run's legs must
    // each go straight along X or along Z. The mesh is in world coordinates, and however many pickets
    // there are, it's three draw ranges: every box's faces are gathered by shade (see MeshBuilder).
    public static class FenceMesh
    {
        public const int Paint = 0;
        public const int PaletteSize = 3;

        public const float Height = 0.9f;   // low enough to jump, at a run
        private const float PicketWidth = 0.12f, PicketThickness = 0.025f, PicketSpacing = 0.22f;   // a 10 cm gap between pickets
        private const float RailHeight = 0.07f, RailThickness = 0.04f, PostSize = 0.1f, Sunk = 0.1f;

        public static Color[] Palette(Color paint)
        {
            var palette = new Color[PaletteSize];
            MeshBuilder.SetBoxShades(palette, Paint, paint);
            return palette;
        }

        public static MeshData Build(GraphicsDevice device, Func<float, float, float> groundAt, IReadOnlyList<Vector2> run)
        {
            var mesh = new MeshBuilder();
            var faces = new[] { new List<Vector3[]>(), new List<Vector3[]>(), new List<Vector3[]>() };   // side, end, top

            // A box standing on (x, y, z), `along` long in the direction of its leg (X or Z), `across` thick
            void Box(bool alongX, float x, float y, float z, float along, float across, float height)
            {
                var hx = (alongX ? along : across) / 2f;
                var hz = (alongX ? across : along) / 2f;
                Vector3 P(float dx, float dy, float dz) => new Vector3(x + dx * hx, y + dy * height, z + dz * hz);
                Vector3 a = P(-1, 0, -1), b = P(1, 0, -1), c = P(1, 0, 1), d = P(-1, 0, 1);
                Vector3 e = P(-1, 1, -1), f = P(1, 1, -1), g = P(1, 1, 1), h = P(-1, 1, 1);
                // The broad faces (across the leg) get the side shade, the narrow ends the dim one
                var (broad, narrow) = alongX ? (new[] { new[] { a, b, f, e }, new[] { c, d, h, g } }, new[] { new[] { b, c, g, f }, new[] { d, a, e, h } })
                                             : (new[] { new[] { b, c, g, f }, new[] { d, a, e, h } }, new[] { new[] { a, b, f, e }, new[] { c, d, h, g } });
                faces[MeshBuilder.Side].AddRange(broad);
                faces[MeshBuilder.Dim].AddRange(narrow);
                faces[MeshBuilder.Top].Add(new[] { e, f, g, h });
                mesh.AddLineLoop(e, f, g, h);
                mesh.AddLine(a, e); mesh.AddLine(b, f); mesh.AddLine(c, g); mesh.AddLine(d, h);
            }

            for (var k = 0; k < run.Count - 1; k++)
            {
                var a = run[k];
                var b = run[k + 1];
                var alongX = MathF.Abs(b.X - a.X) >= MathF.Abs(b.Y - a.Y);
                var length = Vector2.Distance(a, b);
                if (length < 1e-3f)
                    continue;
                var direction = (b - a) / length;

                var pickets = Math.Max(1, (int)MathF.Round(length / PicketSpacing));
                for (var p = 0; p < pickets; p++)
                {
                    var at = a + direction * ((p + 0.5f) * length / pickets);
                    Box(alongX, at.X, groundAt(at.X, at.Y) - Sunk, at.Y, PicketWidth, PicketThickness, Height + Sunk);
                }

                var middle = (a + b) / 2f;
                var ground = groundAt(middle.X, middle.Y);
                foreach (var y in new[] { 0.25f, 0.7f })
                    Box(alongX, middle.X, ground + y, middle.Y, length, RailThickness, RailHeight);
            }

            foreach (var point in run)
                Box(true, point.X, groundAt(point.X, point.Y) - Sunk, point.Y, PostSize, PostSize, Height + Sunk + 0.08f);

            for (var shade = 0; shade < faces.Length; shade++)
            {
                mesh.AddSolidRange(faces[shade].Count * 2, Paint + shade);
                foreach (var quad in faces[shade])
                    mesh.AddQuad(quad[0], quad[1], quad[2], quad[3]);
            }
            return mesh.Build(device);
        }
    }
}
