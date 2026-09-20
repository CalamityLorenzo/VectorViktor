using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace BasicTests
{
    struct MeshData
    {
        public Vector3[] Vertices;
        public MeshData(Vector3[] vertices, Color topColor, Color sideColor, Color otherColor)
        {
            Vertices = vertices;
            VertexPositionColor[] EdgeVertex;
            VertexBuffer EdgeBuffer;

            VertexPositionColor[] SolidVertex;
            VertexBuffer SolidBuffer;
        }
    }

    internal class MeshBuilders
    {


        public Vector3[] RawMeshData { get; } = rawMeshData;
        public Color TopColor { get; } = TopColor;
        public Color SideColor { get; } = SideColor;
        public Color OtherColor { get; } = OtherColor;

        public VertexPositionColor[] BuildEdges() { return BuildEdges(RawMeshData, TopColor, SideColor, OtherColor); }
        public VertexPositionColor[] BuildSolid() { return BuildSolid(RawMeshData, TopColor, SideColor, OtherColor); }

        private static VertexPositionColor[] BuildEdges(Vector3[] RawMeshData, Color TopColor, Color SideColor, Color OtherColor)
        {
            var vertices = new VertexPositionColor[24];
            var index = 0;
            for (var i = 0; i < 4; i++)
            {
                var next = (i + 1) % 4;
                AddLine(vertices, RawMeshData[4 + i], RawMeshData[4 + next], TopColor, ref index);  // top loop
                AddLine(vertices, RawMeshData[i], RawMeshData[next], SideColor, ref index);         // bottom loop
                AddLine(vertices, RawMeshData[i], RawMeshData   [4 + i], SideColor, ref index);        // vertical edge
            }

            return vertices;
        }

        private static VertexPositionColor[] BuildSolid(Vector3[] RawMeshData, Color TopColor, Color SideColor, Color OtherColor)
        {
            var b0 = RawMeshData[0];
            var b1 = RawMeshData[1];
            var b2 = RawMeshData[2];
            var b3 = RawMeshData[3];
            var t0 = RawMeshData[4];
            var t1 = RawMeshData[5];
            var t2 = RawMeshData[6];
            var t3 = RawMeshData[7];

            var vertices = new VertexPositionColor[36];
            var index = 0;
            AddQuad(vertices, t0, t1, t2, t3, TopColor, ref index);   // top
            AddQuad(vertices, b1, b0, b3, b2, OtherColor, ref index);  // bottom
            AddQuad(vertices, b0, b1, t1, t0, SideColor, ref index);  // back
            AddQuad(vertices, b2, b3, t3, t2, SideColor, ref index);  // front
            AddQuad(vertices, b3, b0, t0, t3, SideColor, ref index);  // left
            AddQuad(vertices, b1, b2, t2, t1, SideColor, ref index);  // right
            return vertices;
        }

        private static void AddLine(VertexPositionColor[] vertices, Vector3 a, Vector3 b, Color color, ref int index)
        {
            vertices[index++] = new VertexPositionColor(a, color);
            vertices[index++] = new VertexPositionColor(b, color);
        }
        private static void AddQuad(VertexPositionColor[] vertices, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color color, ref int index)
        {
            vertices[index++] = new VertexPositionColor(a, color);
            vertices[index++] = new VertexPositionColor(b, color);
            vertices[index++] = new VertexPositionColor(c, color);

            vertices[index++] = new VertexPositionColor(a, color);
            vertices[index++] = new VertexPositionColor(c, color);
            vertices[index++] = new VertexPositionColor(d, color);
        }

    }
}
