using MeshCore.Library;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace MeshRawData
{
    // A roadside billboard advertising the Commodore 64: a board Width x Height with its bottom edge
    // Clearance up, on two posts behind it, facing +Z. On its white face, the Commodore logo on the left -
    // the thick C, open to the right, and in its mouth the two flags, blue over red, their ends cut away to
    // a notch - and on the right COMMODORE, with a big 64 under it, in blue block capitals. The origin is
    // on the ground between the posts, which go PostSunk into it, each through a concrete footing standing
    // a little proud of the ground. Everything's gathered into one draw range per colour.
    public static class BillboardMesh
    {
        public const int Post = 0, Board = 3, Face = 6, Blue = 7, Red = 8, Footing = 9;
        public const int PaletteSize = 12;

        public const float Width = 7f, Height = 3.5f, Clearance = 2.4f;
        private const float BoardDepth = 0.2f, Border = 0.15f, Lift = 0.01f;
        public const float PostSize = 0.3f, PostSunk = 0.5f;
        private const float FootingSize = 0.8f, FootingProud = 0.2f;

        public static Color[] Palette(Color post, Color board)
        {
            var palette = new Color[PaletteSize];
            MeshBuilder.SetBoxShades(palette, Post, post);
            MeshBuilder.SetBoxShades(palette, Board, board);
            palette[Face] = new Color(245, 245, 240);
            palette[Blue] = new Color(30, 55, 150);
            palette[Red] = new Color(215, 35, 40);
            MeshBuilder.SetBoxShades(palette, Footing, new Color(165, 160, 150));
            return palette;
        }

        // Block capitals as strokes (x0, y0, x1, y1) in a cell 0.6 wide and 1 high, meeting without overlapping.
        private static readonly Dictionary<char, float[][]> Letters = new Dictionary<char, float[][]>
        {
            ['C'] = new[] { new[] { 0f, 0f, 0.16f, 1f }, new[] { 0.16f, 0.84f, 0.6f, 1f }, new[] { 0.16f, 0f, 0.6f, 0.16f } },
            ['O'] = new[] { new[] { 0f, 0f, 0.16f, 1f }, new[] { 0.44f, 0f, 0.6f, 1f }, new[] { 0.16f, 0.84f, 0.44f, 1f }, new[] { 0.16f, 0f, 0.44f, 0.16f } },
            ['M'] = new[] { new[] { 0f, 0f, 0.16f, 1f }, new[] { 0.44f, 0f, 0.6f, 1f }, new[] { 0.16f, 0.84f, 0.44f, 1f }, new[] { 0.22f, 0.4f, 0.38f, 0.84f } },
            ['D'] = new[] { new[] { 0f, 0f, 0.16f, 1f }, new[] { 0.16f, 0.84f, 0.44f, 1f }, new[] { 0.16f, 0f, 0.44f, 0.16f }, new[] { 0.44f, 0.16f, 0.6f, 0.84f } },
            ['R'] = new[] { new[] { 0f, 0f, 0.16f, 1f }, new[] { 0.16f, 0.84f, 0.6f, 1f }, new[] { 0.44f, 0.58f, 0.6f, 0.84f }, new[] { 0.16f, 0.42f, 0.6f, 0.58f }, new[] { 0.44f, 0f, 0.6f, 0.42f } },
            ['E'] = new[] { new[] { 0f, 0f, 0.16f, 1f }, new[] { 0.16f, 0.84f, 0.6f, 1f }, new[] { 0.16f, 0.42f, 0.5f, 0.58f }, new[] { 0.16f, 0f, 0.6f, 0.16f } },
            ['6'] = new[] { new[] { 0f, 0f, 0.16f, 1f }, new[] { 0.16f, 0.84f, 0.6f, 1f }, new[] { 0.16f, 0.42f, 0.6f, 0.58f }, new[] { 0.16f, 0f, 0.6f, 0.16f }, new[] { 0.44f, 0.16f, 0.6f, 0.42f } },
            ['4'] = new[] { new[] { 0f, 0.58f, 0.16f, 1f }, new[] { 0f, 0.42f, 0.44f, 0.58f }, new[] { 0.44f, 0f, 0.6f, 1f } },
        };

        // Where the posts stand, in its own X and Z: behind the board, a little in from its ends.
        public static readonly float[] PostsAt = { -Width * 0.3f, Width * 0.3f };
        public const float PostZ = -BoardDepth / 2f - PostSize / 2f;

        public static MeshData Build(GraphicsDevice device)
        {
            var mesh = new MeshBuilder();
            var faces = new List<Vector3[]>[PaletteSize];
            for (var slot = 0; slot < PaletteSize; slot++)
                faces[slot] = new List<Vector3[]>();

            // An upright box on its bottom centre: `depth` along Z, `width` along X, in a part's three shades
            void Box(int slot, Vector3 at, float width, float depth, float height)
            {
                float hx = width / 2f, hz = depth / 2f;
                Vector3 P(float dx, float dy, float dz) => at + new Vector3(dx * hx, dy * height, dz * hz);
                Vector3 a = P(-1, 0, -1), b = P(1, 0, -1), c = P(1, 0, 1), d = P(-1, 0, 1);
                Vector3 e = P(-1, 1, -1), f = P(1, 1, -1), g = P(1, 1, 1), h = P(-1, 1, 1);
                faces[slot + MeshBuilder.Side].Add(new[] { b, c, g, f });
                faces[slot + MeshBuilder.Side].Add(new[] { d, a, e, h });
                faces[slot + MeshBuilder.Dim].Add(new[] { a, b, f, e });
                faces[slot + MeshBuilder.Dim].Add(new[] { c, d, h, g });
                faces[slot + MeshBuilder.Dim].Add(new[] { a, d, c, b });
                faces[slot + MeshBuilder.Top].Add(new[] { e, f, g, h });
                mesh.AddLineLoop(a, b, c, d);
                mesh.AddLineLoop(e, f, g, h);
                mesh.AddLine(a, e); mesh.AddLine(b, f); mesh.AddLine(c, g); mesh.AddLine(d, h);
            }

            // A flat shape on the board's face, outlined
            var front = BoardDepth / 2f;
            Vector3 OnFace(float x, float y, float lift) => new Vector3(x, Clearance + y, front + lift);
            void Paint(int slot, float lift, params Vector2[] points)
            {
                var polygon = Array.ConvertAll(points, p => OnFace(p.X, p.Y, lift));
                faces[slot].Add(polygon);
                mesh.AddLineLoop(polygon);
            }

            // Posts behind the board, from well down in the ground up to near its top, each in its footing;
            // the board; its white face
            foreach (var x in PostsAt)
            {
                var foot = new Vector3(x, 0f, PostZ);
                Box(Post, foot - Vector3.Up * PostSunk, PostSize, PostSize, PostSunk + Clearance + Height - 0.3f);
                Box(Footing, foot - Vector3.Up * PostSunk, FootingSize, FootingSize, PostSunk + FootingProud);
            }
            Box(Board, new Vector3(0f, Clearance, 0f), Width, BoardDepth, Height);
            var hw = Width / 2f - Border;
            Paint(Face, Lift, new Vector2(-hw, Border), new Vector2(hw, Border), new Vector2(hw, Height - Border), new Vector2(-hw, Height - Border));

            // The logo: the C, a thick ring open 40 degrees either side of its right-hand side...
            var centre = new Vector2(-Width / 2f + 1.6f, Height / 2f);
            const float outer = 1.15f, inner = 0.62f, mouth = 40f;
            const int segments = 28;
            var from = MathHelper.ToRadians(mouth);
            var to = MathHelper.ToRadians(360f - mouth);
            Vector2 Round(float radius, float angle) => centre + radius * new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            for (var k = 0; k < segments; k++)
            {
                var a0 = MathHelper.Lerp(from, to, k / (float)segments);
                var a1 = MathHelper.Lerp(from, to, (k + 1) / (float)segments);
                var quad = new[] { Round(inner, a0), Round(outer, a0), Round(outer, a1), Round(inner, a1) };
                faces[Blue].Add(Array.ConvertAll(quad, p => OnFace(p.X, p.Y, 2f * Lift)));
                mesh.AddLine(OnFace(quad[1].X, quad[1].Y, 2f * Lift), OnFace(quad[2].X, quad[2].Y, 2f * Lift));
                mesh.AddLine(OnFace(quad[0].X, quad[0].Y, 2f * Lift), OnFace(quad[3].X, quad[3].Y, 2f * Lift));
            }
            foreach (var angle in new[] { from, to })
                mesh.AddLine(OnFace(Round(inner, angle).X, Round(inner, angle).Y, 2f * Lift), OnFace(Round(outer, angle).X, Round(outer, angle).Y, 2f * Lift));

            // ...and the flags in its mouth, from its middle out past its rim, their right ends cut to a notch
            const float flag = 0.36f, gap = 0.05f, reach = 1.3f, notch = 0.32f;
            Paint(Blue, 2f * Lift,
                centre + new Vector2(0f, gap), centre + new Vector2(reach - notch, gap),
                centre + new Vector2(reach, gap + flag), centre + new Vector2(0f, gap + flag));
            Paint(Red, 2f * Lift,
                centre + new Vector2(0f, -gap - flag), centre + new Vector2(reach, -gap - flag),
                centre + new Vector2(reach - notch, -gap), centre + new Vector2(0f, -gap));

            // COMMODORE, and a big 64 under it
            void Write(string text, float left, float bottom, float size)
            {
                for (var i = 0; i < text.Length; i++)
                    foreach (var s in Letters[text[i]])
                    {
                        var x = left + i * 0.8f * size;
                        Paint(Blue, 2f * Lift,
                            new Vector2(x + s[0] * size, bottom + s[1] * size), new Vector2(x + s[2] * size, bottom + s[1] * size),
                            new Vector2(x + s[2] * size, bottom + s[3] * size), new Vector2(x + s[0] * size, bottom + s[3] * size));
                    }
            }
            Write("COMMODORE", -0.3f, 2.25f, 0.5f);
            Write("64", 0.9f, 0.45f, 1.4f);

            for (var slot = 0; slot < PaletteSize; slot++)
            {
                var triangles = 0;
                foreach (var polygon in faces[slot])
                    triangles += polygon.Length - 2;
                if (triangles == 0)
                    continue;
                mesh.AddSolidRange(triangles, slot);
                foreach (var polygon in faces[slot])
                    mesh.AddPolygon(polygon);
            }
            return mesh.Build(device);
        }
    }
}
