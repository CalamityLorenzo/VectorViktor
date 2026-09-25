using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MeshCore.Library
{
    // Collects position-only solid triangles, each in a palette colour slot, and edge lines, then turns
    // them into a MeshData. Every slot's triangles, in whatever order they were added, go into one
    // unbroken run - one draw range, so one draw call - however many parts the mesh is built from.
    //
    // Boxes have three shades like the original VectorViktor box builder: Side (flanks),
    // Dim (nose + tail + optional bottom) and Top. A part's palette uses 3 consecutive slots
    // starting at its baseSlot, in that order.
    public sealed class MeshBuilder
    {
        public const int Side = 0, Dim = 1, Top = 2;

        private readonly List<List<VertexPosition>> _slots = new();   // each slot's triangles
        private readonly List<VertexPosition> _edges = new();
        private readonly List<(Vector3 A, Vector3 B, Vector3 C, Vector3 Inside)> _outlineTriangles = new();

        // Fills the 3 shade slots of a box part from one base colour, with the same formulas as the original.
        public static void SetBoxShades(Color[] palette, int baseSlot, Color color)
        {
            palette[baseSlot + Side] = color;
            palette[baseSlot + Dim] = new Color((int)(color.R * 0.55f), (int)(color.G * 0.55f), (int)(color.B * 0.55f));
            palette[baseSlot + Top] = Color.Lerp(color, Color.White, 0.35f);
        }

        // Axis-aligned box (forward = +Z, right = +X) whose position argument is the centre of its bottom face.
        // Adds 5 quads: 2 flanks (Side), nose + tail (Dim), top (Top); plus its 12 edges. With sealBottom the underside is a sixth
        // quad (Dim); leave it off where the underside is never visible.
        public void AddBox(int baseSlot, Vector3 bottomCenter, float length, float width, float height, bool sealBottom = false)
        {
            var hf = Vector3.UnitZ * (length * 0.5f);
            var hr = Vector3.UnitX * (width * 0.5f);
            var hu = Vector3.Up * height;

            var a = bottomCenter - hf - hr;
            var b = bottomCenter + hf - hr;
            var c = bottomCenter + hf + hr;
            var d = bottomCenter - hf + hr;
            var e = a + hu;
            var f = b + hu;
            var g = c + hu;
            var h = d + hu;

            AddQuad(baseSlot + Side, a, b, f, e);   // flank (-right)
            AddQuad(baseSlot + Side, c, d, h, g);   // flank (+right)
            AddQuad(baseSlot + Dim, b, c, g, f);    // nose (+forward)
            AddQuad(baseSlot + Dim, d, a, e, h);    // tail (-forward)
            if (sealBottom)
                AddQuad(baseSlot + Dim, a, b, c, d);   // bottom
            AddQuad(baseSlot + Top, e, f, g, h);    // top

            AddLine(a, b); AddLine(b, c); AddLine(c, d); AddLine(d, a);   // base
            AddLine(e, f); AddLine(f, g); AddLine(g, h); AddLine(h, e);   // top rim
            AddLine(a, e); AddLine(b, f); AddLine(c, g); AddLine(d, h);   // verticals
        }

        // An upright tapered prism (bottomRadius may differ from topRadius) with `sides` sides, standing
        // on bottomCentre. The side faces use sideSlot; a bottom or top cap is added only if its slot is >= 0.
        // Edges are the top and bottom rings; add verticalEdges for the lines joining them.
        public void AddFrustum(Vector3 bottomCentre, float bottomRadius, float topRadius, float height, int sides,
            int sideSlot, int bottomSlot = -1, int topSlot = -1, bool verticalEdges = false)
        {
            Vector3[] Ring(float y, float radius)
            {
                var ring = new Vector3[sides];
                for (var k = 0; k < sides; k++)
                {
                    var angle = k * MathHelper.TwoPi / sides;
                    ring[k] = new Vector3(bottomCentre.X + radius * MathF.Cos(angle), y, bottomCentre.Z + radius * MathF.Sin(angle));
                }
                return ring;
            }
            var bottom = Ring(bottomCentre.Y, bottomRadius);
            var top = Ring(bottomCentre.Y + height, topRadius);

            for (var k = 0; k < sides; k++)
                AddQuad(sideSlot, bottom[k], bottom[(k + 1) % sides], top[(k + 1) % sides], top[k]);
            if (bottomSlot >= 0)
                AddPolygon(bottomSlot, bottom);
            if (topSlot >= 0)
                AddPolygon(topSlot, top);

            AddLineLoop(bottom);
            AddLineLoop(top);
            if (verticalEdges)
                for (var k = 0; k < sides; k++)
                    AddLine(bottom[k], top[k]);
        }

        // A prism running from start to end in any direction (a branch, a pole), with `sides` sides and a
        // radius that can differ at each end. No end caps. Edges are the lines along its length only,
        // which is what outlines a thin branch in wireframe; add ringEdges for the ends as well.
        public void AddTube(Vector3 start, Vector3 end, float startRadius, float endRadius, int sides, int sideSlot, bool ringEdges = false)
        {
            var axis = Vector3.Normalize(end - start);
            var reference = MathF.Abs(axis.Y) < 0.9f ? Vector3.Up : Vector3.UnitX;
            var u = Vector3.Normalize(Vector3.Cross(axis, reference));
            var v = Vector3.Cross(axis, u);

            Vector3[] Ring(Vector3 centre, float radius)
            {
                var ring = new Vector3[sides];
                for (var k = 0; k < sides; k++)
                {
                    var angle = (k + 0.5f) * MathHelper.TwoPi / sides;
                    ring[k] = centre + radius * (MathF.Cos(angle) * u + MathF.Sin(angle) * v);
                }
                return ring;
            }
            var a = Ring(start, startRadius);
            var b = Ring(end, endRadius);

            for (var k = 0; k < sides; k++)
                AddQuad(sideSlot, a[k], a[(k + 1) % sides], b[(k + 1) % sides], b[k]);

            for (var k = 0; k < sides; k++)
                AddLine(a[k], b[k]);
            if (ringEdges)
            {
                AddLineLoop(a);
                AddLineLoop(b);
            }
        }

        // A triangle, drawn in the palette's colour `slot`.
        public void AddTri(int slot, Vector3 a, Vector3 b, Vector3 c)
        {
            while (_slots.Count <= slot)
                _slots.Add(new List<VertexPosition>());
            var solids = _slots[slot];
            solids.Add(new VertexPosition(a));
            solids.Add(new VertexPosition(b));
            solids.Add(new VertexPosition(c));
        }

        public void AddQuad(int slot, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            AddTri(slot, a, b, c);
            AddTri(slot, a, c, d);
        }

        // A convex polygon (points in order round its edge) as a triangle fan: points.Length - 2 triangles.
        public void AddPolygon(int slot, params Vector3[] points)
        {
            for (var i = 1; i < points.Length - 1; i++)
                AddTri(slot, points[0], points[i], points[i + 1]);
        }

        // A closed loop of lines round the given points, e.g. a polygon's outline.
        public void AddLineLoop(params Vector3[] points)
        {
            for (var i = 0; i < points.Length; i++)
                AddLine(points[i], points[(i + 1) % points.Length]);
        }

        public void AddLine(Vector3 a, Vector3 b)
        {
            _edges.Add(new VertexPosition(a));
            _edges.Add(new VertexPosition(b));
        }

        // A triangle of a closed, rounded surface (a canopy) whose outer contour is drawn, worked out afresh for each
        // view, instead of edges fixed on the mesh. This doesn't draw the triangle: add it with AddTri as well.
        // `inside` is a point inside that surface (a blob's centre), used to tell which way the triangle faces.
        public void AddOutlineTri(Vector3 a, Vector3 b, Vector3 c, Vector3 inside) =>
            _outlineTriangles.Add((a, b, c, inside));

        // The slots' triangles one after another, lowest slot first, each slot's one draw range. With
        // keepFootprint, the mesh also remembers its faces as seen from above (see MeshData.Covers).
        public MeshData Build(GraphicsDevice device, bool keepFootprint = false)
        {
            var solids = new List<VertexPosition>();
            var ranges = new List<DrawRange>();
            for (var slot = 0; slot < _slots.Count; slot++)
            {
                if (_slots[slot].Count == 0)
                    continue;
                ranges.Add(new DrawRange(solids.Count, _slots[slot].Count / 3, slot));
                solids.AddRange(_slots[slot]);
            }
            (Vector2, Vector2, Vector2)[]? footprint = null;
            if (keepFootprint)
            {
                footprint = new (Vector2, Vector2, Vector2)[solids.Count / 3];
                static Vector2 Plan(VertexPosition v) => new Vector2(v.Position.X, v.Position.Z);
                for (var t = 0; t < footprint.Length; t++)
                    footprint[t] = (Plan(solids[t * 3]), Plan(solids[t * 3 + 1]), Plan(solids[t * 3 + 2]));
            }
            return new MeshData(ToBuffer(device, solids), ranges.ToArray(), ToBuffer(device, _edges), Bounds(solids),
                _outlineTriangles.Count > 0 ? new OutlineData(_outlineTriangles) : null, footprint);
        }

        // Round everything added, faces and edges (the outline's triangles are faces too).
        private BoundingBox Bounds(List<VertexPosition> solids)
        {
            var min = new Vector3(float.MaxValue);
            var max = new Vector3(float.MinValue);
            foreach (var list in new[] { solids, _edges })
                foreach (var v in list)
                {
                    min = Vector3.Min(min, v.Position);
                    max = Vector3.Max(max, v.Position);
                }
            return min.X <= max.X ? new BoundingBox(min, max) : new BoundingBox(Vector3.Zero, Vector3.Zero);
        }

        // Null if there's nothing to put in it: a buffer can't be empty.
        private static VertexBuffer? ToBuffer(GraphicsDevice device, List<VertexPosition> vertices)
        {
            if (vertices.Count == 0)
                return null;
            var buffer = new VertexBuffer(device, typeof(VertexPosition), vertices.Count, BufferUsage.WriteOnly);
            buffer.SetData(vertices.ToArray());
            return buffer;
        }
    }
}
