using MeshCore.Library;
using MeshRawData.Helpers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MeshRawData
{
    // A three-seater sofa (see SeatingBuilder): 1.97 wide, chunky arms, a high back. It faces +Z.
    public static class SofaMesh
    {
        public static Color[] Palette(Color body, Color cushion, Color leg) => SeatingBuilder.Palette(body, cushion, leg);

        public static MeshData Build(GraphicsDevice device) =>
            SeatingBuilder.Build(device, seats: 3, armWidth: 0.16f, armRise: 0.18f, legHeight: 0.10f, backCushionHeight: 0.34f);
    }
}
