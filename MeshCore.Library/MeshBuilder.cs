using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MeshCore.Library
{
    // Collects position-only solid triangles (with colour-slot draw ranges) and edge lines, then
    // turns them into a MeshData. Shared by meshes that are assembled from boxes and extra faces.
    //
    // Boxes have three shades like the original VectorViktor box builder: Side (flanks),
    // Dim (nose + tail + optional bottom) and Top. A part's palette uses 3 consecutive slots
    // starting at its baseSlot, in that order.
    public sealed class MeshBuilder
    {
        public const int Side = 0, Dim = 1, Top = 2;

        private readonly List<VertexPosition> _solids = new();
        private readonly List<DrawRange> _solidRanges = new();
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
        // Adds 5 quads, grouped by shade so each shade is one contiguous range: 2 flanks (Side),
        // nose + tail (Dim), top (Top); plus its 12 edges. With sealBottom the underside is a sixth
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

            AddSolidRange(4, baseSlot + Side);
            AddQuad(a, b, f, e);   // flank (-right)
            AddQuad(c, d, h, g);   // flank (+right)
            AddSolidRange(sealBottom ? 6 : 4, baseSlot + Dim);
            AddQuad(b, c, g, f);   // nose (+forward)
            AddQuad(d, a, e, h);   // tail (-forward)
            if (sealBottom)
                AddQuad(a, b, c, d);   // bottom
            AddSolidRange(2, baseSlot + Top);
            AddQuad(e, f, g, h);   // top

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

            AddSolidRange(sides * 2, sideSlot);
            for (var k = 0; k < sides; k++)
                AddQuad(bottom[k], bottom[(k + 1) % sides], top[(k + 1) % sides], top[k]);
            if (bottomSlot >= 0)
            {
                AddSolidRange(sides - 2, bottomSlot);
                AddPolygon(bottom);
            }
            if (topSlot >= 0)
            {
                AddSolidRange(sides - 2, topSlot);
                AddPolygon(top);
            }

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

            AddSolidRange(sides * 2, sideSlot);
            for (var k = 0; k < sides; k++)
                AddQuad(a[k], a[(k + 1) % sides], b[(k + 1) % sides], b[k]);

            for (var k = 0; k < sides; k++)
                AddLine(a[k], b[k]);
            if (ringEdges)
            {
                AddLineLoop(a);
                AddLineLoop(b);
            }
        }

        // Starts a draw range at the current end of the solids; add exactly `primitives` triangles after it.
        public void AddSolidRange(int primitives, int colorSlot) =>
            _solidRanges.Add(new DrawRange(_solids.Count, primitives, colorSlot));

        public void AddTri(Vector3 a, Vector3 b, Vector3 c)
        {
            _solids.Add(new VertexPosition(a));
            _solids.Add(new VertexPosition(b));
            _solids.Add(new VertexPosition(c));
        }

        public void AddQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            AddTri(a, b, c);
            AddTri(a, c, d);
        }

        // A convex polygon (points in order round its edge) as a triangle fan: points.Length - 2 triangles.
        public void AddPolygon(params Vector3[] points)
        {
            for (var i = 1; i < points.Length - 1; i++)
                AddTri(points[0], points[i], points[i + 1]);
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

        public MeshData Build(GraphicsDevice device) =>
            new MeshData(ToBuffer(device, _solids), _solidRanges.ToArray(), ToBuffer(device, _edges),
                _outlineTriangles.Count > 0 ? new OutlineData(_outlineTriangles) : null);

        private static VertexBuffer ToBuffer(GraphicsDevice device, List<VertexPosition> vertices)
        {
            var buffer = new VertexBuffer(device, typeof(VertexPosition), vertices.Count, BufferUsage.WriteOnly);
            buffer.SetData(vertices.ToArray());
            return buffer;
        }
    }
}
