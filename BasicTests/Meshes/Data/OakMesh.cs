using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace BasicTests.Meshes
{
    // A broad oak: ONE large, thick, rounded canopy (a lumpy bulb, wider than it is tall, with a shallow
    // underside) held up by a thick trunk that splits into heavy limbs which show beneath it. Unlike
    // TreeMesh, the canopy is a single mass, not a cluster of separate blobs.
    // In wireframe the canopy is edged with its five latitude rings and all sixteen meridian lines, plus the
    // trunk and limb length lines, so the whole bulb reads as a lumpy dome over a forked trunk.
    // Built with the trunk's base on y = 0 (not centred), so it sits on the ground rather than tumbles.
    static class OakMesh
    {
        // Slots: trunk/limbs, then five leaf shades from darkest (underside) to lightest (top).
        public const int Trunk = 0, LeafBase = 1, LeafShades = 5;
        public const int PaletteSize = 1 + LeafShades;

        public static Color[] Palette(Color trunk, Color leaf)
        {
            var palette = new Color[PaletteSize];
            palette[Trunk] = trunk;
            palette[LeafBase + 0] = Scale(leaf, 0.50f);
            palette[LeafBase + 1] = Scale(leaf, 0.75f);
            palette[LeafBase + 2] = leaf;
            palette[LeafBase + 3] = Color.Lerp(leaf, Color.White, 0.15f);
            palette[LeafBase + 4] = Color.Lerp(leaf, Color.White, 0.35f);
            return palette;
        }

        private static Color Scale(Color c, float f) => new Color((int)(c.R * f), (int)(c.G * f), (int)(c.B * f));

        // The canopy: an ellipsoid, flatter underneath than on top, with bumps in its surface.
        // It is tall (top at 2.35) and hangs low: the underside comes down to 0.62 around the trunk.
        private static readonly Vector3 CanopyCentre = new Vector3(0f, 1.4f, 0f);
        private const float CanopyRadius = 1.0f;        // out to the sides
        private const float CanopyHeightAbove = 0.95f;  // centre to the top
        private const float CanopyHeightBelow = 0.78f;  // centre to the underside
        private const float Lumpiness = 0.09f;          // how far the surface bumps in and out (fraction of the radius)

        private const int Segments = 16;
        private static readonly float[] Latitudes =
        {
            MathHelper.ToRadians(-60f), MathHelper.ToRadians(-30f), 0f, MathHelper.ToRadians(30f), MathHelper.ToRadians(60f),
        };

        // A repeatable pseudo-random bump for each ring point (-1..1), so the surface is uneven but the same every time.
        private static float Bump(int segment, int ring) =>
            MathF.Sin(segment * 2.1f + ring * 1.7f) * MathF.Cos(segment * 0.9f - ring * 2.3f);

        public static MeshData Build(GraphicsDevice device)
        {
            var mesh = new MeshBuilder();

            // ---- Trunk: thick, flared at the base, up to the fork
            var fork = new Vector3(0f, 0.42f, 0f);
            mesh.AddTube(Vector3.Zero, new Vector3(0f, 0.13f, 0f), 0.22f, 0.15f, 5, Trunk);
            mesh.AddTube(new Vector3(0f, 0.13f, 0f), fork, 0.15f, 0.11f, 5, Trunk);

            // ---- Limbs: heavy branches from the fork, ending inside the canopy
            void Limb(Vector3 from, Vector3 to, float fromRadius, float toRadius) =>
                mesh.AddTube(from, to, fromRadius, toRadius, 4, Trunk);

            Limb(fork, new Vector3(-0.22f, 0.90f, 0.04f), 0.08f, 0.05f);      // two upright limbs
            Limb(fork, new Vector3(0.24f, 0.88f, -0.04f), 0.08f, 0.05f);
            var leftElbow = new Vector3(-0.34f, 0.62f, 0.04f);                  // two long limbs that sweep out and up
            Limb(fork, leftElbow, 0.085f, 0.06f);
            Limb(leftElbow, new Vector3(-0.68f, 0.98f, 0.10f), 0.06f, 0.035f);
            var rightElbow = new Vector3(0.32f, 0.62f, -0.04f);
            Limb(fork, rightElbow, 0.085f, 0.06f);
            Limb(rightElbow, new Vector3(0.66f, 0.96f, -0.10f), 0.06f, 0.035f);
            Limb(fork, new Vector3(0.06f, 0.86f, 0.34f), 0.07f, 0.04f);         // one to the front and one to the back
            Limb(fork, new Vector3(-0.06f, 0.86f, -0.34f), 0.07f, 0.04f);

            // ---- Canopy surface: rings of points, bumped, joined by faces
            var poleTop = CanopyCentre + new Vector3(0f, CanopyHeightAbove, 0f);
            var poleBottom = CanopyCentre + new Vector3(0f, -CanopyHeightBelow, 0f);

            var rings = new Vector3[Latitudes.Length][];
            for (var r = 0; r < Latitudes.Length; r++)
            {
                var lat = Latitudes[r];
                var height = MathF.Sin(lat) * (lat > 0f ? CanopyHeightAbove : CanopyHeightBelow);
                rings[r] = new Vector3[Segments];
                for (var k = 0; k < Segments; k++)
                {
                    var azimuth = k * MathHelper.TwoPi / Segments;
                    var offset = new Vector3(
                        CanopyRadius * MathF.Cos(lat) * MathF.Cos(azimuth),
                        height,
                        CanopyRadius * MathF.Cos(lat) * MathF.Sin(azimuth));
                    rings[r][k] = CanopyCentre + offset * (1f + Lumpiness * Bump(k, r));
                }
            }

            // Faces, sorted into shades by how far they face up, so each shade is one contiguous range
            var faceSets = new List<(Vector3 a, Vector3 b, Vector3 c)>[LeafShades];
            for (var s = 0; s < LeafShades; s++)
                faceSets[s] = new List<(Vector3, Vector3, Vector3)>();

            void Face(Vector3 a, Vector3 b, Vector3 c)
            {
                var facing = Vector3.Normalize((a + b + c) / 3f - CanopyCentre).Y;
                var shade = facing > 0.72f ? 4 : facing > 0.35f ? 3 : facing > -0.15f ? 2 : facing > -0.55f ? 1 : 0;
                faceSets[shade].Add((a, b, c));
            }

            var last = Latitudes.Length - 1;
            for (var k = 0; k < Segments; k++)
            {
                var n = (k + 1) % Segments;
                Face(poleBottom, rings[0][n], rings[0][k]);
                for (var r = 0; r < last; r++)
                {
                    Face(rings[r][k], rings[r][n], rings[r + 1][n]);
                    Face(rings[r][k], rings[r + 1][n], rings[r + 1][k]);
                }
                Face(poleTop, rings[last][k], rings[last][n]);
            }

            for (var shade = 0; shade < LeafShades; shade++)
            {
                if (faceSets[shade].Count == 0)
                    continue;
                mesh.AddSolidRange(faceSets[shade].Count, LeafBase + shade);
                foreach (var (a, b, c) in faceSets[shade])
                    mesh.AddTri(a, b, c);
            }

            // ---- Canopy edges: every latitude ring and every meridian from pole to pole, so each face is outlined
            foreach (var ring in rings)
                mesh.AddLineLoop(ring);
            for (var k = 0; k < Segments; k++)
            {
                mesh.AddLine(poleTop, rings[last][k]);
                for (var r = last; r > 0; r--)
                    mesh.AddLine(rings[r][k], rings[r - 1][k]);
                mesh.AddLine(rings[0][k], poleBottom);
            }

            return mesh.Build(device);
        }
    }
}
