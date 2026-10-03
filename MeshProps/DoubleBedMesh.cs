using MeshCore.Library;
using MeshProps.Helpers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MeshProps
{
    // A made-up double bed (see BedBuilder): a 1.35 wide mattress, two pillows. 1.41 wide, 2.01 long with the headboard,
    // 0.92 to the top of its posts. The foot faces +Z.
    public static class DoubleBedMesh
    {
        public static Color[] Palette(Color wood, Color panel, Color linen, Color blanket, Color leg) => BedBuilder.Palette(wood, panel, linen, blanket, leg);

        public static MeshData Build(GraphicsDevice device) => BedBuilder.Build(device, mattressWidth: 1.35f, pillows: 2);
    }
}
