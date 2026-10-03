using MeshCore.Library;
using MeshProps.Helpers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MeshProps
{
    // A made-up single bed (see BedBuilder): a 0.90 wide mattress, one pillow. 0.96 wide, 2.01 long with the headboard,
    // 0.92 to the top of its posts. The foot faces +Z.
    public static class SingleBedMesh
    {
        public static Color[] Palette(Color wood, Color panel, Color linen, Color blanket, Color leg) => BedBuilder.Palette(wood, panel, linen, blanket, leg);

        public static MeshData Build(GraphicsDevice device) => BedBuilder.Build(device, mattressWidth: 0.90f, pillows: 1);
    }
}
