using MeshCore.Library;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MeshProps
{
    public enum CrateKind { Cardboard, Wood, Steel }

    // A box of any size, dressed as what it's made of, so you can guess how heavy it is before you push
    // it: cardboard (a strip of tape over the top), wooden planks (lines round the sides), or steel
    // (a cross braced on every side). Origin at the centre of its bottom face, like a Body.
    public static class CrateMesh
    {
        public static Color[] Palette(CrateKind kind)
        {
            var palette = new Color[3];
            MeshBuilder.SetBoxShades(palette, 0, kind switch
            {
                CrateKind.Cardboard => new Color(190, 150, 90),
                CrateKind.Wood => new Color(150, 100, 50),
                _ => new Color(110, 120, 130),
            });
            return palette;
        }

        public static string Key(CrateKind kind, Vector3 size) => $"crate:{kind}:{size.X:F2}x{size.Y:F2}x{size.Z:F2}";

        public static MeshSource Source(CrateKind kind, Vector3 size) => new MeshSource(Key(kind, size), d => Build(d, kind, size), Palette(kind));

        public static MeshData Build(GraphicsDevice device, CrateKind kind, Vector3 size)
        {
            var mesh = new MeshBuilder();
            mesh.AddBox(0, Vector3.Zero, size.Z, size.X, size.Y, sealBottom: true);

            var hx = size.X / 2f;
            var hz = size.Z / 2f;
            // The four sides as (one bottom corner, the other bottom corner), going round
            var sides = new[]
            {
                (new Vector3(-hx, 0f, -hz), new Vector3(hx, 0f, -hz)),
                (new Vector3(hx, 0f, -hz), new Vector3(hx, 0f, hz)),
                (new Vector3(hx, 0f, hz), new Vector3(-hx, 0f, hz)),
                (new Vector3(-hx, 0f, hz), new Vector3(-hx, 0f, -hz)),
            };
            var up = Vector3.Up * size.Y;

            switch (kind)
            {
                case CrateKind.Cardboard:
                    mesh.AddLine(new Vector3(-0.04f, size.Y, -hz), new Vector3(-0.04f, size.Y, hz));
                    mesh.AddLine(new Vector3(0.04f, size.Y, -hz), new Vector3(0.04f, size.Y, hz));
                    break;
                case CrateKind.Wood:
                    foreach (var t in new[] { 1f / 3f, 2f / 3f })
                        foreach (var (a, b) in sides)
                            mesh.AddLine(a + up * t, b + up * t);
                    break;
                case CrateKind.Steel:
                    foreach (var (a, b) in sides)
                    {
                        mesh.AddLine(a, b + up);
                        mesh.AddLine(b, a + up);
                    }
                    break;
            }
            return mesh.Build(device);
        }
    }
}
