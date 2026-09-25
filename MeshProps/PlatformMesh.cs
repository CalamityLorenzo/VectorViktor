using MeshCore.Library;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MeshProps
{
    // A flat rectangular deck — a mezzanine or balcony floor — with no railing. Built as a single box
    // (see MeshBuilder.AddBox): length runs along Z, width along X, and the position argument is the
    // centre of its underside, same as every other box-based mesh here.
    public static class PlatformMesh
    {
        public const int DeckBase = 0;
        public const int PaletteSize = 3;

        public static Color[] Palette(Color deck)
        {
            var palette = new Color[PaletteSize];
            MeshBuilder.SetBoxShades(palette, DeckBase, deck);
            return palette;
        }

        // Keyed by its size, so two platforms share a mesh only if they're the same size.
        public static MeshSource Source(float length, float width, float thickness, Color[] palette) =>
            new MeshSource($"platform:{length:F3}x{width:F3}x{thickness:F3}", d => Build(d, length, width, thickness), palette);

        public static MeshData Build(GraphicsDevice device, float length, float width, float thickness)
        {
            var mesh = new MeshBuilder();
            mesh.AddBox(DeckBase, Vector3.Zero, length, width, thickness, sealBottom: true);
            return mesh.Build(device);
        }
    }
}
