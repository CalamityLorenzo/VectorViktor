using MeshCore.Library;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MeshProps
{
    // A 1950s chest of drawers to go with the sideboard: a teak carcass with a thin overhanging top, four flat drawer
    // fronts getting deeper towards the floor, each with a long brass bar pull, on four splayed legs. 0.80 wide, 0.46
    // deep, 0.99 to the top. The front faces +Z, centred on X and Z, standing on y = 0.
    public static class ChestOfDrawersMesh
    {
        // Three-shade parts (see MeshBuilder): the carcass and top, the drawers, the pulls; the legs are one slot.
        public const int CarcassBase = 0, DrawerBase = 3, Leg = 6, PullBase = 7;
        public const int PaletteSize = 10;

        public static Color[] Palette(Color carcass, Color drawer, Color leg, Color pull)
        {
            var palette = new Color[PaletteSize];
            MeshBuilder.SetBoxShades(palette, CarcassBase, carcass);
            MeshBuilder.SetBoxShades(palette, DrawerBase, drawer);
            palette[Leg] = leg;
            MeshBuilder.SetBoxShades(palette, PullBase, pull);
            return palette;
        }

        public static MeshData Build(GraphicsDevice device)
        {
            const float carcassWidth = 0.76f, carcassDepth = 0.42f, carcassHeight = 0.78f;
            const float legHeight = 0.18f;
            const float topWidth = 0.80f, topDepth = 0.46f, topThickness = 0.03f;

            var mesh = new MeshBuilder();

            // Carcass, then the top slab sitting exactly on it
            mesh.AddBox(CarcassBase, new Vector3(0f, legHeight, 0f), carcassDepth, carcassWidth, carcassHeight, sealBottom: true);
            mesh.AddBox(CarcassBase, new Vector3(0f, legHeight + carcassHeight, 0f), topDepth, topWidth, topThickness, sealBottom: true);

            // Drawer fronts proud of the front face, top down, a little gap between each, a bar pull a touch above each one's middle
            const float drawerWidth = 0.72f, drawerDepth = 0.015f, gap = 0.012f;
            const float pullWidth = 0.16f, pullHeight = 0.016f, pullDepth = 0.022f;
            var frontZ = carcassDepth / 2f;
            var y = legHeight + carcassHeight - gap;   // the top of the next drawer down
            foreach (var height in new[] { 0.15f, 0.17f, 0.19f, 0.21f })
            {
                y -= height;
                mesh.AddBox(DrawerBase, new Vector3(0f, y, frontZ + drawerDepth / 2f), drawerDepth, drawerWidth, height);
                mesh.AddBox(PullBase, new Vector3(0f, y + height * 0.55f, frontZ + drawerDepth + pullDepth / 2f), pullDepth, pullWidth, pullHeight);
                y -= gap;
            }

            // Splayed legs from under the carcass's corners
            foreach (var sx in new[] { -1f, 1f })
                foreach (var sz in new[] { -1f, 1f })
                    mesh.AddTube(
                        new Vector3(sx * 0.32f, legHeight, sz * 0.16f),
                        new Vector3(sx * 0.36f, 0f, sz * 0.19f),
                        0.03f, 0.02f, 4, Leg);

            return mesh.Build(device);
        }
    }
}
