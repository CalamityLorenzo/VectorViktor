using MeshCore.Library;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using System.Linq;

namespace MeshProps
{
    // A pedestal wash basin for washing your face: a white ceramic basin, flat on top, with an oval bowl sunk into it
    // towards the front and a plug hole at the bottom, standing on a tapering eight-sided pedestal with a flared foot; and
    // two chrome pillar taps behind the bowl, hot on the left and cold on the right, their spouts reaching over it.
    // 0.56 wide, 0.42 deep, 0.86 to the rim. Its back (-Z) goes against the wall; centred on X and Z, on y = 0.
    public static class WashBasinMesh
    {
        // The ceramic has three shades (see MeshBuilder), the inside of the bowl two more, its steep sides darker than
        // its flatter bottom; then the taps' chrome, their hot and cold caps, and the plug hole.
        public const int CeramicBase = 0, Bowl = 3, BowlSteep = 4, Chrome = 5, Hot = 6, Cold = 7, Plug = 8;
        public const int PaletteSize = 9;

        public const float RimHeight = 0.86f;

        public static Color[] Palette(Color ceramic, Color chrome)
        {
            var palette = new Color[PaletteSize];
            MeshBuilder.SetBoxShades(palette, CeramicBase, ceramic);
            palette[Bowl] = Color.Lerp(ceramic, new Color(120, 130, 140), 0.2f);
            palette[BowlSteep] = Color.Lerp(ceramic, new Color(120, 130, 140), 0.4f);
            palette[Chrome] = chrome;
            palette[Hot] = new Color(210, 40, 40);
            palette[Cold] = new Color(40, 80, 200);
            palette[Plug] = new Color(50, 50, 55);
            return palette;
        }

