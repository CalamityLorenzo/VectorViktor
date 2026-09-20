using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace BasicTests.Meshes
{
    internal class IngotFrustrumMeshBuilder :IDisposable
    {
        private Vector3[] _rawMeshData;
        private Dictionary<(Color, Color, Color), MeshData> _meshDataCache = new Dictionary<(Color, Color, Color), MeshData>();
        private bool disposedValue;

        public IngotFrustrumMeshBuilder(Vector3[] vertices)
        {
            _rawMeshData = vertices;
        }

        public MeshData Build(GraphicsDevice device, Color TopColor, Color SideColor, Color OtherColor)
        {
            var key = (TopColor,SideColor,OtherColor);
            if (_meshDataCache.TryGetValue(key, out var cachedMeshData))
            {
                return cachedMeshData;
            }
            else
            {
                var meshData = BuildMeshData(device, TopColor, SideColor, OtherColor);
                _meshDataCache[key] = meshData;
                return meshData;
            }
        }

        private MeshData BuildMeshData(GraphicsDevice device, Color topColor, Color sideColor, Color otherColor)
        {
            var edges = BuildEdges(_rawMeshData, topColor, sideColor, otherColor);
            var solid = BuildSolid(_rawMeshData, topColor, sideColor, otherColor);
            var solidVertex = new VertexBuffer(device, typeof(VertexPositionColor), solid.Length, BufferUsage.WriteOnly);
            solidVertex.SetData(solid);
            var edgeVertex = new VertexBuffer(device, typeof(VertexPositionColor), edges.Length, BufferUsage.WriteOnly);
            edgeVertex.SetData(edges);
            return new MeshData
            {
                Vertices = _rawMeshData,
                EdgeVertex = edges,
                EdgeBuffer = edgeVertex,
                SolidVertex = solid,
                SolidBuffer = solidVertex
            };
        }
        private static VertexPositionColor[] BuildEdges(Vector3[] RawMeshData, Color TopColor, Color SideColor, Color OtherColor)
        {
            var vertices = new VertexPositionColor[24];
            var index = 0;
            for (var i = 0; i < 4; i++)
            {
                var next = (i + 1) % 4;
                AddLine(vertices, RawMeshData[4 + i], RawMeshData[4 + next], TopColor, ref index);  // top loop
                AddLine(vertices, RawMeshData[i], RawMeshData[next], SideColor, ref index);         // bottom loop
                AddLine(vertices, RawMeshData[i], RawMeshData[4 + i], SideColor, ref index);        // vertical edge
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

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects)
                    foreach(var obj in this._meshDataCache)
                    {
                        obj.Value.EdgeBuffer.Dispose();
                        obj.Value.SolidBuffer.Dispose();
                    }
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                disposedValue = true;
            }
        }

        // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
        ~IngotFrustrumMeshBuilder()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: false);
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
