using Microsoft.Xna.Framework.Graphics;
using System;
using System.Linq;

namespace BasicTests.Meshes
{
    // Immutable, self-describing GPU mesh. Owned by the MeshCache; instances only borrow it.
    sealed class MeshData : IDisposable
    {
        public VertexBuffer Solids { get; }
        // Line list, always drawn white; edges carry no colour data of their own.
        public VertexBuffer Edges { get; }
        public DrawRange[] SolidRanges { get; }

        // Highest ColorSlot used by any solid range + 1; a palette must have at least this many colours.
        public int PaletteSize { get; }

        public MeshData(VertexBuffer solids, DrawRange[] solidRanges, VertexBuffer edges)
        {
            Solids = solids ?? throw new ArgumentNullException(nameof(solids));
            Edges = edges ?? throw new ArgumentNullException(nameof(edges));
            SolidRanges = solidRanges ?? throw new ArgumentNullException(nameof(solidRanges));
            PaletteSize = SolidRanges.Select(r => r.ColorSlot + 1).DefaultIfEmpty(0).Max();

            // The ranges must tile the solids buffer exactly, or triangles are silently never drawn (or overrun the buffer).
            var covered = 0;
            foreach (var range in SolidRanges)
            {
                if (range.Start != covered)
                    throw new ArgumentException($"Solid range starting at vertex {range.Start} does not follow on from the previous one (expected {covered}).", nameof(solidRanges));
                covered += range.Primitives * 3;
            }
            if (covered != solids.VertexCount)
                throw new ArgumentException($"Solid ranges cover {covered} vertices but the buffer has {solids.VertexCount}.", nameof(solidRanges));
        }

        public void Dispose()
        {
            Solids?.Dispose();
            Edges?.Dispose();
        }
    }
}
