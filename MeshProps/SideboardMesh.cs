using MeshCore.Library;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MeshProps
{
    // A 1950s sideboard: a long low teak cabinet with a thin overhanging top, three flat doors with
    // upright brass pulls, on four splayed legs. 1.46 wide, 0.46 deep, 0.64 to the top. The front faces +Z,
    // centred on X and Z, standing on y = 0.
    public static class SideboardMesh
    {
        // Three-shade parts (see MeshBuilder): the carcass and top, the doors, the pulls; the legs are one slot.
        public const int CarcassBase = 0, DoorBase = 3, Leg = 6, PullBase = 7;
        public const int PaletteSize = 10;

        public static Color[] Palette(Color carcass, Color door, Color leg, Color pull)
        {
            var palette = new Color[PaletteSize];
            MeshBuilder.SetBoxShades(palette, CarcassBase, carcass);
            MeshBuilder.SetBoxShades(palette, DoorBase, door);
            palette[Leg] = leg;
            MeshBuilder.SetBoxShades(palette, PullBase, pull);
            return palette;
        }

        public static MeshData Build(GraphicsDevice device)
        {
            const float carcassWidth = 1.40f, carcassDepth = 0.42f, carcassHeight = 0.40f;
            const float legHeight = 0.20f;
            const float topWidth = 1.46f, topDepth = 0.46f, topThickness = 0.04f;

            var mesh = new MeshBuilder();

            // Carcass, then the top slab sitting exactly on it
            mesh.AddBox(CarcassBase, new Vector3(0f, legHeight, 0f), carcassDepth, carcassWidth, carcassHeight, sealBottom: true);
            mesh.AddBox(CarcassBase, new Vector3(0f, legHeight + carcassHeight, 0f), topDepth, topWidth, topThickness, sealBottom: true);

            // Three doors proud of the front face, each with an upright pull near its inner edge
            const float doorWidth = 0.44f, doorHeight = 0.34f, doorDepth = 0.012f;
            const float pullWidth = 0.02f, pullHeight = 0.10f, pullDepth = 0.024f;
            var frontZ = carcassDepth / 2f;
            foreach (var x in new[] { -0.46f, 0f, 0.46f })
            {
                mesh.AddBox(DoorBase, new Vector3(x, legHeight + 0.03f, frontZ + doorDepth / 2f), doorDepth, doorWidth, doorHeight);
                mesh.AddBox(PullBase, new Vector3(x + 0.16f, legHeight + 0.14f, frontZ + doorDepth + pullDepth / 2f), pullDepth, pullWidth, pullHeight);
            }

            // Splayed legs from under the carcass's corners
            foreach (var sx in new[] { -1f, 1f })
                foreach (var sz in new[] { -1f, 1f })
                    mesh.AddTube(
                        new Vector3(sx * 0.60f, legHeight, sz * 0.15f),
                        new Vector3(sx * 0.65f, 0f, sz * 0.18f),
                        0.03f, 0.02f, 4, Leg);

            return mesh.Build(device);
        }
    }
}
