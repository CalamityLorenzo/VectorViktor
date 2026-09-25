using MeshCore.Library;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MeshProps
{
    public static class IngotMesh
    {
        // Palette slots. Solid ranges below are grouped by colour so each is one contiguous draw.
        public const int Top = 0, Side = 1, Other = 2;

        public static Color[] Palette(Color top, Color side, Color other) => new[] { top, side, other };

        // Imagine we imported this from a 3D model, but we can also just define it manually. The ingot is a frustum (truncated pyramid) shape.
        // 0-3: bottom (larger) rectangle. 4-7: top (smaller) rectangle, inset on both
        // axes so the sides slope inward.
        private static readonly Vector3[] Corners =
        {
            new Vector3(-0.6f, -0.3f, -0.3f),
            new Vector3(0.6f, -0.3f, -0.3f),
            new Vector3(0.6f, -0.3f, 0.3f),

            new Vector3(-0.6f, -0.3f, 0.3f),
            new Vector3(-0.4f, 0.3f, -0.2f),
            new Vector3(0.4f, 0.3f, -0.2f),
            new Vector3(0.4f, 0.3f, 0.2f),
            
            new Vector3(-0.4f, 0.3f, 0.2f),
        };

        public static MeshData Build(GraphicsDevice device)
        {
            var raw = Corners;
            var solid = BuildSolid(raw);
            var edges = BuildEdges(raw);

            var solidBuffer = new VertexBuffer(device, typeof(VertexPosition), solid.Length, BufferUsage.WriteOnly);
            solidBuffer.SetData(solid);
            var edgeBuffer = new VertexBuffer(device, typeof(VertexPosition), edges.Length, BufferUsage.WriteOnly);
            edgeBuffer.SetData(edges);

            return new MeshData(
                solidBuffer,
                new[]
                {
                    new DrawRange(0, 2, Top),     // top quad
                    new DrawRange(6, 2, Other),   // bottom quad
                    new DrawRange(12, 8, Side),   // four side quads
                },
                edgeBuffer,
                BoundingBox.CreateFromPoints(raw));
        }

        private static VertexPosition[] BuildEdges(Vector3[] raw)
        {
            var vertices = new VertexPosition[24];
            var index = 0;
            for (var i = 0; i < 4; i++)
                AddLine(vertices, raw[4 + i], raw[4 + (i + 1) % 4], ref index);  // top loop
            for (var i = 0; i < 4; i++)
                AddLine(vertices, raw[i], raw[(i + 1) % 4], ref index);          // bottom loop
            for (var i = 0; i < 4; i++)
                AddLine(vertices, raw[i], raw[4 + i], ref index);                // vertical edges
            return vertices;
        }

        private static VertexPosition[] BuildSolid(Vector3[] raw)
        {
            var b0 = raw[0];
            var b1 = raw[1];
            var b2 = raw[2];
            var b3 = raw[3];
            var t0 = raw[4];
            var t1 = raw[5];
            var t2 = raw[6];
            var t3 = raw[7];

            var vertices = new VertexPosition[36];
            var index = 0;
            AddQuad(vertices, t0, t1, t2, t3, ref index);  // top
            AddQuad(vertices, b1, b0, b3, b2, ref index);  // bottom
            AddQuad(vertices, b0, b1, t1, t0, ref index);  // back
            AddQuad(vertices, b2, b3, t3, t2, ref index);  // front
            AddQuad(vertices, b3, b0, t0, t3, ref index);  // left
            AddQuad(vertices, b1, b2, t2, t1, ref index);  // right
            return vertices;
        }

        private static void AddLine(VertexPosition[] vertices, Vector3 a, Vector3 b, ref int index)
        {
            vertices[index++] = new VertexPosition(a);
            vertices[index++] = new VertexPosition(b);
        }

        private static void AddQuad(VertexPosition[] vertices, Vector3 a, Vector3 b, Vector3 c, Vector3 d, ref int index)
        {
            vertices[index++] = new VertexPosition(a);
            vertices[index++] = new VertexPosition(b);
            vertices[index++] = new VertexPosition(c);

            vertices[index++] = new VertexPosition(a);
            vertices[index++] = new VertexPosition(c);
            vertices[index++] = new VertexPosition(d);
        }
    }
}
