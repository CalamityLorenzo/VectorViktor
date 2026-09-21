using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MeshCore.Library
{
    // The outer contour of a closed, rounded surface (a tree canopy) as seen from one eye position.
    //
    // A fixed set of edges can't be the outline of a shape that turns, so this is worked out for each view:
    // the outline is the edges where the surface changes from facing the eye to facing away. Any of those with
    // other surface showing just beyond them (one blob of the canopy in front of another) is dropped, so what is
    // left is the outline of the whole shape and not of the blobs it is made from.
    //
    // This is shared by every instance of the mesh and holds no per-view state; an OutlineView (one per
    // instance) holds the result for the current view.
    public sealed class OutlineData
    {
        private readonly Vector3[] _cornerA, _cornerB, _cornerC;
        private readonly Vector3[] _normals;   // pointing out of the surface
        private readonly Edge[] _edges;
        private readonly Chunk[] _chunks;

        // The face fields index the triangle arrays; Face2 is -1 for an edge on the rim of an open surface.
        private readonly record struct Edge(Vector3 Start, Vector3 End, int Face1, Vector3 Opposite1, int Face2, Vector3 Opposite2);

        // A run of neighbouring triangles from one blob of the canopy, with a sphere round them, so a ray that
        // misses the sphere can skip all of them without testing each triangle.
        private readonly record struct Chunk(int Start, int Count, Vector3 Centre, float RadiusSquared);

        // Each outline edge is tested in this many pieces, so that where another part of the surface crosses in
        // front of it the outline stops at about the right place, rather than the whole edge being kept or lost.
        private const int Pieces = 8;

        // How many triangles go in each chunk. Bigger means fewer sphere tests but more triangles tested when a ray hits one.
        private const int ChunkSize = 4;

        // The most vertices GetOutline can return, i.e. how big a buffer it needs (worst case every other piece is kept).
        public int MaxVertices => _edges.Length * Pieces;

        // `inside` is any point inside the surface the triangle belongs to (a blob's centre). It is only used to
        // tell which way the triangle faces, so the winding of the corners doesn't matter.
        public OutlineData(IReadOnlyList<(Vector3 A, Vector3 B, Vector3 C, Vector3 Inside)> triangles)
        {
            var count = triangles.Count;

            // Corners that sit at the same place are one vertex, which is what lets neighbouring triangles share edges.
            var vertexIds = new Dictionary<(int, int, int), int>();
            int Id(Vector3 p)
            {
                var key = ((int)MathF.Round(p.X * 10000f), (int)MathF.Round(p.Y * 10000f), (int)MathF.Round(p.Z * 10000f));
                if (!vertexIds.TryGetValue(key, out var id))
                    vertexIds[key] = id = vertexIds.Count;
                return id;
            }
            var ids = new int[count * 3];
            for (var i = 0; i < count; i++)
            {
                ids[i * 3] = Id(triangles[i].A);
                ids[i * 3 + 1] = Id(triangles[i].B);
                ids[i * 3 + 2] = Id(triangles[i].C);
            }

            // Triangles that share a vertex are one blob. Group them so each blob is a contiguous run, then cut the runs into chunks.
            var parent = Enumerable.Range(0, vertexIds.Count).ToArray();
            int Find(int x)
            {
                while (parent[x] != x)
                    x = parent[x] = parent[parent[x]];
                return x;
            }
            for (var i = 0; i < count; i++)
            {
                parent[Find(ids[i * 3 + 1])] = Find(ids[i * 3]);
                parent[Find(ids[i * 3 + 2])] = Find(ids[i * 3]);
            }
            var blobOfRoot = new Dictionary<int, int>();
            var blobOf = new int[count];
            for (var i = 0; i < count; i++)
            {
                var root = Find(ids[i * 3]);
                if (!blobOfRoot.TryGetValue(root, out var blob))
                    blobOfRoot[root] = blob = blobOfRoot.Count;
                blobOf[i] = blob;
            }
            var order = Enumerable.Range(0, count).OrderBy(i => blobOf[i]).ToArray();   // stable

            _cornerA = new Vector3[count];
            _cornerB = new Vector3[count];
            _cornerC = new Vector3[count];
            _normals = new Vector3[count];
            for (var j = 0; j < count; j++)
            {
                var (a, b, c, _) = triangles[order[j]];
                _cornerA[j] = a;
                _cornerB[j] = b;
                _cornerC[j] = c;
            }
            var chunks = new List<Chunk>();
            for (var start = 0; start < count;)
            {
                var end = start;
                while (end < count && blobOf[order[end]] == blobOf[order[start]])
                    end++;
                for (var first = start; first < end; first += ChunkSize)
                    chunks.Add(MakeChunk(first, Math.Min(ChunkSize, end - first)));
                start = end;
            }
            _chunks = chunks.ToArray();

            var edges = new Dictionary<(int, int), EdgeBuilder>();
            void AddEdge(int fromId, Vector3 from, int toId, Vector3 to, Vector3 opposite, int face)
            {
                var key = (Math.Min(fromId, toId), Math.Max(fromId, toId));
                if (!edges.TryGetValue(key, out var edge))
                    edges[key] = edge = new EdgeBuilder(from, to);
                edge.AddFace(face, opposite);
            }

            for (var j = 0; j < count; j++)
            {
                var (a, b, c, inside) = triangles[order[j]];
                var normal = Vector3.Cross(b - a, c - a);
                if (normal.LengthSquared() < 1e-12f)
                {
                    _normals[j] = Vector3.Zero;   // degenerate: has no facing, never counts as an edge of anything
                    continue;
                }
                normal.Normalize();
                if (Vector3.Dot(normal, (a + b + c) / 3f - inside) < 0f)
                    normal = -normal;
                _normals[j] = normal;

                var ia = ids[order[j] * 3];
                var ib = ids[order[j] * 3 + 1];
                var ic = ids[order[j] * 3 + 2];
                AddEdge(ia, a, ib, b, c, j);
                AddEdge(ib, b, ic, c, a, j);
                AddEdge(ic, c, ia, a, b, j);
            }

            _edges = edges.Values.Select(e => e.ToEdge()).ToArray();

            Chunk MakeChunk(int start, int length)
            {
                var min = new Vector3(float.MaxValue);
                var max = new Vector3(float.MinValue);
                for (var k = start; k < start + length; k++)
                    foreach (var p in new[] { _cornerA[k], _cornerB[k], _cornerC[k] })
                    {
                        min = Vector3.Min(min, p);
                        max = Vector3.Max(max, p);
                    }
                var centre = (min + max) * 0.5f;
                var radius = 0f;
                for (var k = start; k < start + length; k++)
                    radius = MathF.Max(radius, MathF.Max(Vector3.Distance(centre, _cornerA[k]),
                        MathF.Max(Vector3.Distance(centre, _cornerB[k]), Vector3.Distance(centre, _cornerC[k]))));
                radius = radius * 1.001f + 1e-4f;   // a little slack, so a ray just grazing the sphere isn't skipped by rounding
                return new Chunk(start, length, centre, radius * radius);
            }
        }

        // A place to keep the outline for one instance's current view. It recalculates only when the eye has
        // moved relative to the mesh, so an instance that isn't turning under a still camera costs nothing.
        public OutlineView CreateView() => new OutlineView(this);

        // Fills `lines` with the outline as seen from `eye` (in the surface's own space), as a line list, and
        // returns how many vertices it wrote (always even). `lines` must hold at least MaxVertices.
        public int GetOutline(Vector3 eye, VertexPosition[] lines)
        {
            var written = 0;
            foreach (var edge in _edges)
            {
                var facesEye1 = Vector3.Dot(_normals[edge.Face1], eye - edge.Start) > 0f;
                var facesEye2 = edge.Face2 >= 0 && Vector3.Dot(_normals[edge.Face2], eye - edge.Start) > 0f;
                if (facesEye1 == facesEye2)
                    continue;   // both towards the eye or both away: a fold in the middle of the surface, not its edge

                // The direction just past the edge, away from the face the eye can see (that face and the one
                // behind it are folded over onto the same side of the edge).
                var opposite = facesEye1 ? edge.Opposite1 : edge.Opposite2;
                var along = edge.End - edge.Start;
                var length = along.Length();
                along /= length;
                var outward = (edge.Start + edge.End) * 0.5f - opposite;
                outward -= along * Vector3.Dot(outward, along);
                if (outward.LengthSquared() < 1e-12f)
                    continue;
                outward.Normalize();

                // Keep the pieces the eye can see straight past: if it can't, that piece is inside the outline of
                // the whole shape. Runs of kept pieces are joined into one line.
                var step = along * (length / Pieces);
                var nudge = outward * (length * 0.03f);
                var runStart = -1;
                for (var piece = 0; piece <= Pieces; piece++)
                {
                    var isKept = piece < Pieces
                        && !Blocked(eye, edge.Start + step * (piece + 0.5f) + nudge - eye, edge.Face1, edge.Face2);
                    if (isKept && runStart < 0)
                        runStart = piece;
                    else if (!isKept && runStart >= 0)
                    {
                        lines[written++] = new VertexPosition(edge.Start + step * runStart);
                        lines[written++] = new VertexPosition(edge.Start + step * piece);
                        runStart = -1;
                    }
                }
            }
            return written;
        }

        private bool Blocked(Vector3 origin, Vector3 direction, int ignoreFace1, int ignoreFace2)
        {
            foreach (var chunk in _chunks)
            {
                if (!MayHit(origin, direction, chunk))
                    continue;
                for (var i = chunk.Start; i < chunk.Start + chunk.Count; i++)
                {
                    if (i == ignoreFace1 || i == ignoreFace2)
                        continue;
                    if (RayHitsTriangle(origin, direction, _cornerA[i], _cornerB[i], _cornerC[i]))
                        return true;
                }
            }
            return false;
        }

        // Whether the ray (forward from the origin) passes through the chunk's sphere.
        private static bool MayHit(Vector3 origin, Vector3 direction, Chunk chunk)
        {
            var lengthSquared = direction.LengthSquared();
            if (lengthSquared < 1e-12f)
                return true;
            var toCentre = chunk.Centre - origin;
            var along = MathF.Max(Vector3.Dot(toCentre, direction) / lengthSquared, 0f);
            return (toCentre - direction * along).LengthSquared() <= chunk.RadiusSquared;
        }

        // Moller-Trumbore, counting a hit anywhere in front of the origin and from either side of the triangle.
        private static bool RayHitsTriangle(Vector3 origin, Vector3 direction, Vector3 a, Vector3 b, Vector3 c)
        {
            var edge1 = b - a;
            var edge2 = c - a;
            var p = Vector3.Cross(direction, edge2);
            var determinant = Vector3.Dot(edge1, p);
            if (MathF.Abs(determinant) < 1e-9f)
                return false;
            var inverse = 1f / determinant;
            var toOrigin = origin - a;
            var u = Vector3.Dot(toOrigin, p) * inverse;
            if (u < 0f || u > 1f)
                return false;
            var q = Vector3.Cross(toOrigin, edge1);
            var v = Vector3.Dot(direction, q) * inverse;
            if (v < 0f || u + v > 1f)
                return false;
            return Vector3.Dot(edge2, q) * inverse > 1e-4f;
        }

        private sealed class EdgeBuilder
        {
            private readonly Vector3 _start, _end;
            private int _face1 = -1, _face2 = -1;
            private Vector3 _opposite1, _opposite2;

            public EdgeBuilder(Vector3 start, Vector3 end)
            {
                _start = start;
                _end = end;
            }

            public void AddFace(int face, Vector3 opposite)
            {
                if (_face1 < 0) { _face1 = face; _opposite1 = opposite; }
                else if (_face2 < 0) { _face2 = face; _opposite2 = opposite; }
                // A third face would mean the surface isn't closed and simple; the first two are kept.
            }

            public Edge ToEdge() => new Edge(_start, _end, _face1, _opposite1, _face2, _opposite2);
        }
    }

    // One instance's outline for its current view (see OutlineData.CreateView).
    public sealed class OutlineView
    {
        // Views closer than this (in the mesh's own space) count as unmoved.
        private const float SameViewDistanceSquared = 1e-10f;

        private readonly OutlineData _data;
        private Vector3 _eye;
        private bool _valid;

        // The outline as a line list, valid up to VertexCount.
        public VertexPosition[] Vertices { get; }
        public int VertexCount { get; private set; }

        internal OutlineView(OutlineData data)
        {
            _data = data;
            Vertices = new VertexPosition[data.MaxVertices];
        }

        // Brings the outline up to date for an eye at this position (in the mesh's own space).
        public void Update(Vector3 eye)
        {
            if (_valid && Vector3.DistanceSquared(eye, _eye) < SameViewDistanceSquared)
                return;
            VertexCount = _data.GetOutline(eye, Vertices);
            _eye = eye;
            _valid = true;
        }
    }
}
