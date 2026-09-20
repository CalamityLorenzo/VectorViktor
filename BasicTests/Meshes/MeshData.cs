using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace BasicTests.Meshes
{
    struct MeshData
    {
        public Vector3[] Vertices;
        public VertexPositionColor[] EdgeVertex;
        public VertexBuffer EdgeBuffer;
        public VertexPositionColor[] SolidVertex;
        public VertexBuffer SolidBuffer;
    }
}
