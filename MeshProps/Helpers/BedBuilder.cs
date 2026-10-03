using MeshCore.Library;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MeshProps.Helpers
{
    // Shared by SingleBedMesh and DoubleBedMesh: a made-up 1950s bed. A low wooden frame on two tapered legs at the
    // foot, a headboard panel between two square posts at the head, the mattress, a pillow or two, and a blanket over
    // the rest with the sheet turned down over its top edge, hanging off the sides and the foot with two stripes
    // across it. The mattress is 1.90 long; the foot faces +Z, the head is at -Z, centred on X and Z on the frame,
    // standing on y = 0.
    //
    // Slots: the frame and posts, the headboard panel, the linen (mattress, sheet, pillows) and the blanket have three
    // shades each (see MeshBuilder), the legs one.
    public static class BedBuilder
    {
        public const int WoodBase = 0, PanelBase = 3, LinenBase = 6, BlanketBase = 9, Leg = 12;
        public const int PaletteSize = 13;

        public static Color[] Palette(Color wood, Color panel, Color linen, Color blanket, Color leg)
        {
            var palette = new Color[PaletteSize];
            MeshBuilder.SetBoxShades(palette, WoodBase, wood);
            MeshBuilder.SetBoxShades(palette, PanelBase, panel);
            MeshBuilder.SetBoxShades(palette, LinenBase, linen);
            MeshBuilder.SetBoxShades(palette, BlanketBase, blanket);
            palette[Leg] = leg;
            return palette;
        }

        public static MeshData Build(GraphicsDevice device, float mattressWidth, int pillows)
        {
            const float mattressLength = 1.90f, mattressHeight = 0.16f;
            const float legHeight = 0.14f, frameHeight = 0.16f, rim = 0.03f;   // the frame shows `rim` round the mattress
            const float postSize = 0.05f, headHeight = 0.92f;
            const float panelDepth = 0.03f, panelBottom = 0.36f, panelTop = 0.88f;
            const float pillowDepth = 0.38f, pillowHeight = 0.11f, pillowWidth = 0.60f, pillowGap = 0.05f;
            const float sheetOnTop = 0.50f;     // how much of the mattress, from its head end, the blanket leaves bare
            const float turnDown = 0.14f;       // the sheet folded back over the blanket's top edge
            const float hang = 0.10f;           // how far the blanket hangs down the mattress's sides
            const float blanketOver = 0.03f;    // how far it stands out from the mattress, and above it

            var frameWidth = mattressWidth + 2f * rim;
            var frameLength = mattressLength + 2f * rim;
            var frameTop = legHeight + frameHeight;
            var mattressTop = frameTop + mattressHeight;
            var head = -mattressLength / 2f;
            var foot = mattressLength / 2f;
            var fold = head + sheetOnTop;
            var blanketBottom = mattressTop - hang;

            var mesh = new MeshBuilder();

            // Frame, sealed underneath
            mesh.AddBox(WoodBase, new Vector3(0f, legHeight, 0f), frameLength, frameWidth, frameHeight, sealBottom: true);

            // Headboard: two posts from the floor, flush with the frame's sides and just behind it, and the panel between them
            var headZ = -frameLength / 2f - postSize / 2f;
            foreach (var side in new[] { -1f, 1f })
                mesh.AddBox(WoodBase, new Vector3(side * (frameWidth / 2f - postSize / 2f), 0f, headZ), postSize, postSize, headHeight);
            mesh.AddBox(PanelBase, new Vector3(0f, panelBottom, headZ), panelDepth, frameWidth - 2f * postSize, panelTop - panelBottom, sealBottom: true);

            // Tapered legs at the foot, splayed out a little (the posts hold up the head)
            var legX = frameWidth / 2f - 0.07f;
            var legZ = frameLength / 2f - 0.07f;
            foreach (var side in new[] { -1f, 1f })
                mesh.AddTube(
                    new Vector3(side * legX, legHeight, legZ),
                    new Vector3(side * (legX + 0.03f), 0f, legZ + 0.03f),
                    0.03f, 0.02f, 4, Leg);

            // The mattress: the bare head end its full height, and under the blanket only as far up as the blanket hangs
            mesh.AddBox(LinenBase, new Vector3(0f, frameTop, (head + fold) / 2f), fold - head, mattressWidth, mattressHeight);
            mesh.AddBox(LinenBase, new Vector3(0f, frameTop, (fold + foot) / 2f), foot - fold, mattressWidth, blanketBottom - frameTop);

            // Pillows side by side at the head
            for (var i = 0; i < pillows; i++)
            {
                var x = (i - (pillows - 1) / 2f) * (pillowWidth + pillowGap);
                mesh.AddBox(LinenBase, new Vector3(x, mattressTop, head + 0.04f + pillowDepth / 2f), pillowDepth, pillowWidth, pillowHeight, sealBottom: true);
            }

            // The sheet turned down over the blanket's top edge, standing a little prouder than it; then the blanket, to
            // just past the foot, sealed underneath where it hangs
            var blanketWidth = mattressWidth + 2f * blanketOver;
            var blanketHeight = mattressTop + blanketOver - blanketBottom;
            var blanketHead = fold + turnDown;
            var blanketFoot = foot + blanketOver;
            mesh.AddBox(LinenBase, new Vector3(0f, blanketBottom, fold + turnDown / 2f), turnDown, blanketWidth + 0.01f, blanketHeight + 0.012f, sealBottom: true);
            mesh.AddBox(BlanketBase, new Vector3(0f, blanketBottom, (blanketHead + blanketFoot) / 2f), blanketFoot - blanketHead, blanketWidth, blanketHeight, sealBottom: true);

            // Two stripes across the blanket near the foot: over its top and down both sides
            var hx = blanketWidth / 2f;
            var top = blanketBottom + blanketHeight;
            foreach (var z in new[] { foot - 0.30f, foot - 0.24f })
            {
                mesh.AddLine(new Vector3(-hx, blanketBottom, z), new Vector3(-hx, top, z));
                mesh.AddLine(new Vector3(-hx, top, z), new Vector3(hx, top, z));
                mesh.AddLine(new Vector3(hx, top, z), new Vector3(hx, blanketBottom, z));
            }

            return mesh.Build(device);
        }
    }
}
