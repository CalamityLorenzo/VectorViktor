using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MeshCore.Library
{
    // Immutable, self-describing GPU mesh. Owned by the MeshCache (or whatever built it); instances only borrow it.
    //
    // A headless one (see MeshBuilder.Build with no device) has no GPU buffers, only the vertices they'd have held:
    // it can be looked at, in a test, but not drawn.
    public sealed class MeshData : IDisposable
    {
        // Triangle list, coloured a range at a time (see SolidRanges); null if the mesh has no faces.
        public VertexBuffer? Solids { get; }
        // Line list, always drawn white; edges carry no colour data of their own. Null if the mesh has no edges.
        public VertexBuffer? Edges { get; }
        public DrawRange[] SolidRanges { get; }
        // The view-dependent outline of a rounded surface (a canopy), drawn instead of edges stored in Edges; null for most meshes.
        public OutlineData? Outline { get; }

        // How many vertices the faces and the edges (two to a line) have.
        public int SolidVertexCount { get; }
        public int EdgeVertexCount { get; }

        // The vertices themselves, in their buffers' order, if it's headless: null otherwise, when they're in the buffers.
        public IReadOnlyList<VertexPosition>? HeadlessSolids { get; }
        public IReadOnlyList<VertexPosition>? HeadlessEdges { get; }
        public bool IsHeadless => HeadlessSolids != null;

        // Highest ColorSlot used by any solid range + 1; a palette must have at least this many colours.
        public int PaletteSize { get; }

        // The box round every vertex, faces and edges alike, in the mesh's own space: for culling it.
        public BoundingBox Bounds { get; }

        // Its buffers have been given back: drawing it now is a mistake (see MeshInstance).
        public bool IsDisposed { get; private set; }

        // Its faces as seen from above, in its own (X, Z), if it was built to keep them (see MeshBuilder.Build):
        // for asking what ground it covers.
        private readonly (Vector2 a, Vector2 b, Vector2 c)[]? _footprint;

        public MeshData(VertexBuffer? solids, DrawRange[] solidRanges, VertexBuffer? edges, BoundingBox bounds, OutlineData? outline = null,
                        (Vector2 a, Vector2 b, Vector2 c)[]? footprint = null)
            : this(solids, edges, solids?.VertexCount ?? 0, edges?.VertexCount ?? 0, null, null, solidRanges, bounds, outline, footprint)
        {
        }

        // A mesh with no buffers, only what would go in them.
        public static MeshData Headless(VertexPosition[] solids, DrawRange[] solidRanges, VertexPosition[] edges, BoundingBox bounds,
                                        OutlineData? outline = null, (Vector2 a, Vector2 b, Vector2 c)[]? footprint = null) =>
            new MeshData(null, null, solids.Length, edges.Length, solids, edges, solidRanges, bounds, outline, footprint);

        private MeshData(VertexBuffer? solids, VertexBuffer? edges, int solidVertices, int edgeVertices,
                         IReadOnlyList<VertexPosition>? headlessSolids, IReadOnlyList<VertexPosition>? headlessEdges,
                         DrawRange[] solidRanges, BoundingBox bounds, OutlineData? outline, (Vector2 a, Vector2 b, Vector2 c)[]? footprint)
        {
            _footprint = footprint;
            Solids = solids;
            Edges = edges;
            SolidVertexCount = solidVertices;
            EdgeVertexCount = edgeVertices;
            HeadlessSolids = headlessSolids;
            HeadlessEdges = headlessEdges;
            SolidRanges = solidRanges ?? throw new ArgumentNullException(nameof(solidRanges));
            Bounds = bounds;
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
            if (covered != solidVertices)
                throw new ArgumentException($"Solid ranges cover {covered} vertices but the buffer has {solidVertices}.", nameof(solidRanges));
        }

        // Whether any face lies over (x, z), in the mesh's own space. Only for a mesh built to keep its footprint.
        public bool Covers(float x, float z)
        {
            if (_footprint == null)
                throw new InvalidOperationException("This mesh wasn't built to keep its footprint (see MeshBuilder.Build).");
            var p = new Vector2(x, z);
            foreach (var (a, b, c) in _footprint)
            {
                float Side(Vector2 u, Vector2 v) => (p.X - v.X) * (u.Y - v.Y) - (u.X - v.X) * (p.Y - v.Y);
                var d1 = Side(a, b);
                var d2 = Side(b, c);
                var d3 = Side(c, a);
                if (!((d1 < 0f || d2 < 0f || d3 < 0f) && (d1 > 0f || d2 > 0f || d3 > 0f)))
                    return true;
            }
            return false;
        }

        public void Dispose()
        {
            Solids?.Dispose();
            Edges?.Dispose();
            IsDisposed = true;
        }
    }
}
