using Microsoft.Xna.Framework.Graphics;

namespace MeshCore.Library
{
    // Immutable, self-describing GPU mesh. Owned by the MeshCache; instances only borrow it.
    public sealed class MeshData : IDisposable
    {
        public VertexBuffer Solids { get; }
        // Line list, always drawn white; edges carry no colour data of their own.
        public VertexBuffer Edges { get; }
        public DrawRange[] SolidRanges { get; }
        // The view-dependent outline of a rounded surface (a canopy), drawn instead of edges stored in Edges; null for most meshes.
        public OutlineData? Outline { get; }

        // Highest ColorSlot used by any solid range + 1; a palette must have at least this many colours.
        public int PaletteSize { get; }

        public MeshData(VertexBuffer solids, DrawRange[] solidRanges, VertexBuffer edges, OutlineData? outline = null)
        {
            Solids = solids ?? throw new ArgumentNullException(nameof(solids));
            Edges = edges ?? throw new ArgumentNullException(nameof(edges));
            SolidRanges = solidRanges ?? throw new ArgumentNullException(nameof(solidRanges));
            Outline = outline;
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
