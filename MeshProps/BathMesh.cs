using MeshCore.Library;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Linq;

namespace MeshProps
{
    // A free-standing roll-top bath: a rounded cast-iron tub, widening up to its rolled rim, on four splayed feet; the
    // inside sloping more steeply at the tap end (-X) than at the end you lie back against, the plug on its chain at the
    // tap end, and two chrome pillar taps on the rim there, hot nearest you, their spouts reaching over the water. 1.71
    // long (along X), 0.74 wide, 0.64 to the rim. Its long side faces +Z, centred on X and Z, standing on y = 0.
    //
    // Its outside is a rounded surface, outlined afresh for each view (see OutlineData) rather than with edges of its
    // own: only the rim and the bottom of the inside have fixed edges.
    public static class BathMesh
    {
        // The outside has three shades (see MeshBuilder: its sides, its underside); the enamel inside and over the rim
        // three more, by how much each face turns up (see InsideShade); then the feet, the taps' chrome, their hot and
        // cold caps, and the plug.
        public const int OutsideBase = 0, InsideBase = 3, Foot = 6, Chrome = 7, Hot = 8, Cold = 9, Plug = 10;
        public const int PaletteSize = 11;

        public const float RimHeight = 0.64f;

        public static Color[] Palette(Color outside, Color inside, Color foot, Color chrome)
        {
            var palette = new Color[PaletteSize];
            MeshBuilder.SetBoxShades(palette, OutsideBase, outside);
            palette[InsideBase] = inside;                                                 // flat: the rim, the floor
            palette[InsideBase + 1] = Color.Lerp(inside, new Color(120, 130, 140), 0.2f);  // sloping
            palette[InsideBase + 2] = Color.Lerp(inside, new Color(120, 130, 140), 0.4f);  // steep
            palette[Foot] = foot;
            palette[Chrome] = chrome;
            palette[Hot] = new Color(210, 40, 40);
            palette[Cold] = new Color(40, 80, 200);
            palette[Plug] = new Color(50, 50, 55);
            return palette;
        }

        private const int Around = 32;

        // A ring round the tub, `y` up: a rounded rectangle (a superellipse) `halfLength` along X and `halfWidth` along Z,
        // centred `centreX` along. Every ring has its points at the same angles, so neighbouring rings join up in quads.
        private static Vector3[] Ring(float halfLength, float halfWidth, float y, float centreX = 0f) =>
            Enumerable.Range(0, Around).Select(k =>
            {
                var angle = k * MathHelper.TwoPi / Around;
                var (cos, sin) = (MathF.Cos(angle), MathF.Sin(angle));
                return new Vector3(centreX + halfLength * MathF.Sign(cos) * MathF.Sqrt(MathF.Abs(cos)), y, halfWidth * MathF.Sign(sin) * MathF.Sqrt(MathF.Abs(sin)));
            }).ToArray();

        // Which of the inside's three shades a face is, with a, b and c three of its corners: lit as from above, the
        // flatter it is the lighter, so the hollow shows its depth
        private static int InsideShade(Vector3 a, Vector3 b, Vector3 c)
        {
            var up = MathF.Abs(Vector3.Normalize(Vector3.Cross(b - a, c - a)).Y);
            return InsideBase + (up > 0.85f ? 0 : up > 0.5f ? 1 : 2);
        }

