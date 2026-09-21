using MeshCore.Library;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MeshRawData
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

            var solid = new[]
            {
                // base quad (2 triangles)
                b0, b1, b2,  b0, b2, b3,
                // four sloped sides
                b0, b1, apex,  b1, b2, apex,  b2, b3, apex,  b3, b0, apex,
            };
            var edges = new[]
            {
                b0, b1,  b1, b2,  b2, b3,  b3, b0,       // base loop
                b0, apex,  b1, apex,  b2, apex,  b3, apex, // apex edges
            };

            return new MeshData(
                ToBuffer(device, solid),
                new[]
                {
                    new DrawRange(0, 2, Base),
                    new DrawRange(6, 4, Side),
                },
                ToBuffer(device, edges));
        }

        private static VertexBuffer ToBuffer(GraphicsDevice device, Vector3[] positions)
        {
            var vertices = new VertexPosition[positions.Length];
            for (var i = 0; i < positions.Length; i++)
                vertices[i] = new VertexPosition(positions[i]);
            var buffer = new VertexBuffer(device, typeof(VertexPosition), vertices.Length, BufferUsage.WriteOnly);
            buffer.SetData(vertices);
            return buffer;
        }
    }
}
