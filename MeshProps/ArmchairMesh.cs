using MeshCore.Library;
using MeshProps.Helpers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MeshProps
{
    // A one-seater armchair to go with the settee (see SeatingBuilder): 0.75 wide, with the settee's slim arms,
    // tall legs and tall back. It faces +Z.
    public static class ArmchairMesh
    {
        public static Color[] Palette(Color body, Color cushion, Color leg) => SeatingBuilder.Palette(body, cushion, leg);

        public static MeshData Build(GraphicsDevice device) =>
            SeatingBuilder.Build(device, seats: 1, armWidth: 0.10f, armRise: 0.12f, legHeight: 0.16f, backCushionHeight: 0.40f);
    }
}