        public static MeshData Build(GraphicsDevice device)
        {
            var mesh = new MeshBuilder();
            var centre = new Vector3(0f, 0.4f, 0f);   // inside the tub, for the outline to tell its outside from its inside

            // A band of quads between two rings, in `slot`, or with no slot, in the inside's shade for how it slopes
            void Band(int? slot, Vector3[] lower, Vector3[] upper, bool outlined)
            {
                for (var k = 0; k < Around; k++)
                {
                    var (a, b, c, d) = (lower[k], lower[(k + 1) % Around], upper[(k + 1) % Around], upper[k]);
                    mesh.AddQuad(slot ?? InsideShade(a, b, c), a, b, c, d);
                    if (outlined)
                    {
                        mesh.AddOutlineTri(a, b, c, centre);
                        mesh.AddOutlineTri(a, c, d, centre);
                    }
                }
            }

            // The outside, from its flat bottom bulging out and up to the rim: its profile turns only one way, so it's
            // a convex surface, the quickest kind to outline
            var outside = new[]
            {
                Ring(0.62f, 0.25f, 0.16f), Ring(0.77f, 0.32f, 0.36f), Ring(0.83f, 0.355f, 0.52f),
                Ring(0.855f, 0.37f, 0.62f), Ring(0.855f, 0.37f, RimHeight),
            };
            var bottom = outside[0];
            for (var k = 1; k < Around - 1; k++)
            {
                mesh.AddTri(OutsideBase + MeshBuilder.Dim, bottom[0], bottom[k], bottom[k + 1]);
                mesh.AddOutlineTri(bottom[0], bottom[k], bottom[k + 1], centre);
            }
            for (var r = 0; r < outside.Length - 1; r++)
                Band(OutsideBase + MeshBuilder.Side, outside[r], outside[r + 1], outlined: true);

            // The rim, flat across the top, then the inside down to its floor, the floor further towards the tap end
            var inside = new[]
            {
                Ring(0.79f, 0.31f, RimHeight), Ring(0.78f, 0.30f, 0.62f), Ring(0.72f, 0.26f, 0.45f, -0.02f),
                Ring(0.60f, 0.19f, 0.25f, -0.05f), Ring(0.50f, 0.15f, 0.22f, -0.08f),
            };
            Band(null, inside[0], outside[^1], outlined: false);
            for (var r = 0; r < inside.Length - 1; r++)
                Band(null, inside[r + 1], inside[r], outlined: false);
            mesh.AddPolygon(InsideBase, inside[^1]);
            mesh.AddLineLoop(outside[^1]);
            mesh.AddLineLoop(inside[0]);
            mesh.AddLineLoop(inside[^1]);

            // The feet: a leg splayed out from under each corner, on a round pad
            foreach (var sx in new[] { -1f, 1f })
                foreach (var sz in new[] { -1f, 1f })
                {
                    var pad = new Vector3(sx * 0.60f, 0f, sz * 0.22f);
                    mesh.AddTube(new Vector3(sx * 0.52f, 0.17f, sz * 0.16f), pad + Vector3.Up * 0.03f, 0.035f, 0.026f, 6, Foot);
                    mesh.AddFrustum(pad, 0.045f, 0.035f, 0.035f, 6, Foot, topSlot: Foot, verticalEdges: true);
                }

            // The taps on the rim at the tap end: a pillar, a head with a coloured cap, a spout reaching in over the water
            const float tapX = -0.822f;
            foreach (var (z, cap) in new[] { (0.07f, Hot), (-0.07f, Cold) })
            {
                var foot = new Vector3(tapX, RimHeight, z);
                mesh.AddFrustum(foot, 0.016f, 0.014f, 0.07f, 8, Chrome);
                mesh.AddFrustum(foot + Vector3.Up * 0.07f, 0.022f, 0.018f, 0.015f, 8, Chrome, topSlot: cap);
                var spout = foot + new Vector3(0.01f, 0.05f, 0f);
                var reach = new Vector3(-0.74f, spout.Y, z);
                mesh.AddTube(spout, reach, 0.008f, 0.008f, 6, Chrome);
                mesh.AddTube(reach, reach + new Vector3(0.01f, -0.02f, 0f), 0.008f, 0.007f, 6, Chrome, ringEdges: true);
            }

            // The plug on the floor at the tap end, and its chain, as lines, lying up the end wall to hook over the rim
            var plugAt = new Vector3(-0.48f, 0.221f, 0f);
            var plug = Enumerable.Range(0, 10).Select(k => plugAt + 0.022f * new Vector3(MathF.Cos(k * MathHelper.TwoPi / 10), 0f, MathF.Sin(k * MathHelper.TwoPi / 10))).ToArray();
            mesh.AddPolygon(Plug, plug);
            mesh.AddLineLoop(plug);
            var chain = new[]
            {
                plugAt + new Vector3(-0.022f, 0.001f, 0f), new Vector3(-0.59f, 0.228f, -0.02f), new Vector3(-0.64f, 0.26f, -0.03f),
                new Vector3(-0.72f, 0.45f, -0.04f), new Vector3(-0.77f, 0.62f, -0.05f), new Vector3(-0.79f, RimHeight + 0.003f, -0.05f),
            };
            for (var i = 0; i < chain.Length - 1; i++)
                mesh.AddLine(chain[i], chain[i + 1]);

            return mesh.Build(device);
        }
    }
}
