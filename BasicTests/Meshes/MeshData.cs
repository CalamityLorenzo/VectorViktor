using Microsoft.Xna.Framework.Graphics;
using System;

namespace BasicTests.Meshes
{
    sealed class MeshData : IDisposable
    {
        // Buffer layouts are grouped by colour so each group is one contiguous draw range.
        // Solids (triangle list): top quad, bottom quad, then the four side quads.
        public const int SolidTopStart = 0;
        public const int SolidTopPrimitives = 2;
        public const int SolidBottomStart = 6;
        public const int SolidBottomPrimitives = 2;
        public const int SolidSidesStart = 12;
        public const int SolidSidesPrimitives = 8;

        // Edges (line list): top loop, then bottom loop + verticals.
        public const int EdgeTopStart = 0;
        public const int EdgeTopPrimitives = 4;
        public const int EdgeSidesStart = 8;
        public const int EdgeSidesPrimitives = 8;

        public VertexBuffer Edges;
        public VertexBuffer Solids;

        public void Dispose()
        {
            this.Edges.Dispose();
            this.Solids.Dispose();
        }
    }
}
