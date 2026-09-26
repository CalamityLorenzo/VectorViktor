using MeshCore.Library;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MeshProps
{
    // A low eight-sided turntable, 1 across, to stand something on and turn it round: spokes across its top
    // so you can see it going round, even in wireframe. Built with its base on y = 0; Height is where its top is.
    public static class TurntableMesh
    {
        public const int Side = 0, Top = 1;
        public const int PaletteSize = 2;

        public const float Radius = 0.5f, Height = 0.08f;
        private const int Sides = 8;

        public static Color[] Palette(Color side, Color top) => new[] { side, top };

        public static MeshData Build(GraphicsDevice device)
        {
            var mesh = new MeshBuilder();
            mesh.AddFrustum(Vector3.Zero, Radius, Radius * 0.95f, Height, Sides, Side, topSlot: Top, verticalEdges: true);

            var middle = Vector3.Up * Height;
            for (var k = 0; k < Sides; k += 2)
            {
                var angle = k * MathHelper.TwoPi / Sides;
                mesh.AddLine(middle, middle + new Vector3(MathF.Cos(angle), 0f, MathF.Sin(angle)) * (Radius * 0.95f));
            }
            return mesh.Build(device);
        }
    }
}
