using MeshCore.Library;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MeshProps
{
    // A small bedside cabinet: a drawer at the top and a cupboard under it, its door hinged at the left, a knob on each,
    // on a low plinth. Width x Depth x Height, its front towards +Z, centred on X and Z, on y = 0.
    public static class BedsideTableMesh
    {
        public const int Carcass = 0, Front = 3, Knob = 6;
        public const int PaletteSize = 9;

        public const float Width = 0.45f, Depth = 0.38f, Height = 0.55f;

        public static Color[] Palette(Color carcass, Color front, Color knob)
        {
            var palette = new Color[PaletteSize];
            MeshBuilder.SetBoxShades(palette, Carcass, carcass);
            MeshBuilder.SetBoxShades(palette, Front, front);
            MeshBuilder.SetBoxShades(palette, Knob, knob);
            return palette;
        }

        public static MeshData Build(GraphicsDevice device)
        {
            var mesh = new MeshBuilder();
            const float plinth = 0.05f, top = 0.025f, proud = 0.015f, gap = 0.008f, inset = 0.03f, drawer = 0.13f;

            mesh.AddBox(Carcass, new Vector3(0f, 0f, -0.01f), Depth - 0.04f, Width - 0.04f, plinth);   // the plinth, set back
            mesh.AddBox(Carcass, new Vector3(0f, plinth, 0f), Depth, Width, Height - plinth - top);
            mesh.AddBox(Carcass, new Vector3(0f, Height - top, 0.005f), Depth + 0.01f, Width + 0.02f, top);   // the top, a little over

            // The fronts on its face: the drawer, and the door under it
            var face = Depth / 2f + proud / 2f;
            var drawerBottom = Height - top - inset - drawer;
            var width = Width - 2f * inset;
            mesh.AddBox(Front, new Vector3(0f, drawerBottom, face), proud, width, drawer);
            var doorTop = drawerBottom - gap;
            var doorBottom = plinth + inset;
            mesh.AddBox(Front, new Vector3(0f, doorBottom, face), proud, width, doorTop - doorBottom);

            // A knob in the middle of the drawer, and one up the door's right-hand edge
            var knob = Depth / 2f + proud + 0.01f;
            mesh.AddBox(Knob, new Vector3(0f, drawerBottom + drawer / 2f - 0.01f, knob), 0.02f, 0.03f, 0.02f);
            mesh.AddBox(Knob, new Vector3(width / 2f - 0.04f, (doorBottom + doorTop) / 2f - 0.01f, knob), 0.02f, 0.02f, 0.02f);
            return mesh.Build(device);
        }
    }
}
