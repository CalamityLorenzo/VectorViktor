using MeshCore.Library;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MeshProps
{
    // Odd pieces of crockery, and the plastic camping sporks the droid's hand is made of (see World.Rendering's DroidMesh):
    // a plate, a stack of them, a bowl, a mug, and a spork lying down. Each stands (or lies) on y = 0, centred on X and Z;
    // a mug's handle is towards +X, a spork's handle towards -X and its bowl towards +X.
    public static class CrockeryMesh
    {
        // The china, three shades (see MeshBuilder); the band round a rim and a bowl's or mug's inside; the sporks, three shades
        public const int China = 0, Band = 3, SporkBase = 4;
        public const int PaletteSize = 7;

        public const float PlateRadius = 0.11f, PlateHeight = 0.02f;
        public const float BowlRadius = 0.08f, BowlHeight = 0.06f;
        public const float MugRadius = 0.04f, MugHeight = 0.09f;
        public const float SporkLength = 0.16f;

        public static Color[] Palette(Color china, Color band, Color spork)
        {
            var palette = new Color[PaletteSize];
            MeshBuilder.SetBoxShades(palette, China, china);
            palette[Band] = band;
            MeshBuilder.SetBoxShades(palette, SporkBase, spork);
            return palette;
        }

        // A dinner plate: its underside rising to a rim, the band round the rim on top
        public static MeshData Plate(GraphicsDevice device) => Stack(device, 1);

        // `count` plates, one on another
        public static MeshData Stack(GraphicsDevice device, int count)
        {
            var mesh = new MeshBuilder();
            for (var k = 0; k < count; k++)
                mesh.AddFrustum(new Vector3(0f, k * PlateHeight, 0f), PlateRadius * 0.65f, PlateRadius, PlateHeight, 10, China + MeshBuilder.Side, China + MeshBuilder.Dim, Band);
            return mesh.Build(device);
        }

        public static MeshSource StackSource(int count, Color[] palette) =>
            new MeshSource($"crockery:plates:{count}", d => Stack(d, count), palette);

        // A pudding bowl, open: its inside the band's colour
        public static MeshData Bowl(GraphicsDevice device)
        {
            var mesh = new MeshBuilder();
            mesh.AddFrustum(Vector3.Zero, BowlRadius * 0.55f, BowlRadius, BowlHeight, 10, China + MeshBuilder.Side, China + MeshBuilder.Dim, Band);
            return mesh.Build(device);
        }

        // A mug, a loop of handle on its side
        public static MeshData Mug(GraphicsDevice device)
        {
            var mesh = new MeshBuilder();
            mesh.AddFrustum(Vector3.Zero, MugRadius, MugRadius, MugHeight, 8, China + MeshBuilder.Side, China + MeshBuilder.Dim, Band, verticalEdges: false);
            var (low, high, outward) = (new Vector3(MugRadius, 0.02f, 0f), new Vector3(MugRadius, 0.07f, 0f), new Vector3(0.03f, 0f, 0f));
            mesh.AddTube(low, low + outward, 0.006f, 0.006f, 4, China + MeshBuilder.Side);
            mesh.AddTube(low + outward, high + outward, 0.006f, 0.006f, 4, China + MeshBuilder.Side);
            mesh.AddTube(high + outward, high, 0.006f, 0.006f, 4, China + MeshBuilder.Side);
            return mesh.Build(device);
        }

        // A spork lying flat: a handle, and a shallow bowl with its tines' notches at the end
        public static MeshData Spork(GraphicsDevice device)
        {
            var mesh = new MeshBuilder();
            const float handle = SporkLength * 0.6f, thick = 0.006f, bowl = 0.022f;
            var start = -SporkLength / 2f;
            mesh.AddBox(SporkBase, new Vector3(start + handle / 2f, 0f, 0f), 0.014f, handle, thick);   // forward is +Z: the handle's along X
            var middle = start + handle + bowl;
            mesh.AddFrustum(new Vector3(middle, 0f, 0f), bowl * 0.8f, bowl, thick * 1.5f, 8, SporkBase + MeshBuilder.Side, -1, SporkBase + MeshBuilder.Top);
            var tip = middle + bowl;
            foreach (var z in new[] { -0.007f, 0.007f })
                mesh.AddLine(new Vector3(tip - 0.008f, thick * 1.5f + 0.001f, z), new Vector3(tip + 0.001f, thick * 1.5f + 0.001f, z));   // the tines
            return mesh.Build(device);
        }
    }
}
