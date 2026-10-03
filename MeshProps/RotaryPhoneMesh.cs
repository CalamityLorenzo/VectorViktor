using MeshCore.Library;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MeshProps
{
    // A classic bakelite rotary telephone: a body narrowing to its top on a thin plinth, the dial on its sloping front -
    // a finger wheel with ten holes, a label in the middle, the finger stop at the bottom right - and the handset lying
    // across its cradle on top, its coiled cord looping down to the body's side. 0.26 wide with the handset, 0.24 deep,
    // 0.18 high. The dial faces +Z, centred on X and Z, standing on y = 0.
    public static class RotaryPhoneMesh
    {
        // The bakelite has three shades (see MeshBuilder); the finger wheel one, the label (and what shows through the holes) one.
        public const int BodyBase = 0, Dial = 3, Label = 4;
        public const int PaletteSize = 5;

        public static Color[] Palette(Color body, Color dial, Color label)
        {
            var palette = new Color[PaletteSize];
            MeshBuilder.SetBoxShades(palette, BodyBase, body);
            palette[Dial] = dial;
            palette[Label] = label;
            return palette;
        }

        private const float Plinth = 0.012f, Top = 0.105f;

        public static MeshData Build(GraphicsDevice device)
        {
            var mesh = new MeshBuilder();

            // The plinth, then the body on it: wide at the bottom, narrowing to its top, its front sloping back the most
            mesh.AddBox(BodyBase, Vector3.Zero, 0.24f, 0.24f, Plinth, sealBottom: true);
            var (a, b, c, d) = (new Vector3(-0.11f, Plinth, -0.11f), new Vector3(-0.11f, Plinth, 0.11f), new Vector3(0.11f, Plinth, 0.11f), new Vector3(0.11f, Plinth, -0.11f));
            var (e, f, g, h) = (new Vector3(-0.08f, Top, -0.09f), new Vector3(-0.08f, Top, 0.03f), new Vector3(0.08f, Top, 0.03f), new Vector3(0.08f, Top, -0.09f));
            mesh.AddQuad(BodyBase + MeshBuilder.Side, a, b, f, e);
            mesh.AddQuad(BodyBase + MeshBuilder.Side, c, d, h, g);
            mesh.AddQuad(BodyBase + MeshBuilder.Dim, b, c, g, f);
            mesh.AddQuad(BodyBase + MeshBuilder.Dim, d, a, e, h);
            mesh.AddQuad(BodyBase + MeshBuilder.Top, e, f, g, h);
            mesh.AddLineLoop(a, b, c, d);
            mesh.AddLineLoop(e, f, g, h);
            mesh.AddLine(a, e); mesh.AddLine(b, f); mesh.AddLine(c, g); mesh.AddLine(d, h);

            // The dial, on the sloping front: `across` along it, `up` up its slope, `out` out of it
            var across = Vector3.UnitX;
            var up = Vector3.Normalize(f - b);
            var outward = Vector3.Cross(across, up);
            var centre = new Vector3(0f, Plinth, 0.11f) + up * 0.058f;
            Vector3[] Circle(Vector3 middle, float radius, int sides, float proud)
            {
                var ring = new Vector3[sides];
                for (var k = 0; k < sides; k++)
                {
                    var angle = k * MathHelper.TwoPi / sides;
                    ring[k] = middle + radius * (MathF.Cos(angle) * across + MathF.Sin(angle) * up) + outward * proud;
                }
                return ring;
            }
            void Disc(int slot, Vector3 middle, float radius, int sides, float proud)
            {
                var ring = Circle(middle, radius, sides, proud);
                mesh.AddPolygon(slot, ring);
                mesh.AddLineLoop(ring);
            }
            Disc(Dial, centre, 0.048f, 20, 0.002f);
            Disc(Label, centre, 0.017f, 12, 0.0035f);
            for (var hole = 0; hole < 10; hole++)
            {
                var angle = MathHelper.ToRadians(40f + hole * 28f);   // round from the top right, leaving the bottom right clear
                Disc(Label, centre + 0.034f * (MathF.Cos(angle) * across + MathF.Sin(angle) * up), 0.0075f, 8, 0.0035f);
            }
            var stop = MathHelper.ToRadians(-30f);
            var stopAlong = MathF.Cos(stop) * across + MathF.Sin(stop) * up;
            mesh.AddTube(centre + stopAlong * 0.040f + outward * 0.002f, centre + stopAlong * 0.054f + outward * 0.002f, 0.003f, 0.003f, 4, Dial);

            // The cradle: a prong up from each side of the top
            const float cradleZ = -0.03f, handsetY = 0.165f;
            foreach (var x in new[] { -0.055f, 0.055f })
                mesh.AddBox(BodyBase, new Vector3(x, Top, cradleZ), 0.03f, 0.02f, handsetY - 0.012f - Top);

            // The handset: a cup at each end, open side down, joined by a handle bowed up between them
            const float cupX = 0.10f, cupBottom = 0.12f, cupTop = 0.15f;
            foreach (var side in new[] { -1f, 1f })
                mesh.AddFrustum(new Vector3(side * cupX, cupBottom, cradleZ), 0.03f, 0.022f, cupTop - cupBottom, 10,
                    BodyBase + MeshBuilder.Side, BodyBase + MeshBuilder.Dim, BodyBase + MeshBuilder.Top);
            var handle = new[]
            {
                new Vector3(-cupX, cupTop, cradleZ), new Vector3(-0.06f, handsetY, cradleZ),
                new Vector3(0.06f, handsetY, cradleZ), new Vector3(cupX, cupTop, cradleZ),
            };
            for (var i = 0; i < handle.Length - 1; i++)
                mesh.AddTube(handle[i], handle[i + 1], 0.013f, 0.013f, 6, BodyBase + MeshBuilder.Side);

            // The cord, coiled, as lines only: from under the left cup, sagging out to the side onto the table, and back
            // into the body's left side near the front
            var from = new Vector3(-cupX - 0.01f, cupBottom, cradleZ);
            var sag = new Vector3(-0.21f, -0.05f, 0.02f);
            var to = new Vector3(-0.105f, 0.04f, 0.06f);
            Vector3 Along(float t) => (1f - t) * (1f - t) * from + 2f * (1f - t) * t * sag + t * t * to;
            const int turns = 22, perTurn = 6;
            Vector3 Coil(int k)
            {
                var t = k / (float)(turns * perTurn);
                var tangent = Vector3.Normalize(Along(MathF.Min(t + 0.01f, 1f)) - Along(MathF.Max(t - 0.01f, 0f)));
                var u = Vector3.Normalize(Vector3.Cross(tangent, Vector3.UnitZ));
                var v = Vector3.Cross(tangent, u);
                var angle = k * MathHelper.TwoPi / perTurn;
                return Along(t) + 0.007f * (MathF.Cos(angle) * u + MathF.Sin(angle) * v);
            }
            for (var k = 0; k < turns * perTurn; k++)
                mesh.AddLine(Coil(k), Coil(k + 1));

            return mesh.Build(device);
        }
    }
}
