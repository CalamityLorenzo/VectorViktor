using MeshCore.Library;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MeshProps
{
    // Square-based pyramid: a deliberately different shape from the ingot (5 corners,
    // 18/16 vertices, 2 palette slots, 2 solid ranges) to prove MeshData/MeshCache/MeshInstance are generic.
    public static class PyramidMesh
    {
        public const int Base = 0, Side = 1;

        public static Color[] Palette(Color baseColor, Color side) => new[] { baseColor, side };

        public static MeshData Build(GraphicsDevice device)
        {
            var b0 = new Vector3(-0.5f, -0.4f, -0.5f);
            var b1 = new Vector3(0.5f, -0.4f, -0.5f);
            var b2 = new Vector3(0.5f, -0.4f, 0.5f);
            var b3 = new Vector3(-0.5f, -0.4f, 0.5f);
            var apex = new Vector3(0f, 0.6f, 0f);

            var mesh = new MeshBuilder();
            mesh.AddQuad(Base, b0, b1, b2, b3);   // the base (2 triangles)
            mesh.AddTri(Side, b0, b1, apex);      // four sloped sides
            mesh.AddTri(Side, b1, b2, apex);
            mesh.AddTri(Side, b2, b3, apex);
            mesh.AddTri(Side, b3, b0, apex);
            mesh.AddLineLoop(b0, b1, b2, b3);     // base loop
            mesh.AddLine(b0, apex);               // apex edges
            mesh.AddLine(b1, apex);
            mesh.AddLine(b2, apex);
            mesh.AddLine(b3, apex);
            return mesh.Build(device);
        }
    }
}
