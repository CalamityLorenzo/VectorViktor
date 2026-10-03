using MeshCore.Library;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MeshProps
{
    // A 1950s office desk: a thin teak top over a pedestal of three drawers on the right, on short splayed legs, and on
    // the left two tall splayed legs, with a shallow pencil drawer under the top between them and a modesty panel at the
    // back. 1.40 wide, 0.70 deep, 0.75 to the top. The front (where you sit) faces +Z, centred on X and Z, standing on
    // y = 0; Height is the top's height, for things to stand on it.
    public static class OfficeDeskMesh
    {
        // Three-shade parts (see MeshBuilder): the top, pedestal and panels, the drawers, the pulls; the legs are one slot.
        public const int BodyBase = 0, DrawerBase = 3, Leg = 6, PullBase = 7;
        public const int PaletteSize = 10;

        public const float Height = 0.75f;

        public static Color[] Palette(Color body, Color drawer, Color leg, Color pull)
        {
            var palette = new Color[PaletteSize];
            MeshBuilder.SetBoxShades(palette, BodyBase, body);
            MeshBuilder.SetBoxShades(palette, DrawerBase, drawer);
            palette[Leg] = leg;
            MeshBuilder.SetBoxShades(palette, PullBase, pull);
            return palette;
        }

        public static MeshData Build(GraphicsDevice device)
        {
            const float topWidth = 1.40f, topDepth = 0.70f, topThickness = 0.03f;
            const float underTop = Height - topThickness;
            const float bodyDepth = 0.63f, bodyZ = -0.005f;   // the pedestal and the pencil drawer's housing, set back under the top
            const float pedestalLeft = 0.25f, pedestalRight = 0.67f, pedestalBottom = 0.10f;
            const float housingLeft = -0.64f, housingBottom = 0.64f;
            const float drawerDepth = 0.015f, gap = 0.012f;
            const float pullHeight = 0.016f, pullDepth = 0.022f;
            var frontZ = bodyZ + bodyDepth / 2f;

            var mesh = new MeshBuilder();

            // An upright box from x0 to x1 and y0 to y1, `depth` deep, centred on z
            void Box(int slot, float x0, float x1, float y0, float y1, float z, float depth, bool sealBottom = false) =>
                mesh.AddBox(slot, new Vector3((x0 + x1) / 2f, y0, z), depth, x1 - x0, y1 - y0, sealBottom);

            // A drawer front proud of the front, with a bar pull a touch above its middle
            void Drawer(float x0, float x1, float y0, float y1, float pullWidth)
            {
                Box(DrawerBase, x0, x1, y0, y1, frontZ + drawerDepth / 2f, drawerDepth);
                var pullY = y0 + (y1 - y0) * 0.55f;
                Box(PullBase, (x0 + x1 - pullWidth) / 2f, (x0 + x1 + pullWidth) / 2f, pullY, pullY + pullHeight, frontZ + drawerDepth + pullDepth / 2f, pullDepth);
            }

            // The top, sealed underneath where it overhangs
            Box(BodyBase, -topWidth / 2f, topWidth / 2f, underTop, Height, 0f, topDepth, sealBottom: true);

            // The pedestal and its three drawers, top down, the bottom one deep enough for files
            Box(BodyBase, pedestalLeft, pedestalRight, pedestalBottom, underTop, bodyZ, bodyDepth, sealBottom: true);
            var y = underTop - gap;
            foreach (var height in new[] { 0.15f, 0.17f, 0.25f })
            {
                Drawer(pedestalLeft + 0.015f, pedestalRight - 0.015f, y - height, y, 0.12f);
                y -= height + gap;
            }
            foreach (var x in new[] { pedestalLeft + 0.04f, pedestalRight - 0.04f })
                foreach (var sz in new[] { -1f, 1f })
                    mesh.AddTube(
                        new Vector3(x, pedestalBottom, bodyZ + sz * 0.26f),
                        new Vector3(x + (x > (pedestalLeft + pedestalRight) / 2f ? 0.02f : -0.02f), 0f, bodyZ + sz * 0.28f),
                        0.025f, 0.018f, 4, Leg);

            // The pencil drawer's housing under the top, from the left legs to the pedestal, and its drawer
            Box(BodyBase, housingLeft, pedestalLeft, housingBottom, underTop, bodyZ, bodyDepth, sealBottom: true);
            Drawer(-0.45f, 0.06f, housingBottom + 0.012f, underTop - 0.012f, 0.10f);

            // The modesty panel at the back, under the housing, from the left legs to the pedestal
            Box(BodyBase, housingLeft + 0.08f, pedestalLeft, 0.30f, housingBottom, bodyZ - bodyDepth / 2f + 0.02f, 0.02f, sealBottom: true);

            // The tall legs at the left, splayed, from under the housing
            foreach (var sz in new[] { -1f, 1f })
                mesh.AddTube(
                    new Vector3(-0.60f, housingBottom, sz * 0.27f),
                    new Vector3(-0.65f, 0f, sz * 0.31f),
                    0.03f, 0.02f, 4, Leg);

            return mesh.Build(device);
        }
    }
}
