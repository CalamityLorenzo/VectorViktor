using MeshCore.Library;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MeshProps.Helpers
{
    // Shared by SofaMesh and SetteeMesh: an upholstered mid-century seat with a low frame, slim arms, one
    // loose seat cushion and one back cushion per person, and four splayed wooden legs. The seat faces
    // +Z, is centred on X and Z, and stands on y = 0.
    //
    // Slots: the frame (base, arms, back) has three shades (see MeshBuilder), the cushions three more,
    // and the legs one.
    public static class SeatingBuilder
    {
        public const int BodyBase = 0, CushionBase = 3, Leg = 6;
        public const int PaletteSize = 7;

        public static Color[] Palette(Color body, Color cushion, Color leg)
        {
            var palette = new Color[PaletteSize];
            MeshBuilder.SetBoxShades(palette, BodyBase, body);
            MeshBuilder.SetBoxShades(palette, CushionBase, cushion);
            palette[Leg] = leg;
            return palette;
        }

        public static MeshData Build(GraphicsDevice device, int seats, float armWidth, float armRise, float legHeight, float backCushionHeight)
        {
            const float seatWidth = 0.55f;       // one person
            const float depth = 0.75f;
            const float frameHeight = 0.12f;
            const float cushionHeight = 0.12f;
            const float backThickness = 0.16f;   // the frame's back panel
            const float backCushionThickness = 0.12f;
            const float gap = 0.03f;             // between neighbouring cushions

            var width = seats * seatWidth + 2f * armWidth;
            var innerWidth = width - 2f * armWidth;                  // between the arms
            var frameTop = legHeight + frameHeight;                  // top of the base frame = underside of the cushions
            var seatTop = frameTop + cushionHeight;
            var cushionDepth = depth - backThickness;

            var mesh = new MeshBuilder();

            // Base frame between the arms, sealed underneath
            mesh.AddBox(BodyBase, new Vector3(0f, legHeight, 0f), depth, innerWidth, frameHeight, sealBottom: true);

            // Arms, full depth, rising a little above the seat cushions
            var armHeight = seatTop + armRise - legHeight;
            foreach (var side in new[] { -1f, 1f })
                mesh.AddBox(BodyBase, new Vector3(side * (width / 2f - armWidth / 2f), legHeight, 0f), depth, armWidth, armHeight, sealBottom: true);

            // Back panel of the frame, between the arms
            var backZ = -depth / 2f + backThickness / 2f;
            mesh.AddBox(BodyBase, new Vector3(0f, frameTop, backZ), backThickness, innerWidth, 0.30f);

            // One seat cushion and one back cushion per person
            var cushionZ = depth / 2f - cushionDepth / 2f;
            var backCushionZ = -depth / 2f + backThickness + backCushionThickness / 2f;
            for (var i = 0; i < seats; i++)
            {
                var x = (i - (seats - 1) / 2f) * seatWidth;
                mesh.AddBox(CushionBase, new Vector3(x, frameTop, cushionZ), cushionDepth, seatWidth - gap, cushionHeight);
                mesh.AddBox(CushionBase, new Vector3(x, seatTop, backCushionZ), backCushionThickness, seatWidth - gap, backCushionHeight);
            }

            // Splayed legs, from under the frame's corners out to the floor
            var legX = width / 2f - 0.12f;
            var legZ = depth / 2f - 0.12f;
            foreach (var sx in new[] { -1f, 1f })
                foreach (var sz in new[] { -1f, 1f })
                    mesh.AddTube(
                        new Vector3(sx * legX, legHeight, sz * legZ),
                        new Vector3(sx * (legX + 0.05f), 0f, sz * (legZ + 0.05f)),
                        0.035f, 0.02f, 4, Leg);

            return mesh.Build(device);
        }
    }
}
