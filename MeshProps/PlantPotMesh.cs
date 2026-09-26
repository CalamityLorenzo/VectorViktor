using MeshCore.Library;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MeshProps
{
    // A tall pot plant for a corner: an eight-sided terracotta pot with soil, and a clump of upright, sword-shaped
    // leaves (a snake plant) leaning out a little from its middle. Built with the pot's base on y = 0.
    public static class PlantPotMesh
    {
        // Slots: pot side, pot rim (and underside), soil, and two greens that alternate between leaves.
        public const int PotSide = 0, PotRim = 1, Soil = 2, LeafA = 3, LeafB = 4;
        public const int PaletteSize = 5;

        public const float PotHeight = 0.34f;

        public static Color[] Palette(Color pot, Color leaf)
        {
            var palette = new Color[PaletteSize];
            palette[PotSide] = pot;
            palette[PotRim] = Color.Lerp(pot, Color.White, 0.25f);
            palette[Soil] = new Color(60, 40, 25);
            palette[LeafA] = leaf;
            palette[LeafB] = Color.Lerp(leaf, Color.Yellow, 0.2f);
            return palette;
        }

        private const int LeafCount = 9;

        public static MeshData Build(GraphicsDevice device)
        {
            const float rimHeight = 0.05f;
            var mesh = new MeshBuilder();

            // The pot, and a lip round its top a little wider than it; the soil a little way down inside the lip
            mesh.AddFrustum(Vector3.Zero, 0.13f, 0.17f, PotHeight - rimHeight, 8, PotSide, PotRim, verticalEdges: true);
            mesh.AddFrustum(new Vector3(0f, PotHeight - rimHeight, 0f), 0.185f, 0.185f, rimHeight, 8, PotRim, topSlot: Soil);

            // Each leaf a flat blade from the soil: widening, then tapering to its tip, leaning out and curving over
            // a little. Turned about its own spine so they don't all face the same way.
            for (var i = 0; i < LeafCount; i++)
            {
                var angle = i * MathHelper.TwoPi / LeafCount + 0.4f * (i % 3);
                var length = 0.55f + 0.12f * ((i * 5) % 4);
                var lean = 0.12f + 0.08f * ((i * 7) % 3);
                var halfWidth = 0.04f + 0.01f * (i % 2);

                var radial = new Vector3(MathF.Cos(angle), 0f, MathF.Sin(angle));
                var facing = new Vector3(MathF.Cos(angle + 1.1f), 0f, MathF.Sin(angle + 1.1f));   // across the blade
                var foot = new Vector3(0f, PotHeight - 0.01f, 0f) + radial * 0.04f;

                float[] along = { 0f, 0.3f, 0.65f, 1f };
                float[] width = { 0.5f, 1f, 0.8f, 0f };
                var left = new Vector3[along.Length];
                var right = new Vector3[along.Length];
                for (var j = 0; j < along.Length; j++)
                {
                    var t = along[j];
                    var spine = foot + Vector3.Up * (length * t) + radial * (lean * t * t);
                    left[j] = spine + facing * (halfWidth * width[j]);
                    right[j] = spine - facing * (halfWidth * width[j]);
                }
                var slot = i % 2 == 0 ? LeafA : LeafB;
                for (var j = 0; j < along.Length - 1; j++)
                    mesh.AddQuad(slot, left[j], left[j + 1], right[j + 1], right[j]);

                mesh.AddLineLoop(left[0], left[1], left[2], left[3], right[2], right[1], right[0]);
            }

            return mesh.Build(device);
        }
    }
}
