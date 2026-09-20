using Microsoft.Xna.Framework.Graphics;
using System;

namespace BasicTests.Meshes
{
    sealed class MeshData : IDisposable
    {
        public VertexBuffer Edges;
        public VertexBuffer Solids;

        public void Dispose()
        {
            this.Edges.Dispose();
            this.Solids.Dispose();
        }
    }
}
