using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Runtime.InteropServices;

namespace MeshCore.Library
{
    // A face's corner: where it is, and which slot of the palette its face is coloured from. With the slot on every
    // vertex, a mesh's faces are drawn in one go whatever colours they are (see MeshRendering's PaletteEffect), not a
    // draw for each colour.
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public readonly struct VertexPositionSlot : IVertexType
    {
        public readonly Vector3 Position;
        public readonly float Slot;

        public VertexPositionSlot(Vector3 position, int slot)
        {
            Position = position;
            Slot = slot;
        }

        public static readonly VertexDeclaration VertexDeclaration = new VertexDeclaration(
            new VertexElement(0, VertexElementFormat.Vector3, VertexElementUsage.Position, 0),
            new VertexElement(12, VertexElementFormat.Single, VertexElementUsage.TextureCoordinate, 0));

        VertexDeclaration IVertexType.VertexDeclaration => VertexDeclaration;
    }
}
