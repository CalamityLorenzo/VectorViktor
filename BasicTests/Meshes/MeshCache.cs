using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections;
using System.Collections.Generic;

namespace BasicTests.Meshes
{
    internal class MeshCache : IDisposable, IEnumerable<MeshData>
    {
        private Vector3[] _rawMeshData;
        private Dictionary<string, MeshData> _meshDataCache = new ();

        public MeshCache(Vector3[] vertices)
        {
            _rawMeshData = vertices;
        }

        public MeshData BuildIngot(GraphicsDevice device)
        {
            var key= "ingot";
            if (_meshDataCache.TryGetValue(key, out var cachedMeshData))
            {
                return cachedMeshData;
            }
            else
            {
                var meshData = BuildMeshData(device);
                _meshDataCache[key] = meshData;
                return meshData;
            }
        }

        private MeshData BuildMeshData(GraphicsDevice device)
        {
            var edges = BuildEdges(_rawMeshData);
            var solid = BuildSolid(_rawMeshData);
            var solidVertex = new VertexBuffer(device, typeof(VertexPosition), solid.Length, BufferUsage.WriteOnly);
            solidVertex.SetData(solid);
            var edgeVertex = new VertexBuffer(device, typeof(VertexPosition), edges.Length, BufferUsage.WriteOnly);
            edgeVertex.SetData(edges);
            return new MeshData
            {
                Edges = edgeVertex,
                Solids = solidVertex
            };
        }
        private static VertexPosition[] BuildEdges(Vector3[] RawMeshData)
        {
            var vertices = new VertexPosition[24];
            var index = 0;
            // Grouped by colour: see MeshData.Edge* ranges.
            for (var i = 0; i < 4; i++)
                AddLine(vertices, RawMeshData[4 + i], RawMeshData[4 + (i + 1) % 4], ref index);  // top loop
            for (var i = 0; i < 4; i++)
                AddLine(vertices, RawMeshData[i], RawMeshData[(i + 1) % 4], ref index);          // bottom loop
            for (var i = 0; i < 4; i++)
                AddLine(vertices, RawMeshData[i], RawMeshData[4 + i], ref index);                // vertical edges

            return vertices;
        }

        private static VertexPosition[] BuildSolid(Vector3[] RawMeshData)
        {
            var b0 = RawMeshData[0];
            var b1 = RawMeshData[1];
            var b2 = RawMeshData[2];
            var b3 = RawMeshData[3];
            var t0 = RawMeshData[4];
            var t1 = RawMeshData[5];
            var t2 = RawMeshData[6];
            var t3 = RawMeshData[7];

            var vertices = new VertexPosition[36];
            var index = 0;
            AddQuad(vertices, t0, t1, t2, t3, ref index);   // top
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
        private static void AddQuad(VertexPosition[] vertices, Vector3 a, Vector3 b, Vector3 c, Vector3 d,  ref int index)
        {
            vertices[index++] = new VertexPosition(a);
            vertices[index++] = new VertexPosition(b);
            vertices[index++] = new VertexPosition(c);

            vertices[index++] = new VertexPosition(a);
            vertices[index++] = new VertexPosition(c);
            vertices[index++] = new VertexPosition(d);
        }

        public void Dispose()
        {
            foreach (var meshData in _meshDataCache.Values)
            {
                meshData.Dispose();
            }
            _meshDataCache.Clear();
        }

        public IEnumerator<MeshData> GetEnumerator()
        {
            return _meshDataCache.Values.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
