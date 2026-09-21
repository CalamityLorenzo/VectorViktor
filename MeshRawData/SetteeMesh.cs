using MeshCore.Library;
using MeshRawData.Helpers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MeshRawData
{
    // A two-seater settee (see SeatingBuilder): 1.30 wide, slimmer arms, taller legs and a taller back than
    // the sofa, more of a 1950s feel. Together with the sofa there are five seats, enough for a family of four.
    // It faces +Z.
    public static class SetteeMesh
    {
        public static Color[] Palette(Color body, Color cushion, Color leg) => SeatingBuilder.Palette(body, cushion, leg);

        public static MeshData Build(GraphicsDevice device) =>
            SeatingBuilder.Build(device, seats: 2, armWidth: 0.10f, armRise: 0.12f, legHeight: 0.16f, backCushionHeight: 0.40f);
    }
}
