using MeshCore.Library;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MeshProps
{
    // A 1950s teak dining table to seat four: a thin top over a shallow apron, on four tapered legs splayed out from
    // under its corners. 1.50 long (along X), 0.85 wide, 0.75 to the top; Height, for things to stand on it. Centred on
    // X and Z, standing on y = 0.
    public static class DiningTableMesh
    {
        // The top and apron have three shades (see MeshBuilder); the legs one.
        public const int TopBase = 0, Leg = 3;
        public const int PaletteSize = 4;

        public const float Height = 0.75f, Length = 1.50f, Width = 0.85f;

        public static Color[] Palette(Color top, Color leg)
        {
            var palette = new Color[PaletteSize];
            MeshBuilder.SetBoxShades(palette, TopBase, top);
            palette[Leg] = leg;
            return palette;
        }

        public static MeshData Build(GraphicsDevice device)
        {
            const float topThickness = 0.03f, apronHeight = 0.07f, apronInset = 0.08f;
            const float underTop = Height - topThickness;
            var mesh = new MeshBuilder();

            // The top, sealed underneath where it overhangs, and the apron under it, set in from its edges
            mesh.AddBox(TopBase, new Vector3(0f, underTop, 0f), Width, Length, topThickness, sealBottom: true);
            mesh.AddBox(TopBase, new Vector3(0f, underTop - apronHeight, 0f), Width - 2f * apronInset, Length - 2f * apronInset, apronHeight, sealBottom: true);

            // Legs from under the apron's corners, splayed out to the floor
            var legX = Length / 2f - apronInset - 0.03f;
            var legZ = Width / 2f - apronInset - 0.03f;
            foreach (var sx in new[] { -1f, 1f })
                foreach (var sz in new[] { -1f, 1f })
                    mesh.AddTube(
                        new Vector3(sx * legX, underTop - apronHeight, sz * legZ),
                        new Vector3(sx * (legX + 0.05f), 0f, sz * (legZ + 0.04f)),
                        0.03f, 0.018f, 4, Leg);

            return mesh.Build(device);
        }
    }
}
