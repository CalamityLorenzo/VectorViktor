using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace BasicTests.Meshes
{
    // A low coffee table: a thick top, four square legs and a lower shelf between them. The long
    // side runs along X. Built with its feet on y = 0 (not centred), as it is meant to sit on the
    // ground rather than tumble.
    static class CoffeeTableMesh
    {
        // Two wood parts, each with three shades (see MeshBuilder): the top and shelf, and the legs.
        public const int TopBase = 0, LegBase = 3;
        public const int PaletteSize = 6;

        public static Color[] Palette(Color top, Color legs)
        {
            var palette = new Color[PaletteSize];
            MeshBuilder.SetBoxShades(palette, TopBase, top);
            MeshBuilder.SetBoxShades(palette, LegBase, legs);
            return palette;
        }

        public static MeshData Build(GraphicsDevice device)
        {
            const float length = 1.0f;       // along X
            const float depth = 0.5f;        // along Z
            const float legHeight = 0.24f;
            const float topThickness = 0.04f;
            const float legSize = 0.05f;
            const float legInset = 0.06f;    // from the top's edge to the leg's outside face
            const float shelfThickness = 0.025f;
            const float shelfHeight = 0.07f; // shelf underside above the floor

            var mesh = new MeshBuilder();

            // Top slab, sealed underneath so it is a closed slab from any angle
            mesh.AddBox(TopBase, new Vector3(0f, legHeight, 0f), depth, length, topThickness, sealBottom: true);

            // Legs: centres inset from the corners
            var legX = length * 0.5f - legInset - legSize * 0.5f;
            var legZ = depth * 0.5f - legInset - legSize * 0.5f;
            foreach (var x in new[] { -legX, legX })
                foreach (var z in new[] { -legZ, legZ })
                    mesh.AddBox(LegBase, new Vector3(x, 0f, z), legSize, legSize, legHeight);

            // Shelf: fits exactly between the legs' inner faces so nothing overlaps
            var shelfLength = 2f * (legZ - legSize * 0.5f);   // along Z
            var shelfWidth = 2f * (legX - legSize * 0.5f);    // along X
            mesh.AddBox(TopBase, new Vector3(0f, shelfHeight, 0f), shelfLength, shelfWidth, shelfThickness, sealBottom: true);

            return mesh.Build(device);
        }
    }
}