        public static MeshData Build(GraphicsDevice device)
        {
            const float halfWidth = 0.28f, halfDepth = 0.21f, basinHeight = 0.16f;
            const float bowlZ = 0.03f, bowlX = 0.19f, bowlZRadius = 0.13f, bowlDepth = 0.12f;
            const float top = RimHeight, underside = RimHeight - basinHeight;
            var mesh = new MeshBuilder();

            // The basin's sides and underside: front and back in the dim shade, the ends in the side shade
            var (a, b, c, d) = (new Vector3(-halfWidth, underside, -halfDepth), new Vector3(-halfWidth, underside, halfDepth),
                                new Vector3(halfWidth, underside, halfDepth), new Vector3(halfWidth, underside, -halfDepth));
            var lift = Vector3.Up * basinHeight;
            mesh.AddQuad(CeramicBase + MeshBuilder.Side, a, b, b + lift, a + lift);
            mesh.AddQuad(CeramicBase + MeshBuilder.Side, c, d, d + lift, c + lift);
            mesh.AddQuad(CeramicBase + MeshBuilder.Dim, b, c, c + lift, b + lift);
            mesh.AddQuad(CeramicBase + MeshBuilder.Dim, d, a, a + lift, d + lift);
            mesh.AddQuad(CeramicBase + MeshBuilder.Dim, a, b, c, d);
            mesh.AddLineLoop(a, b, c, d);
            mesh.AddLineLoop(a + lift, b + lift, c + lift, d + lift);
            mesh.AddLine(a, a + lift); mesh.AddLine(b, b + lift); mesh.AddLine(c, c + lift); mesh.AddLine(d, d + lift);

            // Round the bowl's centre, a ring of angles that takes in the top's four corners, so each piece of the top
            // between the bowl's rim and the top's edge is a four-sided piece on one side of the rectangle
            var middle = new Vector3(0f, top, bowlZ);
            var angles = Enumerable.Range(0, 20).Select(k => k * MathHelper.TwoPi / 20).ToList();
            foreach (var (x, z) in new[] { (-halfWidth, -halfDepth), (halfWidth, -halfDepth), (halfWidth, halfDepth), (-halfWidth, halfDepth) })
                angles.Add((MathF.Atan2(z - bowlZ, x) + MathHelper.TwoPi) % MathHelper.TwoPi);
            angles.Sort();

            // Where a line out from the bowl's centre at an angle meets the top's edge
            Vector3 Edge(float angle)
            {
                var (cos, sin) = (MathF.Cos(angle), MathF.Sin(angle));
                var reach = MathF.Abs(cos) > 1e-5f ? halfWidth / MathF.Abs(cos) : float.MaxValue;
                if (MathF.Abs(sin) > 1e-5f)
                    reach = MathF.Min(reach, (sin > 0f ? halfDepth - bowlZ : halfDepth + bowlZ) / MathF.Abs(sin));
                return middle + reach * new Vector3(cos, 0f, sin);
            }
            // A ring round the bowl, `scale` of its rim's size, `down` below the top
            Vector3[] Ring(float scale, float down) =>
                angles.Select(t => middle + new Vector3(scale * bowlX * MathF.Cos(t), -down, scale * bowlZRadius * MathF.Sin(t))).ToArray();

            var rim = Ring(1f, 0f);
            var outer = angles.Select(Edge).ToArray();
            var n = angles.Count;
            for (var k = 0; k < n; k++)
                mesh.AddQuad(CeramicBase + MeshBuilder.Top, rim[k], rim[(k + 1) % n], outer[(k + 1) % n], outer[k]);
            mesh.AddLineLoop(rim);

            // The bowl: steep near the rim, flattening to its bottom, and the plug hole in the middle of that
            var rings = new List<Vector3[]> { rim, Ring(0.85f, bowlDepth * 0.45f), Ring(0.6f, bowlDepth * 0.85f), Ring(0.35f, bowlDepth) };
            for (var r = 0; r < rings.Count - 1; r++)
                for (var k = 0; k < n; k++)
                    mesh.AddQuad(r == 0 ? BowlSteep : Bowl, rings[r][k], rings[r][(k + 1) % n], rings[r + 1][(k + 1) % n], rings[r + 1][k]);
            mesh.AddPolygon(Bowl, rings[^1]);
            mesh.AddLineLoop(rings[^1]);
            var plug = new Vector3[10];
            for (var k = 0; k < plug.Length; k++)
            {
                var angle = k * MathHelper.TwoPi / plug.Length;
                plug[k] = middle + new Vector3(0.02f * MathF.Cos(angle), -bowlDepth + 0.001f, 0.02f * MathF.Sin(angle));
            }
            mesh.AddPolygon(Plug, plug);
            mesh.AddLineLoop(plug);

            // The pedestal: a flared foot, and a column tapering up from it to the basin's underside, set back towards the wall
            const float pedestalZ = -0.06f, foot = 0.04f;
            mesh.AddFrustum(new Vector3(0f, 0f, pedestalZ), 0.13f, 0.11f, foot, 8, CeramicBase + MeshBuilder.Side, topSlot: CeramicBase + MeshBuilder.Top);
            mesh.AddFrustum(new Vector3(0f, foot, pedestalZ), 0.10f, 0.075f, underside - foot, 8, CeramicBase + MeshBuilder.Side, verticalEdges: true);

            // The taps, behind the bowl: a pillar, a head with a coloured cap, and a spout reaching forward and down over the bowl
            const float tapZ = -0.16f;
            foreach (var (x, cap) in new[] { (-0.12f, Hot), (0.12f, Cold) })
            {
                var foot0 = new Vector3(x, top, tapZ);
                mesh.AddFrustum(foot0, 0.018f, 0.016f, 0.05f, 8, Chrome);
                mesh.AddFrustum(foot0 + Vector3.Up * 0.05f, 0.024f, 0.02f, 0.018f, 8, Chrome, topSlot: cap);
                var spout = foot0 + new Vector3(0f, 0.035f, 0.012f);
                var reach = new Vector3(x, spout.Y, -0.06f);
                mesh.AddTube(spout, reach, 0.007f, 0.007f, 6, Chrome);
                mesh.AddTube(reach, reach + new Vector3(0f, -0.018f, 0.008f), 0.007f, 0.006f, 6, Chrome, ringEdges: true);
            }

            return mesh.Build(device);
        }
    }
}
