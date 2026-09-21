using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace BasicTests.Meshes
{
    // A small potted fern: an eight-sided tapered pot with soil, and nine arching fronds that fan out
    // from the centre. Each frond is a flat ribbon that widens then tapers to a point. Built with the
    // pot's base on y = 0 (not centred), so it sits on the ground rather than tumbles.
    static class FernMesh
    {
        // Slots: pot side, pot underside, soil, and two greens that alternate between fronds.
        public const int PotSide = 0, PotBottom = 1, Soil = 2, FrondA = 3, FrondB = 4;
        public const int PaletteSize = 5;

        public static Color[] Palette(Color pot, Color frond)
        {
            var palette = new Color[PaletteSize];
            palette[PotSide] = pot;
            palette[PotBottom] = new Color((int)(pot.R * 0.55f), (int)(pot.G * 0.55f), (int)(pot.B * 0.55f));
            palette[Soil] = new Color(60, 40, 25);
            palette[FrondA] = frond;
            palette[FrondB] = Color.Lerp(frond, Color.White, 0.25f);
            return palette;
        }

        private const int FrondCount = 9;
        private const float PotHeight = 0.12f;

        // Positions along a frond, from its base to its tip, and the ribbon's half-width at each.
        private static readonly float[] Along = { 0f, 0.25f, 0.5f, 0.75f, 1f };
        private static readonly float[] HalfWidth = { 0.012f, 0.035f, 0.045f, 0.032f, 0f };

        public static MeshData Build(GraphicsDevice device)
        {
            var mesh = new MeshBuilder();

            // Pot (with its soil as the top cap); the vertical edges are its silhouette in wireframe
            mesh.AddFrustum(Vector3.Zero, 0.08f, 0.11f, PotHeight, 8, PotSide, PotBottom, Soil, verticalEdges: true);

            // Each frond as its left edge, right edge and spine, at 5 stations
            var fronds = new List<(Vector3[] left, Vector3[] right, Vector3[] spine)>();
            for (var i = 0; i < FrondCount; i++)
            {
                var angle = i * MathHelper.TwoPi / FrondCount + 0.15f * (i % 2);
                var length = 0.30f + 0.06f * ((i * 5) % 3);
                var arch = 0.10f + 0.05f * ((i * 7) % 3);
                var widthScale = 0.9f + 0.1f * ((i * 3) % 3);

                var radial = new Vector3(MathF.Cos(angle), 0f, MathF.Sin(angle));
                var sideways = new Vector3(-radial.Z, 0f, radial.X);

                var left = new Vector3[Along.Length];
                var right = new Vector3[Along.Length];
                var spine = new Vector3[Along.Length];
                for (var j = 0; j < Along.Length; j++)
                {
                    var t = Along[j];
                    var centre = radial * (0.02f + length * t)
                        + Vector3.Up * (PotHeight + arch * MathF.Sin(MathF.PI * 0.85f * t));
                    spine[j] = centre;
                    left[j] = centre + sideways * (HalfWidth[j] * widthScale);
                    right[j] = centre - sideways * (HalfWidth[j] * widthScale);
                }
                fronds.Add((left, right, spine));
            }

            // Fronds in two passes (even, then odd) so each green is one contiguous range
            for (var parity = 0; parity < 2; parity++)
            {
                var count = 0;
                for (var i = parity; i < FrondCount; i += 2)
                    count++;
                mesh.AddSolidRange(count * (Along.Length - 1) * 2, parity == 0 ? FrondA : FrondB);

                for (var i = parity; i < FrondCount; i += 2)
                {
                    var (left, right, _) = fronds[i];
                    for (var j = 0; j < Along.Length - 1; j++)
                        mesh.AddQuad(left[j], left[j + 1], right[j + 1], right[j]);
                }
            }

            // Edges: each frond's outline plus its central rib
            foreach (var (left, right, spine) in fronds)
            {
                var last = Along.Length - 1;
                var outline = new List<Vector3>();
                for (var j = 0; j <= last; j++)
                    outline.Add(left[j]);                    // ends at the tip, which is where left and right meet
                for (var j = last - 1; j >= 0; j--)
                    outline.Add(right[j]);
                mesh.AddLineLoop(outline.ToArray());
                for (var j = 0; j < last; j++)
                    mesh.AddLine(spine[j], spine[j + 1]);
            }

            return mesh.Build(device);
        }
    }
}
