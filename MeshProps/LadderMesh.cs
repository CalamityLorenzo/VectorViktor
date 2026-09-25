using MeshCore.Library;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MeshProps
{
    // A straight fixed ladder: two parallel rails and evenly spaced rungs between them, no cage or
    // hoops. Climbs from the origin (the foot, centred between the rails on y = 0) up by Height,
    // drifting by Lean along +Z as it rises — the way a fixed access ladder leans back into whatever
    // it is bolted to at the top. Lean = 0 gives a perfectly vertical ladder.
    public static class LadderMesh
    {
        public const int Rail = 0, Rung = 1;
        public const int PaletteSize = 2;

        public static Color[] Palette(Color rail, Color rung) => new[] { rail, rung };

        // Keyed by its shape, so two ladders share a mesh only if they're the same ladder.
        public static MeshSource Source(float height, float lean, Color[] palette) =>
            new MeshSource($"ladder:{height:F3}:{lean:F3}", d => Build(d, height, lean), palette);

        public static MeshData Build(
            GraphicsDevice device,
            float height,
            float lean = 0f,
            float width = 0.4f,
            float rungSpacing = 0.3f,
            float railRadius = 0.02f,
            float rungRadius = 0.015f)
        {
            var mesh = new MeshBuilder();

            Vector3 At(float t) => new Vector3(0f, height * t, lean * t);

            foreach (var side in new[] { -1f, 1f })
            {
                var sideOffset = Vector3.UnitX * (side * width * 0.5f);
                mesh.AddTube(At(0f) + sideOffset, At(1f) + sideOffset, railRadius, railRadius, 4, Rail);
            }

            // Evenly spaced between the foot and the head (not flush with either), so a rung never
            // lands exactly at floor or landing level.
            var rungCount = System.Math.Max(1, (int)MathF.Round(height / rungSpacing));
            for (var i = 1; i <= rungCount; i++)
            {
                var centre = At((float)i / (rungCount + 1));
                mesh.AddTube(
                    centre - Vector3.UnitX * (width * 0.5f), centre + Vector3.UnitX * (width * 0.5f),
                    rungRadius, rungRadius, 4, Rung);
            }

            return mesh.Build(device);
        }
    }
}
