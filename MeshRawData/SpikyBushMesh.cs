using MeshCore.Library;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MeshRawData
{
    // The "Spiky Bush": an umbrella-shaped tree about 1.5 times as tall as TreeMesh (1.72 against 1.15).
    // A slender trunk flares at the base and forks into branches that carry a broad, lumpy canopy, wider
    // than the tree is tall, with three lobes on top and an underside that dips at the edges so the
    // branches show beneath it. In wireframe the canopy has no edges of its own: it is drawn as the outline of
    // the whole cluster (see OutlineData), so the blobs it is made from don't show and it reads as clean arcs
    // like the design sketch. The trunk and branches keep their length lines.
    // Built with the trunk's base on y = 0 (not centred), so it sits on the ground rather than tumbles.
    public static class SpikyBushMesh
    {
        // Slots: trunk/branches, then the three leaf shades (see MeshBuilder) starting at LeafBase.
        public const int Trunk = 0, LeafBase = 1;
        public const int PaletteSize = 4;

        public static Color[] Palette(Color trunk, Color leaf)
        {
            var palette = new Color[PaletteSize];
            palette[Trunk] = trunk;
            MeshBuilder.SetBoxShades(palette, LeafBase, leaf);
            return palette;
        }

        // Canopy blobs: centre, radius and how much each is squashed vertically.
        private static readonly (Vector3 centre, float radius, float squash)[] Blobs =
        {
            (new Vector3(0.00f, 1.30f, 0.00f), 0.50f, 0.80f),    // centre, sits high so the underside rises in the middle
            (new Vector3(-0.50f, 1.00f, 0.00f), 0.42f, 0.75f),   // left and right, lower, so the edges droop
            (new Vector3(0.50f, 1.00f, 0.00f), 0.42f, 0.75f),
            (new Vector3(0.00f, 1.10f, 0.38f), 0.36f, 0.75f),    // front and back fill out the depth
            (new Vector3(0.00f, 1.12f, -0.38f), 0.36f, 0.75f),
            (new Vector3(-0.40f, 1.38f, 0.05f), 0.32f, 0.85f),   // two lobes on top
            (new Vector3(0.30f, 1.44f, -0.05f), 0.33f, 0.85f),
        };

        private const int Segments = 8;   // round each blob

        public static MeshData Build(GraphicsDevice device)
        {
            var mesh = new MeshBuilder();

            // ---- Trunk: a flared base, then the trunk up to the fork
            var fork = new Vector3(0f, 0.5f, 0f);
            mesh.AddTube(Vector3.Zero, new Vector3(0f, 0.18f, 0f), 0.13f, 0.06f, 4, Trunk);
            mesh.AddTube(new Vector3(0f, 0.18f, 0f), fork, 0.06f, 0.05f, 4, Trunk);

            // ---- Branches from the fork into the underside of the canopy (their tips end inside it)
            void Branch(Vector3 from, Vector3 to, float fromRadius, float toRadius) =>
                mesh.AddTube(from, to, fromRadius, toRadius, 3, Trunk);

            Branch(fork, new Vector3(-0.10f, 0.95f, 0.00f), 0.035f, 0.022f);    // two inner branches, close together
            Branch(fork, new Vector3(0.12f, 0.93f, 0.02f), 0.035f, 0.022f);
            var leftElbow = new Vector3(-0.22f, 0.66f, 0.02f);                    // two outer branches that curve out
            Branch(fork, leftElbow, 0.04f, 0.03f);
            Branch(leftElbow, new Vector3(-0.42f, 0.80f, 0.00f), 0.03f, 0.02f);
            var rightElbow = new Vector3(0.20f, 0.66f, -0.02f);
            Branch(fork, rightElbow, 0.04f, 0.03f);
            Branch(rightElbow, new Vector3(0.42f, 0.78f, 0.00f), 0.03f, 0.02f);
            Branch(fork, new Vector3(0.03f, 0.88f, 0.30f), 0.03f, 0.02f);        // one to the front and one to the back
            Branch(fork, new Vector3(-0.03f, 0.88f, -0.30f), 0.03f, 0.02f);

            // ---- Canopy: each blob is an 8-sided, 4-band low-poly sphere, squashed
            var faceSets = new List<(Vector3 a, Vector3 b, Vector3 c)>[3];   // by MeshBuilder.Side / Dim / Top
            for (var s = 0; s < 3; s++)
                faceSets[s] = new List<(Vector3, Vector3, Vector3)>();

            foreach (var (centre, radius, squash) in Blobs)
            {
                // Rings at latitude -45, 0 and +45 degrees, plus the two poles
                Vector3 Point(float latitude, int k)
                {
                    var azimuth = k * MathHelper.TwoPi / Segments;
                    return new Vector3(
                        radius * MathF.Cos(latitude) * MathF.Cos(azimuth),
                        radius * MathF.Sin(latitude) * squash,
                        radius * MathF.Cos(latitude) * MathF.Sin(azimuth));
                }
                var lat = new[] { -MathHelper.PiOver4, 0f, MathHelper.PiOver4 };
                var rings = new Vector3[3][];
                for (var r = 0; r < 3; r++)
                {
                    rings[r] = new Vector3[Segments];
                    for (var k = 0; k < Segments; k++)
                        rings[r][k] = Point(lat[r], k);
                }
                var poleTop = new Vector3(0f, radius * squash, 0f);
                var poleBottom = new Vector3(0f, -radius * squash, 0f);

                void Face(Vector3 a, Vector3 b, Vector3 c)
                {
                    var facing = Vector3.Normalize((a + b + c) / 3f).Y;
                    var shade = facing > 0.5f ? MeshBuilder.Top : facing < -0.5f ? MeshBuilder.Dim : MeshBuilder.Side;
                    faceSets[shade].Add((centre + a, centre + b, centre + c));
                    mesh.AddOutlineTri(centre + a, centre + b, centre + c, centre);
                }
                for (var k = 0; k < Segments; k++)
                {
                    var n = (k + 1) % Segments;
                    Face(poleBottom, rings[0][n], rings[0][k]);
                    Face(rings[0][k], rings[0][n], rings[1][n]);
                    Face(rings[0][k], rings[1][n], rings[1][k]);
                    Face(rings[1][k], rings[1][n], rings[2][n]);
                    Face(rings[1][k], rings[2][n], rings[2][k]);
                    Face(poleTop, rings[2][k], rings[2][n]);
                }
            }

            for (var shade = 0; shade < 3; shade++)
            {
                if (faceSets[shade].Count == 0)
                    continue;
                mesh.AddSolidRange(faceSets[shade].Count, LeafBase + shade);
                foreach (var (a, b, c) in faceSets[shade])
                    mesh.AddTri(a, b, c);
            }

            return mesh.Build(device);
        }
    }
}
